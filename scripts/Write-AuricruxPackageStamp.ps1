<#
.SYNOPSIS
  Write auricrux/system/package_stamp.json (and publish copy) so hosts can report package identity.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = '',
    [string]$PackageVersion = '1.3.0',
    [string]$PublishDir = '',
    [string]$HostProfile = 'product-gce',
    [string]$RecipeProfile = 'product_gguf_serve_v1',
    [string]$DeploymentSource = 'package_stamp'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Split-Path $PSScriptRoot -Parent
}
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $RepoRoot '_publish\web'
}

$buildUtc = (Get-Date).ToUniversalTime().ToString('o')
$corpusPath = Join-Path $RepoRoot 'Auricrux.Web\Data\construction-corpus.json'
if (-not (Test-Path -LiteralPath $corpusPath) -and (Test-Path -LiteralPath (Join-Path $PublishDir 'Data\construction-corpus.json'))) {
    $corpusPath = Join-Path $PublishDir 'Data\construction-corpus.json'
}
$corpusSha = ''
if (Test-Path -LiteralPath $corpusPath) {
    $corpusSha = (Get-FileHash -LiteralPath $corpusPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

$stamp = [ordered]@{
    schemaVersion = 1
    packageVersion = $PackageVersion
    buildTimestampUtc = $buildUtc
    suiteTarget = 'construction_god_suite_v1'
    suiteVersion = 'v1'
    suitePath = 'eval/construction_god_suite_v1.json'
    evidenceLedgerPath = 'docs/runtime-proof/auricrux_evidence_ledger_v1.json'
    evidenceLedgerJsonlPath = 'docs/runtime-proof/auricrux_evidence_ledger_v1.jsonl'
    hostProfile = $HostProfile
    recipeProfile = $RecipeProfile
    deploymentSource = $DeploymentSource
    corpusSha256 = $corpusSha
    note = 'Generated at build/publish. Runtime PackageIdentityService + RuntimeTruthService add DLL/corpus SHA256. Stamp corpusSha256 anchors Linux image builds vs Windows CRLF publish false-STALE.'
}

$systemDir = Join-Path $RepoRoot 'auricrux\system'
New-Item -ItemType Directory -Force -Path $systemDir | Out-Null
$stampPath = Join-Path $systemDir 'package_stamp.json'
($stamp | ConvertTo-Json -Depth 4) | Set-Content $stampPath -Encoding UTF8

$dests = @(
    (Join-Path $PublishDir 'auricrux\system\package_stamp.json'),
    (Join-Path $PublishDir 'Data\package_stamp.json')
)
foreach ($d in $dests) {
    New-Item -ItemType Directory -Force -Path (Split-Path $d) | Out-Null
    Copy-Item -Force $stampPath $d
}

Write-Host ("PACKAGE_STAMP_OK version={0} buildUtc={1} path={2}" -f $PackageVersion, $buildUtc, $stampPath)
exit 0
