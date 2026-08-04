<#
.SYNOPSIS
  Assert Auricrux evidence rules (offline vs live authority, append-only history, no score inflation).
.DESCRIPTION
  Validates docs/runtime-proof/AURICRUX_EVIDENCE_RULES.md policy against ledger, manifest,
  prior FAIL reports, and alias-rescore sidecars. Never starts training. Never mutates evidence.
.PARAMETER ProductHost
  Expected product host for live authority reports.
.PARAMETER SkipManifestLiveCheck
  Skip requiring manifest PASS to cite a live report (emergency / WIP only).
#>
[CmdletBinding()]
param(
    [string]$ProductHost = 'auricrux.futurecontractorsofamerica.com',
    [switch]$SkipManifestLiveCheck
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
$checks = New-Object System.Collections.Generic.List[object]

function Add-Check([string]$Id, [string]$Status, [string]$Detail) {
    [void]$checks.Add([pscustomobject]@{ id = $Id; status = $Status; detail = $Detail })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
}

Write-Host '=== Auricrux evidence rules assert ===' -ForegroundColor Cyan
Write-Host 'Offline support != live authority. Append only. No score upgrade without rerun.'

# --- ER-01 rules doc present ---
$rulesDoc = Join-Path $repoRoot 'docs\runtime-proof\AURICRUX_EVIDENCE_RULES.md'
if (-not (Test-Path $rulesDoc)) {
    Add-Check 'ER-01-rules-doc' 'FAIL' 'AURICRUX_EVIDENCE_RULES.md missing'
} else {
    $rt = Get-Content $rulesDoc -Raw
    $need = @(
        'Offline package validation',
        'Offline alias rescore',
        'Local suite validation',
        'Live product host validation',
        'Manifest PASS',
        'Release PASS',
        'may not replace live',
        'dated live',
        'without rerun',
        'historically preserved',
        'appended, not overwritten'
    )
    $missing = @($need | Where-Object { $rt -notmatch [regex]::Escape($_) })
    if ($missing.Count -gt 0) {
        Add-Check 'ER-01-rules-doc' 'FAIL' ("Rules doc missing sections: {0}" -f ($missing -join ', '))
    } else {
        Add-Check 'ER-01-rules-doc' 'PASS' 'Canonical evidence rules doc present with required classes'
    }
}

# --- ER-02 prior FAIL preserved ---
$priorFailCandidates = @(
    (Join-Path $repoRoot 'docs\runtime-proof\construction_god_suite_gguf_generative_2026-08-02.json'),
    (Join-Path $repoRoot 'eval\reports\construction_god_suite_gguf_generative_2026-08-02.json'),
    (Join-Path $repoRoot 'eval\reports\construction_god_suite_gguf_generative_2026-08-02_baseline76p7_FAIL.json')
)
$priorFail = $priorFailCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $priorFail) {
    Add-Check 'ER-02-prior-fail-preserved' 'FAIL' 'Prior 2026-08-02 FAIL report missing from runtime-proof/eval/reports'
} else {
    try {
        $pf = Get-Content $priorFail -Raw | ConvertFrom-Json
        $rate = [double]$pf.passRatePercent
        $passed = [bool]$pf.suitePassed
        if ($passed -or $rate -ge 80) {
            Add-Check 'ER-02-prior-fail-preserved' 'FAIL' ("Prior FAIL path rewritten as PASS/score>={0}: {1}" -f $rate, $priorFail)
        } else {
            Add-Check 'ER-02-prior-fail-preserved' 'PASS' ("Prior FAIL retained rate={0}% path={1}" -f $rate, $priorFail)
        }
    } catch {
        Add-Check 'ER-02-prior-fail-preserved' 'FAIL' ("Prior FAIL parse error: {0}" -f $_.Exception.Message)
    }
}

# --- ER-03 ledger append-only / FAIL rows intact ---
$ledgerPath = Join-Path $repoRoot 'docs\runtime-proof\auricrux_evidence_ledger_v1.json'
$jsonlPath = Join-Path $repoRoot 'docs\runtime-proof\auricrux_evidence_ledger_v1.jsonl'
if (-not (Test-Path $ledgerPath)) {
    Add-Check 'ER-03-ledger-append-only' 'FAIL' 'auricrux_evidence_ledger_v1.json missing'
} else {
    try {
        $ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
        $entries = @($ledger.entries)
        $failRows = @($entries | Where-Object { [string]$_.status -eq 'FAIL' })
        $liveAuth = @($entries | Where-Object { [string]$_.authority -eq 'live-dated-host-validation' })
        $badAuth = @($entries | Where-Object {
            $a = [string]$_.authority
            $a -match 'alias|offline' -and [string]$_.status -eq 'PASS'
        })
        if ($failRows.Count -lt 1) {
            Add-Check 'ER-03-ledger-append-only' 'FAIL' 'Ledger has no FAIL rows (history wiped?)'
        } elseif ($badAuth.Count -gt 0) {
            Add-Check 'ER-03-ledger-append-only' 'FAIL' ("Ledger PASS rows with offline/alias authority: {0}" -f $badAuth.Count)
        } else {
            $jsonlOk = Test-Path $jsonlPath
            Add-Check 'ER-03-ledger-append-only' 'PASS' ("entries={0} failRows={1} liveAuth={2} jsonl={3}" -f $entries.Count, $failRows.Count, $liveAuth.Count, $jsonlOk)
        }

        # Detect score mutation of earliest FAIL row vs on-disk prior report
        if ($priorFail -and $failRows.Count -gt 0) {
            $pf = Get-Content $priorFail -Raw | ConvertFrom-Json
            $firstFail = $failRows | Select-Object -First 1
            $ledgerRate = [double]$firstFail.totalScorePercent
            $fileRate = [double]$pf.passRatePercent
            if ([math]::Abs($ledgerRate - $fileRate) -gt 0.15) {
                Add-Check 'ER-04-no-score-upgrade-without-rerun' 'FAIL' ("FAIL ledger score {0} != preserved report {1} (possible overwrite/upgrade)" -f $ledgerRate, $fileRate)
            } else {
                Add-Check 'ER-04-no-score-upgrade-without-rerun' 'PASS' ("FAIL history score stable at {0}%" -f $fileRate)
            }
        } else {
            Add-Check 'ER-04-no-score-upgrade-without-rerun' 'WARN' 'Skipped (no prior FAIL pair)'
        }
    } catch {
        Add-Check 'ER-03-ledger-append-only' 'FAIL' ("Ledger parse error: {0}" -f $_.Exception.Message)
        Add-Check 'ER-04-no-score-upgrade-without-rerun' 'WARN' 'Skipped (ledger parse failed)'
    }
}

# --- ER-05 alias rescore marked support-only ---
$aliasReports = @(
    Get-ChildItem (Join-Path $repoRoot 'docs\runtime-proof') -Filter '*alias_rescore*.json' -ErrorAction SilentlyContinue
    Get-ChildItem (Join-Path $repoRoot 'eval\reports') -Filter '*alias_rescore*.json' -ErrorAction SilentlyContinue
)
if ($aliasReports.Count -eq 0) {
    Add-Check 'ER-05-alias-rescore-support-only' 'WARN' 'No alias_rescore artifacts found (ok if never run)'
} else {
    $bad = @()
    foreach ($f in $aliasReports) {
        try {
            $ar = Get-Content $f.FullName -Raw | ConvertFrom-Json
            $mode = [string]$ar.mode
            $auth = [string]$ar.authority
            $canReplace = $ar.cannotReplaceLive
            if ($mode -notmatch 'offline') { $bad += ("{0}: mode not offline" -f $f.Name) }
            if ($auth -and $auth -notmatch 'support') { $bad += ("{0}: authority={1}" -f $f.Name, $auth) }
            # Newer rescored files must stamp support-only; older files without fields get WARN via missing stamp
            if ($null -eq $ar.PSObject.Properties['authority'] -and $null -eq $ar.PSObject.Properties['cannotReplaceLive']) {
                # Tolerate legacy sidecar if mode is clearly offline and no suitePassed claim as live
                if ($mode -notmatch 'offline') { $bad += ("{0}: legacy without offline mode" -f $f.Name) }
            } elseif ($auth -and ($auth -eq 'live-dated-host-validation')) {
                $bad += ("{0}: claims live authority" -f $f.Name)
            }
            if ($null -ne $canReplace -and [bool]$canReplace -eq $false) {
                # good
            }
        } catch {
            $bad += ("{0}: parse error" -f $f.Name)
        }
    }
    if ($bad.Count -gt 0) {
        Add-Check 'ER-05-alias-rescore-support-only' 'FAIL' ($bad -join '; ')
    } else {
        Add-Check 'ER-05-alias-rescore-support-only' 'PASS' ("alias_rescore files={0} marked/treated support-only" -f $aliasReports.Count)
    }
}

# --- ER-06 rescore script stamps support-only ---
$rescorePy = Join-Path $repoRoot 'scripts\rescore_gguf_report_aliases.py'
if (-not (Test-Path $rescorePy)) {
    Add-Check 'ER-06-rescore-script-stamp' 'FAIL' 'rescore_gguf_report_aliases.py missing'
} else {
    $py = Get-Content $rescorePy -Raw
    if ($py -match 'authority.+support-only' -and $py -match 'cannotReplaceLive' -and $py -match 'offline-excerpt-rescore') {
        Add-Check 'ER-06-rescore-script-stamp' 'PASS' 'Rescore script stamps support-only / cannotReplaceLive'
    } else {
        Add-Check 'ER-06-rescore-script-stamp' 'FAIL' 'Rescore script missing authority=support-only / cannotReplaceLive stamps'
    }
}

# --- ER-07 ledger writer refuses offline authority ---
$ledgerWriter = Join-Path $repoRoot 'scripts\Write-GgufSuiteEvidenceLedger.ps1'
if (-not (Test-Path $ledgerWriter)) {
    Add-Check 'ER-07-ledger-writer-live-only' 'FAIL' 'Write-GgufSuiteEvidenceLedger.ps1 missing'
} else {
    $lw = Get-Content $ledgerWriter -Raw
    if ($lw -match 'live-dated-host-validation' -and $lw -match 'offline-excerpt-rescore|cannot append offline|RefuseOffline|authority.*live') {
        Add-Check 'ER-07-ledger-writer-live-only' 'PASS' 'Ledger writer encodes live authority + offline refusal'
    } elseif ($lw -match 'live-dated-host-validation') {
        Add-Check 'ER-07-ledger-writer-live-only' 'WARN' 'Ledger writer sets live authority; offline refusal guard not detected'
    } else {
        Add-Check 'ER-07-ledger-writer-live-only' 'FAIL' 'Ledger writer missing live-dated-host-validation authority'
    }
}

# --- ER-08 manifest PASS requires dated live validation ---
$manifest = Join-Path $repoRoot 'auricrux\system\model_manifest.json'
if (-not (Test-Path $manifest)) {
    Add-Check 'ER-08-manifest-pass-requires-live' 'FAIL' 'model_manifest.json missing'
} elseif ($SkipManifestLiveCheck) {
    Add-Check 'ER-08-manifest-pass-requires-live' 'WARN' 'Skipped (-SkipManifestLiveCheck)'
} else {
    try {
        $m = Get-Content $manifest -Raw | ConvertFrom-Json
        $eval = [string]$m.adapter.evalStatus
        $suitePassed = [bool]$m.adapter.ggufGenerativeSuitePassed
        $claimsPass = $suitePassed -or ($eval -match 'PASS')
        if (-not $claimsPass) {
            Add-Check 'ER-08-manifest-pass-requires-live' 'PASS' ("Manifest does not claim generative PASS (evalStatus={0})" -f $eval)
        } else {
            $validatedAt = [string]$m.adapter.ggufGenerativeValidatedAtUtc
            $reportRel = [string]$m.adapter.ggufGenerativeReport
            $claimedRate = [double]$m.adapter.ggufGenerativePassRatePercent
            $reportPath = $null
            if (-not [string]::IsNullOrWhiteSpace($reportRel)) {
                $leaf = Split-Path $reportRel -Leaf
                $cand = @(
                    (Join-Path $repoRoot ($reportRel -replace '/', '\')),
                    (Join-Path (Join-Path $repoRoot 'docs\runtime-proof') $leaf),
                    (Join-Path (Join-Path $repoRoot 'eval\reports') $leaf)
                )
                $reportPath = $cand | Where-Object { Test-Path $_ } | Select-Object -First 1
            }
            $errs = @()
            if ([string]::IsNullOrWhiteSpace($validatedAt)) { $errs += 'ggufGenerativeValidatedAtUtc missing' }
            if (-not $reportPath) { $errs += ("ggufGenerativeReport missing on disk: {0}" -f $reportRel) }
            else {
                $rep = Get-Content $reportPath -Raw | ConvertFrom-Json
                $mode = [string]$rep.mode
                $base = [string]$rep.baseUrl
                $rate = [double]$rep.passRatePercent
                if ($mode -match 'offline|alias.rescore') { $errs += 'cited report is offline/alias rescore' }
                if ($base -notmatch [regex]::Escape($ProductHost)) { $errs += ("cited report baseUrl not product host: {0}" -f $base) }
                if ([math]::Abs($rate - $claimedRate) -gt 0.15) {
                    $errs += ("claimed rate {0} != report {1}" -f $claimedRate, $rate)
                }
                if (-not [bool]$rep.suitePassed) { $errs += 'cited report suitePassed=false' }
            }
            if ($errs.Count -gt 0) {
                Add-Check 'ER-08-manifest-pass-requires-live' 'FAIL' ($errs -join '; ')
            } else {
                Add-Check 'ER-08-manifest-pass-requires-live' 'PASS' ("Manifest PASS cites live dated report rate={0}% at={1}" -f $claimedRate, $validatedAt)
            }
        }
    } catch {
        Add-Check 'ER-08-manifest-pass-requires-live' 'FAIL' ("Manifest check error: {0}" -f $_.Exception.Message)
    }
}

# --- ER-09 suite runner never overwrites reports ---
$suiteRunner = Join-Path $repoRoot 'scripts\run-gguf-construction-suite.ps1'
if (-not (Test-Path $suiteRunner)) {
    Add-Check 'ER-09-suite-unique-stamp' 'FAIL' 'run-gguf-construction-suite.ps1 missing'
} else {
    $sr = Get-Content $suiteRunner -Raw
    if ($sr -match 'Never overwrite' -and $sr -match 'yyyy-MM-ddTHHmmssZ') {
        Add-Check 'ER-09-suite-unique-stamp' 'PASS' 'Suite runner uses unique UTC stamps and refuses overwrite'
    } else {
        Add-Check 'ER-09-suite-unique-stamp' 'FAIL' 'Suite runner missing unique-stamp / never-overwrite guards'
    }
}

# --- ER-10 release PASS definition documented (doc-only check) ---
if (Test-Path $rulesDoc) {
    $rt = Get-Content $rulesDoc -Raw
    if ($rt -match 'Release PASS' -and $rt -match 'package identity' -and $rt -match 'safety gate') {
        Add-Check 'ER-10-release-pass-defined' 'PASS' 'Release PASS defined as gate + live suite + identity + history'
    } else {
        Add-Check 'ER-10-release-pass-defined' 'FAIL' 'Release PASS definition incomplete in rules doc'
    }
}

# --- ER-11 ledger integrity companion (no authority elevation) ---
$ledAssert = Join-Path $repoRoot 'scripts\Assert-EvidenceLedgerIntegrity.ps1'
if (-not (Test-Path $ledAssert)) {
    Add-Check 'ER-11-ledger-integrity' 'FAIL' 'Assert-EvidenceLedgerIntegrity.ps1 missing'
} else {
    & $ledAssert 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Add-Check 'ER-11-ledger-integrity' 'FAIL' 'EVIDENCE_LEDGER_INTEGRITY_BLOCKED'
    } else {
        Add-Check 'ER-11-ledger-integrity' 'PASS' 'EVIDENCE_LEDGER_INTEGRITY_OK (authority still only from qualifying evidence)'
    }
}

$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$token = if ($fail -eq 0) { 'EVIDENCE_RULES_OK' } else { 'EVIDENCE_RULES_BLOCKED' }

$receiptDir = Join-Path $repoRoot 'docs\runtime-proof'
New-Item -ItemType Directory -Force -Path $receiptDir | Out-Null
$receipt = [ordered]@{
    atUtc = (Get-Date).ToUniversalTime().ToString('o')
    token = $token
    productHost = $ProductHost
    skipManifestLiveCheck = [bool]$SkipManifestLiveCheck
    passCount = $pass
    failCount = $fail
    rulesDoc = 'docs/runtime-proof/AURICRUX_EVIDENCE_RULES.md'
    checks = $checks
}
$receiptPath = Join-Path $receiptDir 'auricrux-evidence-rules-latest.json'
($receipt | ConvertTo-Json -Depth 6) | Set-Content $receiptPath -Encoding UTF8

Write-Host ''
Write-Host ("Verdict: {0} (PASS={1} FAIL={2})" -f $token, $pass, $fail) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Write-Host ("Receipt: {0}" -f $receiptPath)

if ($fail -gt 0) {
    Write-Host 'BLOCKERS:' -ForegroundColor Red
    $checks | Where-Object { $_.status -eq 'FAIL' } | ForEach-Object {
        Write-Host (" - {0}: {1}" -f $_.id, $_.detail) -ForegroundColor Red
    }
    exit 1
}

Write-Host 'EVIDENCE_RULES_OK'
exit 0
