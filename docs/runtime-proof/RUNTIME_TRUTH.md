# Runtime truth endpoint

**Endpoint:** `GET /api/runtime-truth` (alias: `GET /api/truth`)  
**Service:** `Auricrux.Web/Services/RuntimeTruthService.cs`  
**Assert:** `scripts/Assert-RuntimeTruth.ps1` → `RUNTIME_TRUTH_OK`  
**Purpose:** Operational truth verification only — which package/model a host is actually serving.

## Reported fields

| Field | Meaning |
|-------|---------|
| `activeModel` | Configured primary model tag (e.g. `auricrux-fca`) |
| `activeModelReady` | Ollama reports the primary tag present |
| `activePackageVersion` | Package stamp / config version |
| `activeDllVersion` / `activeDllSha256` | Running assembly identity |
| `corpusVersion` / `corpusSha256` / `corpusEntries` | Grounding corpus identity |
| `hostProfile` | Short host class label (default `product-gce`) |
| `recipeProfile` | Product serve recipe (default `product_gguf_serve_v1`) |
| `suiteCompatibility` | Suite target/version + product-bar compatibility + 80% floor |
| `buildTimestampUtc` | Stamp / DLL build time |
| `deploymentSource` | Short label (`gcp-cutover`, `package_stamp`, `local-dev`, …) |
| `fallbackModeActive` / `fallbackReason` | Corpus-fallback, degraded Ollama, or `*-dev-fallback` / llama3.2 primary |
| `runtimeMode` | `ollama-live` / `ollama-degraded` / `corpus-fallback` |

## Never exposed

- Connection strings, passwords, API keys, SSH private keys  
- Absolute disk paths (`/mnt/...`, `C:\...`)  
- Full Ollama URLs with credentials (host label only via package identity elsewhere)  
- Long deployment URLs with query strings (redacted to host or label)

## Configure (non-secret)

`appsettings.json` / env:

- `Auricrux:HostProfile` / `AURICRUX_HOST_PROFILE`
- `Auricrux:RecipeProfile` / `AURICRUX_RECIPE_PROFILE`
- `Auricrux:DeploymentSource` / `AURICRUX_DEPLOYMENT_SOURCE`

Stamp writer also records these into `package_stamp.json`.

## Commands

```powershell
# Local package checks + live probe
.\scripts\Assert-RuntimeTruth.ps1

# After cutover
Invoke-RestMethod https://auricrux.futurecontractorsofamerica.com/api/runtime-truth | Format-List
```

Until the host is cut over to a build that includes this endpoint, live probe may WARN with 404 — package presence checks can still PASS.
