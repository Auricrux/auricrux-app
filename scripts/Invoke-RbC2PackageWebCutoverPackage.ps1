<#
.SYNOPSIS
  RB-C2 package-web cutover execution package orchestrator (auditable).

.DESCRIPTION
  Phases:
    Preconditions  - Section A GO/NO-GO; never dispatches cutover
    CaptureBaseline - Record pre-cutover host/manifest/git state
    PostVerify      - Section C after operator-run Actions cutover
    RecordEvidence  - Merge outcome note into latest receipt
    ShowDispatch    - Print manual dispatch commands only

  NEVER runs: gh workflow run, gcloud SSH, ollama mutate, train contact.
  NEVER claims: Manifest PASS, suite PASS, Promotion OK, cutover complete
                unless PostVerify criteria are met and -MarkExecuted is used
                after a real operator cutover (still requires proof tokens).

.PARAMETER Phase
  Preconditions | CaptureBaseline | PostVerify | RecordEvidence | ShowDispatch

.PARAMETER GhRunId
  Required for PostVerify / optional for RecordEvidence - Actions run id.

.PARAMETER Outcome
  For RecordEvidence: PREPARED | DISPATCHED | POSTVERIFY_PASS | POSTVERIFY_FAIL | ROLLBACK

.PARAMETER MarkExecuted
  Only with PostVerify: set cutoverExecuted=true in receipt when all success
  criteria pass. Default false even on PASS (operator must opt in to stamp).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Preconditions', 'CaptureBaseline', 'PostVerify', 'RecordEvidence', 'ShowDispatch')]
    [string]$Phase,

    [string]$BaseUrl = 'https://auricrux.futurecontractorsofamerica.com',
    [string]$ExpectedHost = 'auricrux.futurecontractorsofamerica.com',
    [string]$ExpectedProductModel = 'auricrux-fca',
    [string]$PublishDir = '',
    [string]$GhRunId = '',
    [ValidateSet('PREPARED', 'DISPATCHED', 'POSTVERIFY_PASS', 'POSTVERIFY_FAIL', 'ROLLBACK', '')]
    [string]$Outcome = '',
    [string]$Note = '',
    [switch]$MarkExecuted
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot '_publish\web'
}

$proofDir = Join-Path $repoRoot 'docs\runtime-proof'
$latestPath = Join-Path $proofDir 'rb-c2-cutover-execution-package-latest.json'
$packageDoc = 'docs/runtime-proof/RB_C2_PACKAGE_WEB_CUTOVER_EXECUTION_PACKAGE.md'
$workflowRel = '.github/workflows/gcp-cutover-build-auricrux.yml'

function Write-Tok([string]$Token, [string]$Color = 'Cyan') {
    Write-Host $Token -ForegroundColor $Color
}

function Get-UtcStamp {
    return (Get-Date).ToUniversalTime()
}

function Read-Latest {
    if (Test-Path -LiteralPath $latestPath) {
        try { return Get-Content -LiteralPath $latestPath -Raw | ConvertFrom-Json } catch { return $null }
    }
    return $null
}

function Save-Receipt([hashtable]$Doc) {
    $utc = Get-UtcStamp
    $Doc['updatedAtUtc'] = $utc.ToString('o')
    $Doc['packageDoc'] = $packageDoc
    $Doc['blocker'] = 'RB-C2'
    $Doc['scope'] = 'package-web-cutover-only'
    $Doc['claimsForbidden'] = @(
        'Manifest PASS from cutover alone',
        'suite PASS from cutover alone',
        'Release PASS',
        'Promotion OK',
        'model-weight cutover',
        'train interrupt'
    )
    # PS 5.1: pipe-to-ConvertTo-Json on Hashtable can drop content; use -InputObject.
    # Depth must be high enough for checks[] rows.
    $json = ConvertTo-Json -InputObject $Doc -Depth 20
    if ([string]::IsNullOrWhiteSpace($json)) {
        throw 'Save-Receipt produced empty JSON (serialization failure)'
    }
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($latestPath, $json, $utf8)
    $dated = Join-Path $proofDir ('rb-c2-cutover-execution-package-{0:yyyy-MM-dd}.json' -f $utc)
    [System.IO.File]::WriteAllText($dated, $json, $utf8)
    $len = (Get-Item -LiteralPath $latestPath).Length
    if ($len -lt 50) { throw ("Save-Receipt wrote suspiciously small file ({0} bytes)" -f $len) }
    Write-Host ("Receipt: {0} ({1} bytes)" -f $latestPath, $len)
    Write-Host ("Dated:   {0}" -f $dated)
}

function ConvertTo-PlainObject($obj) {
    # Round-trip through JSON so nested ordered/hashtable/PSCustomObject serialize stably.
    if ($null -eq $obj) { return $null }
    return (ConvertTo-Json -InputObject $obj -Depth 20 | ConvertFrom-Json)
}

function Invoke-AssertToken {
    param(
        [string]$Id,
        [string]$ScriptRel,
        [hashtable]$Splat = @{},
        [string]$ReceiptRel = '',
        [string[]]$AcceptTokens
    )
    $scriptPath = Join-Path $repoRoot $ScriptRel
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        return [pscustomobject]@{ id = $Id; status = 'FAIL'; detail = "missing $ScriptRel"; token = $null }
    }
    # Switches must be splatted as hashtables (string '-SkipLiveProbes' does not bind).
    & $scriptPath @Splat | Out-Null
    $exitCode = $LASTEXITCODE
    $token = $null
    if ($ReceiptRel) {
        $rp = Join-Path $repoRoot $ReceiptRel
        if (Test-Path -LiteralPath $rp) {
            try {
                $rj = Get-Content -LiteralPath $rp -Raw | ConvertFrom-Json
                if ($rj.token) { $token = [string]$rj.token }
                elseif ($rj.verdict) { $token = [string]$rj.verdict }
            } catch { }
        }
    }
    if (-not $token) {
        # Fallback: accept exit 0 when only one OK token listed
        if ($exitCode -eq 0 -and $AcceptTokens.Count -eq 1) { $token = $AcceptTokens[0] }
    }
    $ok = $false
    if ($token) {
        foreach ($t in $AcceptTokens) {
            if ($token -eq $t -or $token.StartsWith($t)) { $ok = $true; break }
        }
    }
    return [pscustomobject]@{
        id     = $Id
        status = $(if ($ok) { 'PASS' } else { 'FAIL' })
        detail = $(if ($ok) { $token } else { "expected one of: $($AcceptTokens -join ','); got='$token' exit=$exitCode" })
        token  = $token
        exitCode = $exitCode
    }
}

function Add-Row($list, [string]$Id, [string]$Status, [string]$Detail) {
    [void]$list.Add([pscustomobject]@{ id = $Id; status = $Status; detail = $Detail })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
}

# ---------------------------------------------------------------------------
switch ($Phase) {
    'ShowDispatch' {
        Write-Host '=== RB-C2 manual dispatch (NOT executed by this script) ===' -ForegroundColor Cyan
        Write-Host 'Prerequisites: RB_C2_CUTOVER_PRECONDITIONS_GO + Section B acknowledgements + pushed SHA'
        Write-Host ''
        Write-Host 'gh workflow run gcp-cutover-build-auricrux.yml -f action=full'
        Write-Host 'gh run list --workflow=gcp-cutover-build-auricrux.yml --limit 3'
        Write-Host 'gh run watch'
        Write-Host ''
        Write-Host 'After Actions success:'
        Write-Host '.\scripts\Invoke-RbC2PackageWebCutoverPackage.ps1 -Phase PostVerify -GhRunId <id> [-MarkExecuted]'
        Write-Tok 'RB_C2_CUTOVER_DISPATCH_NOT_EXECUTED' 'Yellow'
        exit 0
    }

    'Preconditions' {
        Write-Host '=== RB-C2 Preconditions (Section A) - no cutover dispatch ===' -ForegroundColor Cyan
        $checks = New-Object System.Collections.Generic.List[object]
        $tokens = [ordered]@{}

        # A1 publish files
        $need = @(
            (Join-Path $PublishDir 'Auricrux.Web.dll'),
            (Join-Path $PublishDir 'Data\construction-corpus.json'),
            (Join-Path $PublishDir 'auricrux\system\package_stamp.json')
        )
        $missing = @($need | Where-Object { -not (Test-Path -LiteralPath $_) })
        if ($missing.Count -eq 0) {
            Add-Row $checks 'A1-publish' 'PASS' 'DLL + corpus + stamp present'
        } else {
            Add-Row $checks 'A1-publish' 'FAIL' ("missing: {0}" -f ($missing -join '; '))
        }

        # A4 stamp fields
        $stampPath = Join-Path $PublishDir 'auricrux\system\package_stamp.json'
        $stamp = $null
        if (Test-Path -LiteralPath $stampPath) {
            $stamp = Get-Content -LiteralPath $stampPath -Raw | ConvertFrom-Json
            $okStamp = $stamp.packageVersion -and $stamp.buildTimestampUtc -and $stamp.suiteTarget -eq 'construction_god_suite_v1' -and $stamp.hostProfile -and $stamp.recipeProfile
            if ($okStamp) {
                Add-Row $checks 'A4-stamp' 'PASS' ("version={0} suite={1} host={2} recipe={3}" -f $stamp.packageVersion, $stamp.suiteTarget, $stamp.hostProfile, $stamp.recipeProfile)
            } else {
                Add-Row $checks 'A4-stamp' 'FAIL' 'stamp missing required fields'
            }
        } else {
            Add-Row $checks 'A4-stamp' 'FAIL' 'stamp missing'
        }

        # A5-A6 files
        $proc = Join-Path $proofDir 'gguf-suite-live-cutover-procedure-2026-08-03.md'
        $base = Join-Path $proofDir 'gguf-grounding-precutover-baseline-2026-08-03.json'
        if ((Test-Path $proc) -and (Select-String -Path $proc -Pattern 'auricrux-web-prev', '/api/health' -Quiet)) {
            Add-Row $checks 'A5-rollback-proc' 'PASS' 'procedure + rollback markers'
        } else {
            Add-Row $checks 'A5-rollback-proc' 'FAIL' 'rollback procedure missing markers'
        }
        if (Test-Path $base) { Add-Row $checks 'A6-baseline' 'PASS' 'precutover baseline present' }
        else { Add-Row $checks 'A6-baseline' 'FAIL' 'precutover baseline missing' }

        # A7 workflow safety
        $wf = Join-Path $repoRoot $workflowRel
        if (Test-Path $wf) {
            $wfText = Get-Content -LiteralPath $wf -Raw
            $hasTrain = $wfText -match 'LIVE 3B TRAIN PROTECTION'
            $hasPrimary = $wfText -match 'PrimaryModel=auricrux-fca'
            $hasPrev = $wfText -match 'prev-\$\(date'
            $mutates = $wfText -match 'ollama\s+(create|rm)\s+auricrux-fca'
            if ($hasTrain -and $hasPrimary -and $hasPrev -and -not $mutates) {
                Add-Row $checks 'A7-workflow' 'PASS' 'train protect + PrimaryModel + prev-rename; no product ollama mutate'
            } else {
                Add-Row $checks 'A7-workflow' 'FAIL' ("train={0} primary={1} prev={2} mutate={3}" -f $hasTrain, $hasPrimary, $hasPrev, $mutates)
            }
        } else {
            Add-Row $checks 'A7-workflow' 'FAIL' 'workflow missing'
        }

        # Assert scripts (receipt-backed tokens; switches via hashtable splat)
        $assertSpecs = @(
            @{ id = 'A3-offline-gate'; rel = 'scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1'; splat = @{ SkipLiveProbes = $true }; receipt = 'docs\runtime-proof\gguf-deployment-safety-gate-latest.json'; accept = @('DEPLOYMENT_SAFETY_GATE_OK') },
            @{ id = 'A8-drill'; rel = 'scripts\Invoke-CutoverRollbackDryRun.ps1'; splat = @{}; receipt = 'docs\runtime-proof\cutover-rollback-drill-latest.json'; accept = @('CUTOVER_ROLLBACK_DRILL_OK_LIVE_BLOCKED', 'CUTOVER_ROLLBACK_DRILL_OK') },
            @{ id = 'A9-train'; rel = 'scripts\Assert-Live3bTrainProtection.ps1'; splat = @{}; receipt = 'docs\runtime-proof\live-3b-train-protection-latest.json'; accept = @('LIVE_3B_TRAIN_PROTECTION_OK') },
            @{ id = 'A10-clobber'; rel = 'scripts\Assert-ProductModelClobberProtection.ps1'; splat = @{}; receipt = 'docs\runtime-proof\product-model-clobber-protection-latest.json'; accept = @('PRODUCT_MODEL_CLOBBER_PROTECTION_OK') },
            @{ id = 'A11-fallback'; rel = 'scripts\Assert-ProductFallbackProfile.ps1'; splat = @{}; receipt = 'docs\runtime-proof\product-fallback-profile-latest.json'; accept = @('PRODUCT_FALLBACK_PROFILE_OK') },
            @{ id = 'A13-evidence'; rel = 'scripts\Assert-AuricruxEvidenceRules.ps1'; splat = @{}; receipt = 'docs\runtime-proof\auricrux-evidence-rules-latest.json'; accept = @('EVIDENCE_RULES_OK') },
            @{ id = 'A15-authority'; rel = 'scripts\Assert-AuricruxAuthorityMap.ps1'; splat = @{}; receipt = 'docs\runtime-proof\authority-map-latest.json'; accept = @('AUTHORITY_MAP_OK') },
            @{ id = 'A18-drift'; rel = 'scripts\Assert-OperationalDrift.ps1'; splat = @{}; receipt = 'docs\runtime-proof\operational-drift-latest.json'; accept = @('OPERATIONAL_DRIFT_OK', 'OPERATIONAL_DRIFT_WARN') }
        )
        foreach ($spec in $assertSpecs) {
            Write-Host ("--- {0} ---" -f $spec.id) -ForegroundColor DarkCyan
            $r = Invoke-AssertToken -Id $spec.id -ScriptRel $spec.rel -Splat $spec.splat -ReceiptRel $spec.receipt -AcceptTokens $spec.accept
            Add-Row $checks $r.id $r.status $(if ($r.token) { $r.token } else { $r.detail })
            if ($r.token) { $tokens[$r.token] = $true }
            if ($spec.id -eq 'A18-drift' -and $r.status -eq 'PASS') {
                $driftReceipt = Join-Path $proofDir 'operational-drift-latest.json'
                if (Test-Path $driftReceipt) {
                    $dr = Get-Content $driftReceipt -Raw | ConvertFrom-Json
                    $failN = 0
                    if ($dr.PSObject.Properties.Name -contains 'failCount') { $failN = [int]$dr.failCount }
                    elseif ($dr.PSObject.Properties.Name -contains 'fail') { $failN = [int]$dr.fail }
                    if ($failN -gt 0) {
                        Add-Row $checks 'A18-drift-failcount' 'FAIL' ("operational drift FAIL count={0}" -f $failN)
                    } else {
                        Add-Row $checks 'A18-drift-failcount' 'PASS' 'drift FAIL count=0'
                    }
                }
            }
        }

        # A12 live health
        try {
            $h = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/health') -TimeoutSec 45
            $pm = [string]$h.primaryModel
            $ready = [bool]$h.primaryModelReady
            $mode = [string]$h.runtimeMode
            if ($pm -eq $ExpectedProductModel -and $ready) {
                Add-Row $checks 'A12-health' 'PASS' ("primaryModel={0} ready mode={1} status={2}" -f $pm, $mode, $h.status)
            } else {
                Add-Row $checks 'A12-health' 'FAIL' ("primaryModel={0} ready={1} mode={2}" -f $pm, $ready, $mode)
            }
            $tokens['preCutoverHealth'] = [ordered]@{
                status = [string]$h.status
                primaryModel = $pm
                primaryModelReady = $ready
                runtimeMode = $mode
            }
        } catch {
            Add-Row $checks 'A12-health' 'FAIL' $_.Exception.Message
        }

        # A14 ledger/manifest align (honest FAIL) - rates live under adapter
        try {
            $ledgerPath = Join-Path $proofDir 'auricrux_evidence_ledger_v1.json'
            $manPath = Join-Path $repoRoot 'auricrux\system\model_manifest.json'
            $ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
            $man = Get-Content $manPath -Raw | ConvertFrom-Json
            $auth = $ledger.currentLiveAuthority
            $rateL = [double]$auth.passRatePercent
            $rateM = [double]$man.adapter.ggufGenerativePassRatePercent
            $statusL = [string]$auth.status
            $evalStatus = [string]$man.adapter.evalStatus
            $noPassClaim = ($evalStatus -match 'FAIL') -and ($evalStatus -notmatch '(?<!FAIL-)PASS' -or $evalStatus -match 'FAIL')
            # Honest FAIL string contains FAIL; reject pure PASS claims
            $honest = ($evalStatus -match 'FAIL') -and ($man.adapter.ggufGenerativeSuitePassed -eq $false)
            if ($statusL -eq 'FAIL' -and [math]::Abs($rateL - $rateM) -lt 0.05 -and $honest) {
                Add-Row $checks 'A14-ledger-manifest' 'PASS' ("both FAIL @{0}% evalStatus={1}" -f $rateL, $evalStatus)
            } else {
                Add-Row $checks 'A14-ledger-manifest' 'FAIL' ("ledger={0}@{1} manifestRate={2} eval={3}" -f $statusL, $rateL, $rateM, $evalStatus)
            }
        } catch {
            Add-Row $checks 'A14-ledger-manifest' 'FAIL' $_.Exception.Message
        }

        # A16 proof pack
        $pack = @(
            'AURICRUX_AUTHORITY_MAP.md',
            'AURICRUX_PRIORITY_OPS_PROCEDURE.md',
            'CUTOVER_ROLLBACK_DRILL.md',
            'CUTOVER_GO_NO_GO_CHECKLIST.md',
            'RB_C2_PACKAGE_WEB_CUTOVER_EXECUTION_PACKAGE.md',
            'auricrux_evidence_ledger_v1.json',
            'authoritative-suite-rerun-prereqs-latest.json'
        )
        $packMiss = @($pack | Where-Object { -not (Test-Path (Join-Path $proofDir $_)) })
        # package-prepared may be dated or latest
        $prepOk = (Test-Path (Join-Path $proofDir 'package-prepared-latest.json')) -or (Test-Path (Join-Path $proofDir 'package-prepared-2026-08-03.json'))
        if ($packMiss.Count -eq 0 -and $prepOk) {
            Add-Row $checks 'A16-proof-pack' 'PASS' 'runtime-proof pack present'
        } else {
            Add-Row $checks 'A16-proof-pack' 'FAIL' ("missing={0} prep={1}" -f ($packMiss -join ','), $prepOk)
        }

        # A17 gh
        try {
            $ghOut = gh auth status 2>&1 | Out-String
            if ($LASTEXITCODE -eq 0 -or $ghOut -match 'Logged in') {
                Add-Row $checks 'A17-gh' 'PASS' 'gh auth usable'
            } else {
                Add-Row $checks 'A17-gh' 'FAIL' $ghOut.Substring(0, [Math]::Min(300, $ghOut.Length))
            }
        } catch {
            Add-Row $checks 'A17-gh' 'FAIL' $_.Exception.Message
        }

        # Git readiness (advisory WARN if dirty/ahead - FAIL only if no remote tracking)
        try {
            Push-Location $repoRoot
            $sb = git status -sb 2>&1 | Out-String
            $head = (git rev-parse HEAD 2>&1 | Out-String).Trim()
            $unpushed = git log '@{u}..HEAD' --oneline 2>&1 | Out-String
            Pop-Location
            if ($unpushed -and $unpushed.Trim() -and ($unpushed -notmatch 'fatal')) {
                Add-Row $checks 'G-unpushed' 'WARN' ("local commits not on upstream - push before dispatch:`n{0}" -f $unpushed.Trim())
            } else {
                Add-Row $checks 'G-unpushed' 'PASS' ("HEAD={0}" -f $head.Substring(0, [Math]::Min(12, $head.Length)))
            }
            $tokens['gitHead'] = $head
            $tokens['gitStatusShort'] = $sb.Trim()
        } catch {
            Add-Row $checks 'G-unpushed' 'WARN' $_.Exception.Message
        }

        $fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
        $pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
        $warn = @($checks | Where-Object { $_.status -eq 'WARN' }).Count
        $go = ($fail -eq 0)
        $verdict = if ($go) { 'RB_C2_CUTOVER_PRECONDITIONS_GO' } else { 'RB_C2_CUTOVER_PRECONDITIONS_NO_GO' }

        $prev = Read-Latest
        $preBlock = [ordered]@{
            atUtc = (Get-UtcStamp).ToString('o')
            verdict = $verdict
            pass = $pass
            fail = $fail
            warn = $warn
            checks = @(foreach ($c in $checks) { [ordered]@{ id = $c.id; status = $c.status; detail = [string]$c.detail } })
            tokens = @{}
            stamp = $(if ($stamp) {
                [ordered]@{
                    packageVersion = [string]$stamp.packageVersion
                    buildTimestampUtc = [string]$stamp.buildTimestampUtc
                    suiteTarget = [string]$stamp.suiteTarget
                    hostProfile = [string]$stamp.hostProfile
                    recipeProfile = [string]$stamp.recipeProfile
                    deploymentSource = [string]$stamp.deploymentSource
                }
            } else { $null })
            publishDir = $PublishDir
        }
        foreach ($k in $tokens.Keys) {
            $v = $tokens[$k]
            if ($v -is [bool] -or $v -is [string] -or $v -is [int] -or $v -is [long] -or $v -is [double]) {
                $preBlock.tokens[$k] = $v
            } elseif ($v -is [System.Collections.IDictionary] -or $v -is [hashtable]) {
                $preBlock.tokens[$k] = $v
            } else {
                $preBlock.tokens[$k] = [string]$v
            }
        }
        $doc = [ordered]@{
            schemaVersion = 1
            purpose = 'rb-c2-package-web-cutover-execution-package'
            cutoverExecuted = $false
            cutoverDispatchedByThisScript = $false
            phases = [ordered]@{
                preconditions = $preBlock
            }
            sectionB_acknowledgementsRequired = $true
            sectionB_checkedByOperator = $false
            nextOperatorAction = $(if ($go) {
                'Check Section B in RB_C2_PACKAGE_WEB_CUTOVER_EXECUTION_PACKAGE.md; push SHA; then: gh workflow run gcp-cutover-build-auricrux.yml -f action=full; then PostVerify'
            } else {
                'Fix FAIL checks; re-run -Phase Preconditions; do not dispatch'
            })
            ghRunId = $null
            outcome = 'PREPARED'
        }
        if ($prev -and $prev.phases) {
            if ($prev.phases.captureBaseline) { $doc.phases['captureBaseline'] = ConvertTo-PlainObject $prev.phases.captureBaseline }
            if ($prev.phases.postVerify) { $doc.phases['postVerify'] = ConvertTo-PlainObject $prev.phases.postVerify }
        }
        Save-Receipt $doc
        Write-Host ''
        Write-Host ("Verdict: {0} (PASS={1} FAIL={2} WARN={3})" -f $verdict, $pass, $fail, $warn) -ForegroundColor $(if ($go) { 'Green' } else { 'Red' })
        Write-Tok $verdict $(if ($go) { 'Green' } else { 'Red' })
        if (-not $go) { exit 2 }
        exit 0
    }

    'CaptureBaseline' {
        Write-Host '=== RB-C2 CaptureBaseline (pre-cutover evidence) ===' -ForegroundColor Cyan
        $baseline = [ordered]@{
            atUtc = (Get-UtcStamp).ToString('o')
            baseUrl = $BaseUrl
            health = $null
            runtimeTruthHttp = $null
            packageIdentityPresent = $false
            capabilitiesOk = $false
            packageHostToken = $null
            note = 'Pre-cutover: missing packageIdentity / runtime-truth 404 is expected for RB-C2'
        }
        try {
            $h = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/health') -TimeoutSec 45
            $baseline.health = [ordered]@{
                status = [string]$h.status
                primaryModel = [string]$h.primaryModel
                primaryModelReady = [bool]$h.primaryModelReady
                runtimeMode = [string]$h.runtimeMode
                packageIdentity = $(if ($h.packageIdentity) { 'present' } else { 'absent' })
            }
            $baseline.packageIdentityPresent = [bool]$h.packageIdentity
        } catch {
            $baseline.health = @{ error = $_.Exception.Message }
        }
        try {
            $rt = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + '/api/runtime-truth') -TimeoutSec 45 -UseBasicParsing
            $baseline.runtimeTruthHttp = [int]$rt.StatusCode
        } catch {
            $resp = $_.Exception.Response
            if ($resp) { $baseline.runtimeTruthHttp = [int]$resp.StatusCode }
            else { $baseline.runtimeTruthHttp = 'error' }
        }
        try {
            Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/capabilities') -TimeoutSec 45 | Out-Null
            $baseline.capabilitiesOk = $true
        } catch {
            $baseline.capabilitiesOk = $false
        }

        # Soft probe - expect BLOCKED before cutover (receipt-backed)
        $phR = Invoke-AssertToken -Id 'baseline-ph' -ScriptRel 'scripts\Assert-PackageHostConsistency.ps1' -ReceiptRel 'docs\runtime-proof\package-host-consistency-latest.json' -AcceptTokens @('PACKAGE_HOST_CONSISTENCY_OK', 'PACKAGE_HOST_CONSISTENCY_BLOCKED')
        $baseline.packageHostToken = $(if ($phR.token) { $phR.token } else { 'UNKNOWN' })

        $prev = Read-Latest
        $doc = [ordered]@{
            schemaVersion = 1
            purpose = 'rb-c2-package-web-cutover-execution-package'
            cutoverExecuted = $(if ($prev) { [bool]$prev.cutoverExecuted } else { $false })
            cutoverDispatchedByThisScript = $false
            phases = [ordered]@{}
            sectionB_acknowledgementsRequired = $true
            outcome = $(if ($prev -and $prev.outcome) { [string]$prev.outcome } else { 'PREPARED' })
            nextOperatorAction = $(if ($prev -and $prev.nextOperatorAction) { [string]$prev.nextOperatorAction } else { 'Run Preconditions then dispatch manually' })
            ghRunId = $(if ($prev -and $prev.ghRunId) { $prev.ghRunId } else { $null })
        }
        if ($prev -and $prev.phases -and $prev.phases.preconditions) {
            $doc.phases['preconditions'] = ConvertTo-PlainObject $prev.phases.preconditions
        }
        $doc.phases['captureBaseline'] = $baseline
        if ($prev -and $prev.phases -and $prev.phases.postVerify) {
            $doc.phases['postVerify'] = ConvertTo-PlainObject $prev.phases.postVerify
        }
        Save-Receipt $doc
        Write-Tok 'RB_C2_CUTOVER_BASELINE_CAPTURED' 'Green'
        Write-Host ("packageHostToken={0} runtimeTruthHttp={1} packageIdentityPresent={2}" -f $baseline.packageHostToken, $baseline.runtimeTruthHttp, $baseline.packageIdentityPresent)
        exit 0
    }

    'PostVerify' {
        Write-Host '=== RB-C2 PostVerify (Section C) - does not dispatch ===' -ForegroundColor Cyan
        if ([string]::IsNullOrWhiteSpace($GhRunId)) {
            Write-Host 'WARN: -GhRunId not set; Actions success (S1) cannot be proven from this run.' -ForegroundColor Yellow
        }

        $checks = New-Object System.Collections.Generic.List[object]
        $tokens = [ordered]@{}

        # S1 Actions
        if ($GhRunId) {
            try {
                $runJson = gh run view $GhRunId --json conclusion,url,headSha,status,displayTitle,workflowName 2>&1 | Out-String
                $run = $runJson | ConvertFrom-Json
                if ($run.conclusion -eq 'success') {
                    Add-Row $checks 'S1-actions' 'PASS' ("conclusion=success sha={0} url={1}" -f $run.headSha, $run.url)
                    $tokens['ghRun'] = $run
                } else {
                    Add-Row $checks 'S1-actions' 'FAIL' ("conclusion={0} status={1}" -f $run.conclusion, $run.status)
                }
            } catch {
                Add-Row $checks 'S1-actions' 'FAIL' $_.Exception.Message
            }
        } else {
            Add-Row $checks 'S1-actions' 'FAIL' 'GhRunId required to prove Actions success'
        }

        # S2 health
        try {
            $h = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/health') -TimeoutSec 45
            if ([string]$h.primaryModel -eq $ExpectedProductModel -and [bool]$h.primaryModelReady) {
                Add-Row $checks 'S2-health' 'PASS' ("status={0} model={1} mode={2}" -f $h.status, $h.primaryModel, $h.runtimeMode)
            } else {
                Add-Row $checks 'S2-health' 'FAIL' ("model={0} ready={1}" -f $h.primaryModel, $h.primaryModelReady)
            }
            $tokens['health'] = $h
        } catch {
            Add-Row $checks 'S2-health' 'FAIL' $_.Exception.Message
        }

        # S3 package host
        Write-Host '--- Assert-PackageHostConsistency ---' -ForegroundColor DarkCyan
        $r3 = Invoke-AssertToken -Id 'S3-package-host' -ScriptRel 'scripts\Assert-PackageHostConsistency.ps1' -ReceiptRel 'docs\runtime-proof\package-host-consistency-latest.json' -AcceptTokens @('PACKAGE_HOST_CONSISTENCY_OK')
        Add-Row $checks $r3.id $r3.status $(if ($r3.token) { $r3.token } else { $r3.detail })
        if ($r3.token) { $tokens[$r3.token] = $true }

        # S4 runtime-truth
        try {
            $rt = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/runtime-truth') -TimeoutSec 45
            $fb = $false
            if ($null -ne $rt.fallbackModeActive) { $fb = [bool]$rt.fallbackModeActive }
            if (-not $fb) {
                Add-Row $checks 'S4-runtime-truth' 'PASS' 'HTTP 200; fallbackModeActive=false (or absent/false)'
            } else {
                Add-Row $checks 'S4-runtime-truth' 'FAIL' 'fallbackModeActive=true'
            }
            $tokens['runtimeTruth'] = $true
        } catch {
            Add-Row $checks 'S4-runtime-truth' 'FAIL' $_.Exception.Message
        }

        # S5 live gate (no SkipLiveProbes)
        Write-Host '--- Assert-GgufSuiteDeploymentSafetyGate (live) ---' -ForegroundColor DarkCyan
        $r5 = Invoke-AssertToken -Id 'S5-live-gate' -ScriptRel 'scripts\Assert-GgufSuiteDeploymentSafetyGate.ps1' -ReceiptRel 'docs\runtime-proof\gguf-deployment-safety-gate-latest.json' -AcceptTokens @('DEPLOYMENT_SAFETY_GATE_OK')
        Add-Row $checks $r5.id $r5.status $(if ($r5.token) { $r5.token } else { $r5.detail })
        if ($r5.token) { $tokens[$r5.token] = $true }

        # S6 clobber
        $r6 = Invoke-AssertToken -Id 'S6-clobber' -ScriptRel 'scripts\Assert-ProductModelClobberProtection.ps1' -ReceiptRel 'docs\runtime-proof\product-model-clobber-protection-latest.json' -AcceptTokens @('PRODUCT_MODEL_CLOBBER_PROTECTION_OK')
        Add-Row $checks $r6.id $r6.status $(if ($r6.token) { $r6.token } else { $r6.detail })

        # S7 train
        $r7 = Invoke-AssertToken -Id 'S7-train' -ScriptRel 'scripts\Assert-Live3bTrainProtection.ps1' -ReceiptRel 'docs\runtime-proof\live-3b-train-protection-latest.json' -AcceptTokens @('LIVE_3B_TRAIN_PROTECTION_OK')
        Add-Row $checks $r7.id $r7.status $(if ($r7.token) { $r7.token } else { $r7.detail })

        # Drift live (supporting)
        Write-Host '--- Assert-OperationalDrift -ProbeLive ---' -ForegroundColor DarkCyan
        $rD = Invoke-AssertToken -Id 'S-drift-live' -ScriptRel 'scripts\Assert-OperationalDrift.ps1' -Splat @{ ProbeLive = $true } -ReceiptRel 'docs\runtime-proof\operational-drift-latest.json' -AcceptTokens @('OPERATIONAL_DRIFT_OK', 'OPERATIONAL_DRIFT_WARN')
        Add-Row $checks $rD.id $(if ($rD.status -eq 'PASS') { 'PASS' } else { 'WARN' }) $(if ($rD.token) { $rD.token } else { $rD.detail })

        $fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
        $pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
        $ok = ($fail -eq 0)
        $verdict = if ($ok) { 'RB_C2_CUTOVER_POSTVERIFY_PASS' } else { 'RB_C2_CUTOVER_POSTVERIFY_FAIL' }

        $executed = $false
        if ($ok -and $MarkExecuted) {
            $executed = $true
        }

        $prev = Read-Latest
        $doc = [ordered]@{
            schemaVersion = 1
            purpose = 'rb-c2-package-web-cutover-execution-package'
            cutoverExecuted = $executed
            cutoverDispatchedByThisScript = $false
            cutoverExecutedNote = $(if ($executed) {
                'Stamped only after PostVerify PASS + -MarkExecuted; does not imply suite/Manifest PASS'
            } elseif ($ok) {
                'PostVerify PASS but cutoverExecuted left false (omit -MarkExecuted); re-run with -MarkExecuted to stamp RB-C2 closure evidence'
            } else {
                'PostVerify FAIL - do not claim RB-C2 closed; consider rollback'
            })
            phases = [ordered]@{}
            sectionB_acknowledgementsRequired = $true
            ghRunId = $(if ($GhRunId) { $GhRunId } else { $null })
            outcome = $(if ($ok) { 'POSTVERIFY_PASS' } else { 'POSTVERIFY_FAIL' })
            nextOperatorAction = $(if ($ok) {
                'RB-C2 eligible to close; optional suite for RB-C1: run-gguf-construction-suite.ps1 (not auto)'
            } else {
                'See failure criteria; rollback if host degraded; do not claim RB-C2'
            })
            rbC1Cleared = $false
            manifestPassClaimed = $false
            promotionOkClaimed = $false
        }
        if ($prev -and $prev.phases) {
            if ($prev.phases.preconditions) { $doc.phases['preconditions'] = $prev.phases.preconditions }
            if ($prev.phases.captureBaseline) { $doc.phases['captureBaseline'] = $prev.phases.captureBaseline }
        }
        $doc.phases['postVerify'] = [ordered]@{
            atUtc = (Get-UtcStamp).ToString('o')
            verdict = $verdict
            pass = $pass
            fail = $fail
            checks = @($checks)
            tokens = $tokens
            markExecutedRequested = [bool]$MarkExecuted
        }
        Save-Receipt $doc
        Write-Host ''
        Write-Host ("Verdict: {0} (PASS={1} FAIL={2})" -f $verdict, $pass, $fail) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
        Write-Tok $verdict $(if ($ok) { 'Green' } else { 'Red' })
        if (-not $ok) { exit 3 }
        exit 0
    }

    'RecordEvidence' {
        if ([string]::IsNullOrWhiteSpace($Outcome)) {
            Write-Error '-Outcome required for RecordEvidence'
            exit 1
        }
        $prev = Read-Latest
        if (-not $prev) {
            Write-Error 'No latest receipt; run Preconditions first'
            exit 1
        }
        $doc = [ordered]@{}
        foreach ($p in $prev.PSObject.Properties) {
            $doc[$p.Name] = $p.Value
        }
        $doc['outcome'] = $Outcome
        $doc['recordNote'] = $Note
        $doc['recordAtUtc'] = (Get-UtcStamp).ToString('o')
        if ($GhRunId) { $doc['ghRunId'] = $GhRunId }
        if ($Outcome -eq 'ROLLBACK') {
            $doc['cutoverExecuted'] = $false
            $doc['nextOperatorAction'] = 'Rollback recorded; stabilize host; re-run Preconditions before retry'
        }
        if ($Outcome -eq 'DISPATCHED') {
            $doc['nextOperatorAction'] = 'Wait for Actions; then PostVerify -GhRunId ...'
            $doc['cutoverDispatchedByThisScript'] = $false
            $doc['operatorDispatchedManually'] = $true
        }
        Save-Receipt $doc
        Write-Tok ('RB_C2_CUTOVER_EVIDENCE_RECORDED_' + $Outcome) 'Cyan'
        exit 0
    }
}
