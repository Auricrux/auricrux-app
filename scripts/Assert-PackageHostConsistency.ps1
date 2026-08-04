<#
.SYNOPSIS
  Package-to-host consistency checker: compare intended publish package vs live product host.
.DESCRIPTION
  Fails loudly on stale, mismatched, or ambiguous hosts.
  Token: PACKAGE_HOST_CONSISTENCY_OK / PACKAGE_HOST_CONSISTENCY_BLOCKED
.PARAMETER AllowMissingPackageIdentity
  Emergency only. Default is FAIL when host lacks packageIdentity (ambiguous/stale).
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'https://auricrux.futurecontractorsofamerica.com',
    [string]$PublishDir = '',
    [string]$ExpectedHost = 'auricrux.futurecontractorsofamerica.com',
    [string]$ExpectedProductModel = 'auricrux-fca',
    [string]$ExpectedSuiteTarget = 'construction_god_suite_v1',
    [switch]$AllowMissingPackageIdentity,
    [switch]$SkipSearchProbe
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot '_publish\web'
}

$checks = New-Object System.Collections.Generic.List[object]
function Add-Check([string]$Id, [string]$Status, [string]$Detail) {
    [void]$checks.Add([pscustomobject]@{ id = $Id; status = $Status; detail = $Detail })
    $col = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1}: {2}" -f $Status, $Id, $Detail) -ForegroundColor $col
}

function Get-Sha256Lower([string]$Path) {
    if (-not (Test-Path $Path)) { return $null }
    return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

function Test-BytesContainAscii([byte[]]$Bytes, [string]$Needle) {
    $n = [Text.Encoding]::ASCII.GetBytes($Needle)
    for ($i = 0; $i -le $Bytes.Length - $n.Length; $i++) {
        $ok = $true
        for ($j = 0; $j -lt $n.Length; $j++) {
            if ($Bytes[$i + $j] -ne $n[$j]) { $ok = $false; break }
        }
        if ($ok) { return $true }
    }
    return $false
}

Write-Host '=== Package-to-host consistency checker ===' -ForegroundColor Cyan
Write-Host ("BaseUrl={0} PublishDir={1}" -f $BaseUrl, $PublishDir)
Write-Host 'Fails loudly on stale / mismatched / ambiguous host.'

# --- Local intended package ---
$pubDll = Join-Path $PublishDir 'Auricrux.Web.dll'
$pubCorpus = Join-Path $PublishDir 'Data\construction-corpus.json'
$pubStamp = Join-Path $PublishDir 'auricrux\system\package_stamp.json'
if (-not (Test-Path $pubStamp)) { $pubStamp = Join-Path $PublishDir 'Data\package_stamp.json' }
$pubManifest = Join-Path $PublishDir 'auricrux\system\model_manifest.json'
$pubAppsettings = Join-Path $PublishDir 'appsettings.json'
if (-not (Test-Path $pubAppsettings)) {
    $pubAppsettings = Join-Path $repoRoot 'Auricrux.Web\appsettings.json'
}

$missingPub = @()
foreach ($p in @($PublishDir, $pubDll, $pubCorpus)) {
    if (-not (Test-Path $p)) { $missingPub += $p }
}
if ($missingPub.Count -gt 0) {
    Add-Check 'PH-01-publish-package' 'FAIL' ("Intended publish package incomplete: {0}" -f ($missingPub -join '; '))
} else {
    Add-Check 'PH-01-publish-package' 'PASS' 'Publish package present (DLL + corpus)'
}

$pubCorpusSha = Get-Sha256Lower $pubCorpus
$pubDllSha = Get-Sha256Lower $pubDll
$pubCorpusEntries = 0
if (Test-Path $pubCorpus) {
    try {
        $cj = Get-Content $pubCorpus -Raw | ConvertFrom-Json
        if ($cj -is [System.Array]) { $pubCorpusEntries = $cj.Count }
        elseif ($cj.entries) { $pubCorpusEntries = @($cj.entries).Count }
        elseif ($cj.PSObject.Properties.Name -contains 'Count') { $pubCorpusEntries = [int]$cj.Count }
        else {
            # array root sometimes deserializes as Object[]
            $pubCorpusEntries = @($cj).Count
        }
    } catch {
        Add-Check 'PH-01b-corpus-parse' 'FAIL' ("Publish corpus parse failed: {0}" -f $_.Exception.Message)
    }
}

$pubStampObj = $null
$pubVersion = $null
$pubSuite = $ExpectedSuiteTarget
if (Test-Path $pubStamp) {
    $pubStampObj = Get-Content $pubStamp -Raw | ConvertFrom-Json
    $pubVersion = [string]$pubStampObj.packageVersion
    if ($pubStampObj.suiteTarget) { $pubSuite = [string]$pubStampObj.suiteTarget }
    Add-Check 'PH-02-publish-stamp' 'PASS' ("stamp version={0} suite={1}" -f $pubVersion, $pubSuite)
} else {
    Add-Check 'PH-02-publish-stamp' 'FAIL' 'package_stamp.json missing from publish package (ambiguous intended build)'
}

$pubPrimary = $ExpectedProductModel
if (Test-Path $pubAppsettings) {
    try {
        $as = Get-Content $pubAppsettings -Raw | ConvertFrom-Json
        if ($as.Auricrux.PrimaryModel) { $pubPrimary = [string]$as.Auricrux.PrimaryModel }
        Add-Check 'PH-03-publish-config' 'PASS' ("appsettings PrimaryModel={0}" -f $pubPrimary)
    } catch {
        Add-Check 'PH-03-publish-config' 'FAIL' ("appsettings parse failed: {0}" -f $_.Exception.Message)
    }
} else {
    Add-Check 'PH-03-publish-config' 'FAIL' 'appsettings.json missing (config ambiguous)'
}

$pubManifestEval = $null
$pubManifestModelId = $null
if (Test-Path $pubManifest) {
    try {
        $mj = Get-Content $pubManifest -Raw | ConvertFrom-Json
        $pubManifestModelId = [string]$mj.modelId
        $pubManifestEval = [string]$mj.adapter.evalStatus
        Add-Check 'PH-04-publish-manifest' 'PASS' ("modelId present; evalStatus={0}" -f $pubManifestEval)
    } catch {
        Add-Check 'PH-04-publish-manifest' 'FAIL' ("manifest parse failed: {0}" -f $_.Exception.Message)
    }
} else {
    Add-Check 'PH-04-publish-manifest' 'FAIL' 'model_manifest.json missing from publish package'
}

# DLL ExpandSearchTerms in intended package
if (Test-Path $pubDll) {
    $bytes = [IO.File]::ReadAllBytes($pubDll)
    if (Test-BytesContainAscii $bytes 'ExpandSearchTerms') {
        Add-Check 'PH-05-publish-expand-search' 'PASS' 'ExpandSearchTerms present in publish DLL'
    } else {
        Add-Check 'PH-05-publish-expand-search' 'FAIL' 'ExpandSearchTerms NOT in publish DLL (search expansion absent)'
    }
} else {
    Add-Check 'PH-05-publish-expand-search' 'FAIL' 'Publish DLL missing'
}

# --- Live host probes ---
$health = $null
$cap = $null
try {
    $uri = [Uri]$BaseUrl
    if ($uri.Host -ne $ExpectedHost -or $uri.Scheme -ne 'https') {
        Add-Check 'PH-06-target-host' 'FAIL' ("Ambiguous/wrong target host={0} scheme={1} (expected https://{2})" -f $uri.Host, $uri.Scheme, $ExpectedHost)
    } else {
        Add-Check 'PH-06-target-host' 'PASS' ("https://{0}" -f $uri.Host)
    }
} catch {
    Add-Check 'PH-06-target-host' 'FAIL' ("Invalid BaseUrl: {0}" -f $_.Exception.Message)
}

try {
    $health = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/health') -TimeoutSec 45
    Add-Check 'PH-07-health-reachable' 'PASS' ("status={0}" -f $health.status)
} catch {
    Add-Check 'PH-07-health-reachable' 'FAIL' ("health probe failed: {0}" -f $_.Exception.Message)
}

try {
    $cap = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/capabilities') -TimeoutSec 45
    Add-Check 'PH-08-capabilities-reachable' 'PASS' 'capabilities OK'
} catch {
    Add-Check 'PH-08-capabilities-reachable' 'FAIL' ("capabilities probe failed: {0}" -f $_.Exception.Message)
}

$livePkg = $null
if ($null -ne $cap -and $null -ne $cap.packageIdentity) { $livePkg = $cap.packageIdentity }
elseif ($null -ne $health -and $null -ne $health.packageIdentity) { $livePkg = $health.packageIdentity }

if ($null -eq $livePkg) {
    $msg = 'AMBIGUOUS/STALE: host does not report packageIdentity - cannot prove intended package is deployed'
    if ($AllowMissingPackageIdentity) {
        Add-Check 'PH-09-package-identity-present' 'WARN' ("{0} (-AllowMissingPackageIdentity)" -f $msg)
    } else {
        Add-Check 'PH-09-package-identity-present' 'FAIL' $msg
    }
} else {
    Add-Check 'PH-09-package-identity-present' 'PASS' ("version={0} stamp={1} source={2}" -f $livePkg.packageVersion, $livePkg.stampFilePresent, $livePkg.stampSource)
}

# Product model name
$livePrimary = $null
if ($null -ne $health -and $health.primaryModel) { $livePrimary = [string]$health.primaryModel }
elseif ($null -ne $cap -and $cap.primaryModel) { $livePrimary = [string]$cap.primaryModel }
elseif ($null -ne $livePkg -and $livePkg.primaryModel) { $livePrimary = [string]$livePkg.primaryModel }

if ([string]::IsNullOrWhiteSpace($livePrimary)) {
    Add-Check 'PH-10-product-model-name' 'FAIL' 'Host primaryModel missing (ambiguous product model)'
} elseif ($livePrimary -ne $ExpectedProductModel) {
    Add-Check 'PH-10-product-model-name' 'FAIL' ("MISMATCH primaryModel live={0} expected={1}" -f $livePrimary, $ExpectedProductModel)
} elseif ($pubPrimary -and $livePrimary -ne $pubPrimary) {
    Add-Check 'PH-10-product-model-name' 'FAIL' ("MISMATCH primaryModel live={0} publish-config={1}" -f $livePrimary, $pubPrimary)
} else {
    $ready = $true
    if ($null -ne $health) { $ready = [bool]$health.primaryModelReady }
    if (-not $ready) {
        Add-Check 'PH-10-product-model-name' 'FAIL' ("primaryModel={0} but primaryModelReady=false (endpoint/model not serving)" -f $livePrimary)
    } else {
        Add-Check 'PH-10-product-model-name' 'PASS' ("primaryModel={0} ready" -f $livePrimary)
    }
}

# Model endpoint (Ollama reachability via health)
if ($null -eq $health) {
    Add-Check 'PH-11-model-endpoint' 'FAIL' 'No health report - cannot verify model endpoint'
} else {
    $ollamaOk = [bool]$health.ollamaReachable
    $mode = [string]$health.runtimeMode
    if (-not $ollamaOk) {
        Add-Check 'PH-11-model-endpoint' 'FAIL' ("Ollama unreachable runtimeMode={0}" -f $mode)
    } elseif ($mode -match 'corpus-fallback') {
        Add-Check 'PH-11-model-endpoint' 'FAIL' 'Model endpoint degraded to corpus-fallback (ambiguous generative path)'
    } else {
        $hint = ''
        if ($null -ne $livePkg -and $livePkg.ollamaEndpointHost) { $hint = [string]$livePkg.ollamaEndpointHost }
        Add-Check 'PH-11-model-endpoint' 'PASS' ("ollamaReachable mode={0} hostHint={1}" -f $mode, $(if ($hint) { $hint } else { 'n/a-until-cutover' }))
    }
}

# Suite target
if ($null -ne $livePkg) {
    $liveSuite = [string]$livePkg.suiteTarget
    if ([string]::IsNullOrWhiteSpace($liveSuite)) {
        Add-Check 'PH-12-suite-target' 'FAIL' 'packageIdentity.suiteTarget empty (ambiguous)'
    } elseif ($liveSuite -ne $pubSuite) {
        Add-Check 'PH-12-suite-target' 'FAIL' ("MISMATCH suiteTarget live={0} publish={1}" -f $liveSuite, $pubSuite)
    } else {
        Add-Check 'PH-12-suite-target' 'PASS' ("suiteTarget={0}" -f $liveSuite)
    }
} else {
    Add-Check 'PH-12-suite-target' 'FAIL' 'Cannot verify suite target without packageIdentity'
}

# Manifest version / evalStatus
if ($null -ne $livePkg) {
    $liveEval = [string]$livePkg.manifestEvalStatus
    $liveMid = [string]$livePkg.manifestModelId
    if ([string]::IsNullOrWhiteSpace($liveEval) -and [string]::IsNullOrWhiteSpace($liveMid)) {
        Add-Check 'PH-13-manifest-version' 'FAIL' 'Host packageIdentity missing manifest linkage (ambiguous)'
    } elseif ($pubManifestEval -and $liveEval -and ($liveEval -ne $pubManifestEval)) {
        Add-Check 'PH-13-manifest-version' 'FAIL' ("MISMATCH evalStatus live={0} publish={1}" -f $liveEval, $pubManifestEval)
    } else {
        Add-Check 'PH-13-manifest-version' 'PASS' ("evalStatus={0}; modelId={1}" -f $(if ($liveEval) { $liveEval } else { '(empty)' }), $(if ($liveMid) { $liveMid.Substring(0, [Math]::Min(40, $liveMid.Length)) } else { '(empty)' }))
    }
} else {
    Add-Check 'PH-13-manifest-version' 'FAIL' 'Cannot verify manifest without packageIdentity'
}

# Corpus files (SHA + entries)
if ($null -ne $livePkg) {
    $liveCorpus = [string]$livePkg.corpusSha256
    $liveEntries = 0
    try { $liveEntries = [int]$livePkg.corpusEntries } catch { $liveEntries = 0 }
    if (-not $liveEntries -and $null -ne $cap) {
        try { $liveEntries = [int]$cap.corpusEntries } catch {}
    }
    if ([string]::IsNullOrWhiteSpace($liveCorpus)) {
        Add-Check 'PH-14-corpus-files' 'FAIL' 'Host corpusSha256 empty (ambiguous corpus)'
    } elseif ($pubCorpusSha -and ($liveCorpus.ToLowerInvariant() -ne $pubCorpusSha)) {
        Add-Check 'PH-14-corpus-files' 'FAIL' ("STALE corpusSha live={0}... publish={1}..." -f $liveCorpus.Substring(0, [Math]::Min(12, $liveCorpus.Length)), $pubCorpusSha.Substring(0, 12))
    } else {
        $entryNote = ''
        if ($pubCorpusEntries -gt 0 -and $liveEntries -gt 0 -and $liveEntries -ne $pubCorpusEntries) {
            Add-Check 'PH-14-corpus-files' 'FAIL' ("MISMATCH corpusEntries live={0} publish={1}" -f $liveEntries, $pubCorpusEntries)
        } else {
            if ($pubCorpusEntries -gt 0) { $entryNote = (" entries={0}" -f $liveEntries) }
            Add-Check 'PH-14-corpus-files' 'PASS' ("corpusSha match{0}" -f $entryNote)
        }
    }
} else {
    Add-Check 'PH-14-corpus-files' 'FAIL' 'Cannot verify corpus without packageIdentity'
}

# Package version stamp
if ($null -ne $livePkg) {
    $liveVer = [string]$livePkg.packageVersion
    if ([string]::IsNullOrWhiteSpace($liveVer)) {
        Add-Check 'PH-15-package-version' 'FAIL' 'Host packageVersion empty'
    } elseif ($pubVersion -and ($liveVer -ne $pubVersion)) {
        Add-Check 'PH-15-package-version' 'FAIL' ("MISMATCH packageVersion live={0} publish={1}" -f $liveVer, $pubVersion)
    } elseif (-not [bool]$livePkg.stampFilePresent) {
        Add-Check 'PH-15-package-version' 'FAIL' 'stampFilePresent=false (ambiguous build stamp on host)'
    } else {
        Add-Check 'PH-15-package-version' 'PASS' ("packageVersion={0} buildUtc={1}" -f $liveVer, $livePkg.buildTimestampUtc)
    }
} else {
    Add-Check 'PH-15-package-version' 'FAIL' 'Cannot verify package version without packageIdentity'
}

# DLL identity
if ($null -ne $livePkg) {
    $liveDll = [string]$livePkg.dllSha256
    $liveDllVer = [string]$livePkg.dllFileVersion
    if ([string]::IsNullOrWhiteSpace($liveDll) -and [string]::IsNullOrWhiteSpace($liveDllVer)) {
        Add-Check 'PH-16-dll-identity' 'FAIL' 'Host DLL identity missing (ambiguous)'
    } elseif ($pubDllSha -and $liveDll -and ($liveDll.ToLowerInvariant() -eq $pubDllSha)) {
        Add-Check 'PH-16-dll-identity' 'PASS' ("DLL sha match {0}..." -f $liveDll.Substring(0, 12))
    } elseif ($pubDllSha -and $liveDll) {
        # Cross-OS rebuild often differs; FAIL only if stamp/corpus also look stale - here WARN unless version also empty
        if ([string]::IsNullOrWhiteSpace($liveDllVer)) {
            Add-Check 'PH-16-dll-identity' 'FAIL' ("DLL sha differs and dllFileVersion empty live={0}... publish={1}..." -f $liveDll.Substring(0, 12), $pubDllSha.Substring(0, 12))
        } else {
            Add-Check 'PH-16-dll-identity' 'WARN' ("DLL sha differs (OS/container rebuild?) live={0}... publish={1}... ver={2} - corpus+version remain authoritative" -f $liveDll.Substring(0, 12), $pubDllSha.Substring(0, 12), $liveDllVer)
        }
    } else {
        Add-Check 'PH-16-dll-identity' 'PASS' ("DLL version reported={0}" -f $(if ($liveDllVer) { $liveDllVer } else { 'sha-only-pending' }))
    }
} else {
    Add-Check 'PH-16-dll-identity' 'FAIL' 'Cannot verify DLL identity without packageIdentity'
}

# Search expansion behavior (live)
if ($SkipSearchProbe) {
    Add-Check 'PH-17-search-expansion' 'WARN' 'Skipped (-SkipSearchProbe)'
} else {
    try {
        $body = @{ query = 'concrete cutting dust respiratory hazard'; searchScope = 'Internal' } | ConvertTo-Json
        $sr = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/api/search') -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 60
        $hits = @($sr.results)
        if ($hits.Count -lt 1) {
            Add-Check 'PH-17-search-expansion' 'FAIL' 'Search returned 0 hits for silica-expansion probe (stale package or broken retrieval)'
        } else {
            $blob = ($hits | ConvertTo-Json -Depth 5 -Compress)
            if ($blob -match '(?i)silica|respirable|respiratory') {
                Add-Check 'PH-17-search-expansion' 'PASS' ("search expansion OK hits={0} (silica/respirable present)" -f $hits.Count)
            } else {
                Add-Check 'PH-17-search-expansion' 'FAIL' ("Search hits lack silica/respirable for cutting-dust probe (ExpandSearchTerms likely absent on host) hits={0}" -f $hits.Count)
            }
        }
    } catch {
        Add-Check 'PH-17-search-expansion' 'FAIL' ("search probe failed: {0}" -f $_.Exception.Message)
    }
}

# Relevant environment / config signals (from packageIdentity when deployed)
if ($null -ne $livePkg) {
    $envBits = @()
    if ($null -ne $livePkg.envPrimaryModelSet) { $envBits += ("PrimaryModelEnv={0}" -f [bool]$livePkg.envPrimaryModelSet) }
    if ($null -ne $livePkg.envOllamaUrlSet) { $envBits += ("OllamaUrlEnv={0}" -f [bool]$livePkg.envOllamaUrlSet) }
    if ($null -ne $livePkg.envPublicHostSet) { $envBits += ("PublicHostEnv={0}" -f [bool]$livePkg.envPublicHostSet) }
    if ($livePkg.ollamaEndpointHost) { $envBits += ("ollamaHost={0}" -f $livePkg.ollamaEndpointHost) }
    if ($livePkg.expandSearchTermsBuiltIn -eq $true) { $envBits += 'expandSearchTermsBuiltIn=true' }
    elseif ($null -eq $livePkg.expandSearchTermsBuiltIn) {
        Add-Check 'PH-18-env-config-signals' 'WARN' 'Host packageIdentity lacks env/config signals (pre-cutover schema) - deploy package with PackageIdentityService updates'
    }
    if ($envBits.Count -gt 0) {
        Add-Check 'PH-18-env-config-signals' 'PASS' ($envBits -join '; ')
    } elseif ($checks | Where-Object { $_.id -eq 'PH-18-env-config-signals' }) {
        # already WARN
    } else {
        Add-Check 'PH-18-env-config-signals' 'WARN' 'No env override signals reported yet'
    }

    # Host reported unambiguous
    $hr = [string]$livePkg.hostReported
    if ([string]::IsNullOrWhiteSpace($hr)) {
        Add-Check 'PH-19-host-unambiguous' 'FAIL' 'hostReported empty (ambiguous which host served identity)'
    } elseif ($hr -notmatch [regex]::Escape($ExpectedHost) -and $hr -ne $ExpectedHost) {
        Add-Check 'PH-19-host-unambiguous' 'FAIL' ("hostReported='{0}' does not match expected '{1}'" -f $hr, $ExpectedHost)
    } else {
        Add-Check 'PH-19-host-unambiguous' 'PASS' ("hostReported={0}" -f $hr)
    }
} else {
    Add-Check 'PH-18-env-config-signals' 'FAIL' 'No packageIdentity - env/config signals unavailable'
    Add-Check 'PH-19-host-unambiguous' 'FAIL' 'No packageIdentity - host identity ambiguous'
}

$fail = @($checks | Where-Object { $_.status -eq 'FAIL' }).Count
$pass = @($checks | Where-Object { $_.status -eq 'PASS' }).Count
$warn = @($checks | Where-Object { $_.status -eq 'WARN' }).Count
$token = if ($fail -eq 0) { 'PACKAGE_HOST_CONSISTENCY_OK' } else { 'PACKAGE_HOST_CONSISTENCY_BLOCKED' }

$receipt = [ordered]@{
    atUtc = (Get-Date).ToUniversalTime().ToString('o')
    token = $token
    baseUrl = $BaseUrl
    publishDir = $PublishDir
    expectedHost = $ExpectedHost
    expectedProductModel = $ExpectedProductModel
    passCount = $pass
    failCount = $fail
    warnCount = $warn
    publish = [ordered]@{
        corpusSha256 = $pubCorpusSha
        dllSha256 = $pubDllSha
        corpusEntries = $pubCorpusEntries
        packageVersion = $pubVersion
        suiteTarget = $pubSuite
        primaryModel = $pubPrimary
        manifestEvalStatus = $pubManifestEval
    }
    live = [ordered]@{
        packageIdentityPresent = ($null -ne $livePkg)
        packageVersion = $(if ($livePkg) { [string]$livePkg.packageVersion } else { $null })
        corpusSha256 = $(if ($livePkg) { [string]$livePkg.corpusSha256 } else { $null })
        dllSha256 = $(if ($livePkg) { [string]$livePkg.dllSha256 } else { $null })
        suiteTarget = $(if ($livePkg) { [string]$livePkg.suiteTarget } else { $null })
        primaryModel = $livePrimary
        manifestEvalStatus = $(if ($livePkg) { [string]$livePkg.manifestEvalStatus } else { $null })
        ollamaReachable = $(if ($health) { [bool]$health.ollamaReachable } else { $null })
        runtimeMode = $(if ($health) { [string]$health.runtimeMode } else { $null })
    }
    checks = $checks
}
$receiptDir = Join-Path $repoRoot 'docs\runtime-proof'
New-Item -ItemType Directory -Force -Path $receiptDir | Out-Null
$receiptPath = Join-Path $receiptDir 'package-host-consistency-latest.json'
($receipt | ConvertTo-Json -Depth 8) | Set-Content $receiptPath -Encoding UTF8

Write-Host ''
Write-Host ("Verdict: {0} (PASS={1} FAIL={2} WARN={3})" -f $token, $pass, $fail, $warn) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
Write-Host ("Receipt: {0}" -f $receiptPath)
if ($fail -gt 0) {
    Write-Host 'BLOCKERS (stale / mismatched / ambiguous):' -ForegroundColor Red
    $checks | Where-Object { $_.status -eq 'FAIL' } | ForEach-Object {
        Write-Host (" - {0}: {1}" -f $_.id, $_.detail) -ForegroundColor Red
    }
    Write-Host 'PACKAGE_HOST_CONSISTENCY_BLOCKED'
    exit 1
}
Write-Host 'PACKAGE_HOST_CONSISTENCY_OK'
exit 0
