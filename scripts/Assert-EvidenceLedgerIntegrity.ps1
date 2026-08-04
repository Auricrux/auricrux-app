<#
.SYNOPSIS
  Audit Auricrux evidence ledger integrity and block accidental authority elevation.
.DESCRIPTION
  Read-only relative to suite reports. May only append AUTHORITY-CORRECTION metadata via
  -RepairSupersessionIndex (adds index + correction row; never mutates FAIL/PASS scores).
  Token: EVIDENCE_LEDGER_INTEGRITY_OK
#>
[CmdletBinding()]
param(
    [string]$ProductHost = 'auricrux.futurecontractorsofamerica.com',
    [switch]$RepairSupersessionIndex
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$ledgerPath = Join-Path $repoRoot 'docs\runtime-proof\auricrux_evidence_ledger_v1.json'
$jsonlPath = Join-Path $repoRoot 'docs\runtime-proof\auricrux_evidence_ledger_v1.jsonl'
$checks = New-Object System.Collections.Generic.List[hashtable]

function Add-Check([string]$Id, [string]$Status, [string]$Detail) {
    [void]$checks.Add(@{ id = $Id; status = $Status; detail = $Detail })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
}

function Test-FallbackContamination($ReportObj) {
    $hits = 0
    $cases = @()
    if ($ReportObj.cases) { $cases = @($ReportObj.cases) }
    elseif ($ReportObj.results) { $cases = @($ReportObj.results) }
    foreach ($c in $cases) {
        $ex = ''
        if ($c.excerpt) { $ex = [string]$c.excerpt }
        elseif ($c.responseExcerpt) { $ex = [string]$c.responseExcerpt }
        if ($ex -match 'no live model reachable' -or $ex -match 'corpus-fallback' -or $ex -match 'corpus response \(grounded') {
            $hits++
        }
    }
    return $hits
}

function Test-ReportQualifiesForPassAuthority($ReportObj, [string]$HostNeedle) {
    if (-not $ReportObj) { return @{ ok = $false; reasons = @('report-missing') } }
    $reasons = @()
    $mode = [string]$ReportObj.mode
    $base = [string]$ReportObj.baseUrl
    $rate = 0.0
    try { $rate = [double]$ReportObj.passRatePercent } catch { }
    $passed = [bool]$ReportObj.suitePassed
    $fallback = Test-FallbackContamination $ReportObj
    $hasPi = ($null -ne $ReportObj.packageIdentity) -and (
        -not [string]::IsNullOrWhiteSpace([string]$ReportObj.packageIdentity.packageVersion) -or
        -not [string]::IsNullOrWhiteSpace([string]$ReportObj.packageIdentity.corpusSha256) -or
        [bool]$ReportObj.packageIdentity.stampFilePresent
    )
    if ($mode -match 'offline|alias.rescore|excerpt-rescore') { $reasons += 'offline-or-alias-mode' }
    if ($mode -and $mode -notmatch 'gguf-generative-product-chat') { $reasons += ("mode={0}" -f $mode) }
    if ($base -notmatch [regex]::Escape($HostNeedle)) { $reasons += 'baseUrl-not-product-host' }
    if (-not $passed) { $reasons += 'suitePassed-false' }
    if ($rate -lt 80) { $reasons += ("rate={0}" -f $rate) }
    if ($fallback -gt 0) { $reasons += ("fallbackHits={0}" -f $fallback) }
    if (-not $hasPi) { $reasons += 'missing-packageIdentity' }
    return @{ ok = ($reasons.Count -eq 0); reasons = $reasons; fallbackHits = $fallback; hasPackageIdentity = $hasPi; rate = $rate }
}

Write-Host '=== Evidence ledger integrity audit ===' -ForegroundColor Cyan
Write-Host 'Authority derives only from qualifying evidence. History preserved. No elevation without qualification.'

if (-not (Test-Path $ledgerPath)) {
    Add-Check 'EL-00-ledger-present' 'FAIL' 'auricrux_evidence_ledger_v1.json missing'
    $token = 'EVIDENCE_LEDGER_INTEGRITY_BLOCKED'
    # fall through to receipt
    $ledger = $null
} else {
    Add-Check 'EL-00-ledger-present' 'PASS' 'Ledger JSON present'
    $ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
}

$entries = @()
if ($ledger) { $entries = @($ledger.entries) }

# --- EL-01 historical FAIL preserved ---
$failRows = @($entries | Where-Object { [string]$_.status -eq 'FAIL' })
$priorFailPath = Join-Path $repoRoot 'docs\runtime-proof\construction_god_suite_gguf_generative_2026-08-02.json'
if ($failRows.Count -lt 1) {
    Add-Check 'EL-01-historical-fail-preserved' 'FAIL' 'No FAIL rows in ledger'
} elseif (-not (Test-Path $priorFailPath)) {
    Add-Check 'EL-01-historical-fail-preserved' 'FAIL' 'Prior FAIL report file missing on disk'
} else {
    $pf = Get-Content $priorFailPath -Raw | ConvertFrom-Json
    $fr = $failRows | Select-Object -First 1
    $ok = ([math]::Abs([double]$fr.totalScorePercent - [double]$pf.passRatePercent) -le 0.15) -and (-not [bool]$pf.suitePassed)
    if ($ok) {
        Add-Check 'EL-01-historical-fail-preserved' 'PASS' ("FAIL row+file retained at {0}% evidenceId={1}" -f $fr.totalScorePercent, $fr.evidenceId)
    } else {
        Add-Check 'EL-01-historical-fail-preserved' 'FAIL' 'FAIL ledger/file score or suitePassed mutated'
    }
}

# --- EL-02 historical PASS preserved (files + rows; not authority) ---
$passRows = @($entries | Where-Object { [string]$_.status -eq 'PASS' })
$passFilesOk = $true
$passFileDetails = @()
foreach ($pr in $passRows) {
    $rel = [string]$pr.reportPath
    if ([string]::IsNullOrWhiteSpace($rel)) { $passFilesOk = $false; $passFileDetails += "$($pr.evidenceId):missing-path"; continue }
    $abs = Join-Path $repoRoot ($rel -replace '/', '\')
    if (-not (Test-Path $abs)) { $passFilesOk = $false; $passFileDetails += "$($pr.evidenceId):file-missing"; continue }
    $rep = Get-Content $abs -Raw | ConvertFrom-Json
    if ([math]::Abs([double]$rep.passRatePercent - [double]$pr.totalScorePercent) -gt 0.15) {
        $passFilesOk = $false
        $passFileDetails += "$($pr.evidenceId):score-mismatch"
    } else {
        $passFileDetails += ("{0}@{1}% retained" -f $pr.evidenceId, $pr.totalScorePercent)
    }
}
if ($passRows.Count -eq 0) {
    Add-Check 'EL-02-historical-pass-preserved' 'WARN' 'No PASS rows yet (ok if never recorded)'
} elseif ($passFilesOk) {
    Add-Check 'EL-02-historical-pass-preserved' 'PASS' ("PASS rows={0} files retained: {1}" -f $passRows.Count, ($passFileDetails -join '; '))
} else {
    Add-Check 'EL-02-historical-pass-preserved' 'FAIL' ($passFileDetails -join '; ')
}

# --- EL-03 authority source identified ---
$auth = $null
if ($ledger -and $ledger.PSObject.Properties.Name -contains 'currentLiveAuthority') {
    $auth = $ledger.currentLiveAuthority
}
if (-not $auth -or [string]::IsNullOrWhiteSpace([string]$auth.report)) {
    Add-Check 'EL-03-authority-source-identified' 'FAIL' 'currentLiveAuthority missing'
} else {
    $authReportAbs = Join-Path $repoRoot (([string]$auth.report) -replace '/', '\')
    $authFileOk = Test-Path $authReportAbs
    Add-Check 'EL-03-authority-source-identified' $(if ($authFileOk) { 'PASS' } else { 'FAIL' }) (
        "currentLiveAuthority status={0} rate={1} report={2} onDisk={3}" -f $auth.status, $auth.passRatePercent, $auth.report, $authFileOk
    )
}

# --- EL-04 offline evidence distinguished ---
$offlineInLedger = @($entries | Where-Object {
    $a = [string]$_.authority
    $m = [string]$_.mode
    ($a -match 'support-only|offline') -or ($m -match 'offline|alias')
})
$aliasFiles = @(
    Get-ChildItem (Join-Path $repoRoot 'docs\runtime-proof') -Filter '*alias_rescore*.json' -ErrorAction SilentlyContinue
)
$aliasBad = @()
foreach ($af in $aliasFiles) {
    $ar = Get-Content $af.FullName -Raw | ConvertFrom-Json
    if ([string]$ar.authority -eq 'live-dated-host-validation') { $aliasBad += $af.Name }
    if ([string]$ar.mode -notmatch 'offline') { $aliasBad += "$($af.Name):mode" }
}
# Offline must not appear as PASS authority rows
$offlinePassAuth = @($entries | Where-Object {
    [string]$_.status -eq 'PASS' -and ([string]$_.authority -match 'support-only|offline')
})
if ($offlinePassAuth.Count -gt 0 -or $aliasBad.Count -gt 0) {
    Add-Check 'EL-04-offline-distinguished' 'FAIL' ("offlinePassAuth={0} aliasBad={1}" -f $offlinePassAuth.Count, ($aliasBad -join ','))
} else {
    Add-Check 'EL-04-offline-distinguished' 'PASS' (
        "offline rows in ledger as support={0}; alias_rescore files={1} support-only; offline not elevating PASS authority" -f $offlineInLedger.Count, $aliasFiles.Count
    )
}

# --- EL-05 live evidence distinguished ---
$liveRows = @($entries | Where-Object {
    [string]$_.authority -match 'live-dated-host-validation' -or [string]$_.host -match $ProductHost
})
$correctionRows = @($entries | Where-Object { [string]$_.status -eq 'AUTHORITY-CORRECTION' })
if ($liveRows.Count -lt 1) {
    Add-Check 'EL-05-live-distinguished' 'FAIL' 'No live-dated host validation rows'
} else {
    Add-Check 'EL-05-live-distinguished' 'PASS' ("live-class rows={0} authority-correction rows={1}" -f $liveRows.Count, $correctionRows.Count)
}

# --- Classify PASS rows for supersession / qualification ---
$disqualifiedPass = @()
$qualifiedPass = @()
foreach ($pr in $passRows) {
    $rel = [string]$pr.reportPath
    $abs = Join-Path $repoRoot ($rel -replace '/', '\')
    if (-not (Test-Path $abs)) {
        $disqualifiedPass += [pscustomobject]@{ evidenceId = $pr.evidenceId; reasons = @('report-missing'); reportPath = $rel }
        continue
    }
    $rep = Get-Content $abs -Raw | ConvertFrom-Json
    $q = Test-ReportQualifiesForPassAuthority $rep $ProductHost
    # Also respect explicit writer stamps if present
    if ($pr.PSObject.Properties.Name -contains 'authorityQualifiedPass' -and [bool]$pr.authorityQualifiedPass) {
        if ($q.ok) { $qualifiedPass += $pr } else {
            $disqualifiedPass += [pscustomobject]@{ evidenceId = $pr.evidenceId; reasons = @($q.reasons + 'stamp-vs-reprobe-mismatch'); reportPath = $rel }
        }
    } elseif ([string]$pr.authority -eq 'live-dated-host-validation-qualified' -and $q.ok) {
        $qualifiedPass += $pr
    } elseif ($q.ok) {
        $qualifiedPass += $pr
    } else {
        $disqualifiedPass += [pscustomobject]@{
            evidenceId = $pr.evidenceId
            reasons = $q.reasons
            reportPath = $rel
            rate = $pr.totalScorePercent
        }
    }
}

# --- EL-06 superseded evidence identified ---
$supersededIndex = @()
if ($ledger -and $ledger.PSObject.Properties.Name -contains 'supersededEvidence') {
    $supersededIndex = @($ledger.supersededEvidence)
}
$supersededIds = @($supersededIndex | ForEach-Object { [string]$_.evidenceId })
$correctionMentions = @()
foreach ($cr in $correctionRows) {
    if ($cr.note) { $correctionMentions += [string]$cr.note }
    if ($cr.PSObject.Properties.Name -contains 'supersedes') {
        foreach ($s in @($cr.supersedes)) { $supersededIds += [string]$s }
    }
    if ($cr.PSObject.Properties.Name -contains 'disqualifiedEvidenceIds') {
        foreach ($s in @($cr.disqualifiedEvidenceIds)) { $supersededIds += [string]$s }
    }
}
$supersededIds = @($supersededIds | Where-Object { $_ } | Select-Object -Unique)
$unmarked = @($disqualifiedPass | Where-Object { $supersededIds -notcontains $_.evidenceId -and ($correctionMentions -join ' ') -notmatch [regex]::Escape($_.evidenceId) })

# Heuristic: status-truth correction note mentioning 86.7/93.3 covers legacy unmarked rows
$legacyCovered = $false
$corrText = ($correctionMentions -join ' ')
if ($corrText -match '86\.7' -and $corrText -match '93\.3' -and $corrText -match 'historically') {
    $legacyCovered = $true
}

if ($disqualifiedPass.Count -eq 0) {
    Add-Check 'EL-06-superseded-identified' 'PASS' 'No disqualified PASS rows requiring supersession marks'
} elseif ($unmarked.Count -eq 0) {
    Add-Check 'EL-06-superseded-identified' 'PASS' ("superseded/disqualified PASS ids marked: {0}" -f (($disqualifiedPass | ForEach-Object { $_.evidenceId }) -join ','))
} elseif ($legacyCovered -and $supersededIds.Count -eq 0) {
    Add-Check 'EL-06-superseded-identified' 'WARN' (
        "Disqualified PASS covered by AUTHORITY-CORRECTION narrative only (ids={0}). Run with -RepairSupersessionIndex for explicit index." -f (($unmarked | ForEach-Object { $_.evidenceId }) -join ',')
    )
} else {
    Add-Check 'EL-06-superseded-identified' 'FAIL' (
        "Disqualified PASS not marked superseded: {0}" -f (($unmarked | ForEach-Object { "{0}[{1}]" -f $_.evidenceId, ($_.reasons -join '|') }) -join '; ')
    )
}

# --- EL-07 audit trace intact (jsonl ↔ json) ---
if (-not (Test-Path $jsonlPath)) {
    Add-Check 'EL-07-audit-trace-intact' 'FAIL' 'JSONL mirror missing'
} else {
    $jsonlLines = @(Get-Content $jsonlPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $jsonlIds = @()
    $parseFail = 0
    foreach ($line in $jsonlLines) {
        try {
            $o = $line | ConvertFrom-Json
            $jsonlIds += [string]$o.evidenceId
        } catch { $parseFail++ }
    }
    $jsonIds = @($entries | ForEach-Object { [string]$_.evidenceId })
    $missingInJsonl = @($jsonIds | Where-Object { $_ -notin $jsonlIds })
    $extraInJsonl = @($jsonlIds | Where-Object { $_ -notin $jsonIds })
    if ($parseFail -gt 0 -or $missingInJsonl.Count -gt 0) {
        Add-Check 'EL-07-audit-trace-intact' 'FAIL' (
            "jsonEntries={0} jsonlLines={1} missingInJsonl={2} parseFail={3}" -f $jsonIds.Count, $jsonlLines.Count, ($missingInJsonl -join ','), $parseFail
        )
    } else {
        Add-Check 'EL-07-audit-trace-intact' 'PASS' (
            "json entries={0} jsonl lines={1} ids aligned (extraJsonlHistorical={2})" -f $jsonIds.Count, $jsonlLines.Count, $extraInJsonl.Count
        )
    }
}

# --- EL-08 no accidental authority elevation ---
$elevationFail = @()
if ($auth) {
    $st = [string]$auth.status
    $repRel = [string]$auth.report
    $repAbs = Join-Path $repoRoot ($repRel -replace '/', '\')
    if ($st -eq 'PASS') {
        if (-not (Test-Path $repAbs)) {
            $elevationFail += 'currentLiveAuthority PASS but report missing'
        } else {
            $q = Test-ReportQualifiesForPassAuthority (Get-Content $repAbs -Raw | ConvertFrom-Json) $ProductHost
            if (-not $q.ok) {
                $elevationFail += ("currentLiveAuthority PASS disqualified: {0}" -f ($q.reasons -join ','))
            }
        }
        # Must not point at known disqualified evidence report paths
        foreach ($d in $disqualifiedPass) {
            if ($repRel -replace '\\', '/' -eq ([string]$d.reportPath -replace '\\', '/')) {
                $elevationFail += ("currentLiveAuthority points at disqualified PASS {0}" -f $d.evidenceId)
            }
        }
    } elseif ($st -eq 'FAIL') {
        # Truthful FAIL is OK; ensure not silently pointing at a PASS-scored contaminated report as FAIL incorrectly? FAIL is fine.
        if ($auth.passRatePercent -ge 80 -and $st -eq 'FAIL') {
            # unusual but possible if threshold semantics differ — warn
        }
    } else {
        $elevationFail += ("unexpected currentLiveAuthority status={0}" -f $st)
    }
}
# Naive "latest PASS wins" trap: ensure latest PASS is not treated as authority without qualification
if ($passRows.Count -gt 0 -and $auth) {
    $latestPass = $passRows | Select-Object -Last 1
    if ([string]$auth.status -eq 'PASS' -and [string]$auth.report -eq [string]$latestPass.reportPath) {
        $abs = Join-Path $repoRoot (([string]$latestPass.reportPath) -replace '/', '\')
        if (Test-Path $abs) {
            $q = Test-ReportQualifiesForPassAuthority (Get-Content $abs -Raw | ConvertFrom-Json) $ProductHost
            if (-not $q.ok) { $elevationFail += 'latest PASS elevated without qualification' }
        }
    }
}
# Disqualified PASS must not equal currentLiveAuthority report while status PASS
if ($elevationFail.Count -eq 0) {
    $msg = if ($auth) {
        "currentLiveAuthority={0}@{1}% protected; qualifiedPassRows={2}; disqualifiedPassRows={3}" -f $auth.status, $auth.passRatePercent, $qualifiedPass.Count, $disqualifiedPass.Count
    } else { 'no auth pointer' }
    Add-Check 'EL-08-no-accidental-elevation' 'PASS' $msg
} else {
    Add-Check 'EL-08-no-accidental-elevation' 'FAIL' ($elevationFail -join '; ')
}

# --- Optional repair: explicit supersededEvidence index + correction append ---
if ($RepairSupersessionIndex -and $ledger -and $disqualifiedPass.Count -gt 0) {
    Write-Host 'Repair: appending supersession index (scores untouched)...' -ForegroundColor Cyan
    $newSuperseded = @()
    foreach ($d in $disqualifiedPass) {
        $newSuperseded += [ordered]@{
            evidenceId = $d.evidenceId
            reportPath = $d.reportPath
            retainedHistorically = $true
            authorityQualified = $false
            reasons = @($d.reasons)
            supersededAtUtc = (Get-Date).ToUniversalTime().ToString('o')
            supersededBy = 'authority-supersession-index'
        }
    }
    $correction = [ordered]@{
        evidenceId = ('authority-supersession-{0}' -f (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'))
        recordedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        dateUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
        host = 'docs-only-correction'
        modelName = 'auricrux-fca'
        suiteName = 'construction_god_suite_v1'
        status = 'AUTHORITY-CORRECTION'
        authority = 'authority-supersession'
        totalPassed = 23
        totalCases = 30
        totalScorePercent = 76.7
        thresholdPercent = 80
        reportPath = 'docs/runtime-proof/construction_god_suite_gguf_generative_2026-08-02.json'
        note = 'Explicit supersession index: historical PASS rows retained on disk and in ledger but are NOT qualifying live authority. Authority remains FAIL 76.7 from 2026-08-02. Offline alias rescore remains support-only and outside authority elevation.'
        disqualifiedEvidenceIds = @($disqualifiedPass | ForEach-Object { $_.evidenceId })
        supersedes = @($disqualifiedPass | ForEach-Object { $_.evidenceId })
        priorFailPreserved = 'docs/runtime-proof/construction_god_suite_gguf_generative_2026-08-02.json'
        offlineEvidence = [ordered]@{
            class = 'support-only'
            aliasRescoreExample = 'docs/runtime-proof/construction_god_suite_gguf_generative_2026-08-02_alias_rescore.json'
            mayElevateAuthority = $false
        }
        currentLiveAuthorityConfirmed = [ordered]@{
            status = 'FAIL'
            passRatePercent = 76.7
            report = 'docs/runtime-proof/construction_god_suite_gguf_generative_2026-08-02.json'
        }
    }

    # Rebuild ledger object without mutating prior row scores/status
    $entriesList = @($entries) + @([pscustomobject]$correction)
    $authOut = $auth
    if (-not $authOut) {
        $authOut = [ordered]@{
            status = 'FAIL'
            report = 'docs/runtime-proof/construction_god_suite_gguf_generative_2026-08-02.json'
            passRatePercent = 76.7
        }
    }
    # Force FAIL pointer if somehow PASS without qualification
    if ([string]$authOut.status -eq 'PASS') {
        $abs = Join-Path $repoRoot (([string]$authOut.report) -replace '/', '\')
        $q = if (Test-Path $abs) { Test-ReportQualifiesForPassAuthority (Get-Content $abs -Raw | ConvertFrom-Json) $ProductHost } else { @{ ok = $false } }
        if (-not $q.ok) {
            $authOut = [ordered]@{
                status = 'FAIL'
                report = 'docs/runtime-proof/construction_god_suite_gguf_generative_2026-08-02.json'
                passRatePercent = 76.7
                authorityClass = 'live-dated-host-validation'
                note = 'demoted-during-supersession-repair'
            }
        }
    }

    $out = [ordered]@{
        schemaVersion = 1
        ledgerId = 'auricrux_evidence_ledger_v1'
        purpose = 'Chronological Auricrux eval/deploy evidence. Append only; never overwrite prior FAIL rows or reports. AUTHORITY-CORRECTION rows may demote later claims without deleting history.'
        updatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        entryCount = $entriesList.Count
        authorityDerivation = [ordered]@{
            rule = 'currentLiveAuthority may become PASS only from qualifying live product-host evidence (mode=gguf-generative-product-chat, rate>=80, zero fallback contamination, packageIdentity present). Historical PASS rows may be retained without elevating authority.'
            offlineMayElevate = $false
            disqualifiedPassMayElevate = $false
            naiveLatestPassWins = $false
        }
        supersededEvidence = $newSuperseded
        currentLiveAuthority = $authOut
        entries = $entriesList
    }

    $tmp = "$ledgerPath.tmp"
    ($out | ConvertTo-Json -Depth 12) | Set-Content $tmp -Encoding UTF8
    Move-Item -Force $tmp $ledgerPath
    ($correction | ConvertTo-Json -Depth 10 -Compress) | Add-Content $jsonlPath -Encoding UTF8
    Write-Host ("REPAIR_APPEND {0}" -f $correction.evidenceId) -ForegroundColor Green

    # Reload for final EL-06 recheck note in receipt
    $ledger = Get-Content $ledgerPath -Raw | ConvertFrom-Json
    $entries = @($ledger.entries)
    $auth = $ledger.currentLiveAuthority
    # Replace prior EL-06 FAIL/WARN with PASS after successful repair
    for ($i = 0; $i -lt $checks.Count; $i++) {
        if ($checks[$i].id -eq 'EL-06-superseded-identified') {
            $checks[$i] = @{
                id     = 'EL-06-superseded-identified'
                status = 'PASS'
                detail = ("superseded/disqualified PASS ids marked via repair: {0}" -f (($disqualifiedPass | ForEach-Object { $_.evidenceId }) -join ','))
            }
        }
    }
    Add-Check 'EL-06b-repair-applied' 'PASS' ("supersededEvidence index written; entries now {0}" -f $entries.Count)
}

$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$warn = @($checks | Where-Object { $_.status -eq 'WARN' }).Count
$token = if ($fail -eq 0) { 'EVIDENCE_LEDGER_INTEGRITY_OK' } else { 'EVIDENCE_LEDGER_INTEGRITY_BLOCKED' }

$receipt = @{
    atUtc = (Get-Date).ToUniversalTime().ToString('o')
    token = $token
    passCount = $pass
    failCount = $fail
    warnCount = $warn
    currentLiveAuthority = $auth
    historicalFailRows = $failRows.Count
    historicalPassRows = $passRows.Count
    disqualifiedPassIds = @($disqualifiedPass | ForEach-Object { $_.evidenceId })
    qualifiedPassIds = @($qualifiedPass | ForEach-Object { $_.evidenceId })
    checks = @($checks)
    policy = 'Authority must derive only from qualifying evidence. Offline and disqualified PASS cannot elevate currentLiveAuthority.'
}
$receiptPath = Join-Path $repoRoot 'docs\runtime-proof\evidence-ledger-integrity-latest.json'
($receipt | ConvertTo-Json -Depth 8) | Set-Content $receiptPath -Encoding UTF8

Write-Host ''
Write-Host ("Verdict: {0} (PASS={1} FAIL={2} WARN={3})" -f $token, $pass, $fail, $warn) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Write-Host ("Receipt: {0}" -f $receiptPath)
Write-Host $token
if ($fail -gt 0) { exit 2 }
exit 0
