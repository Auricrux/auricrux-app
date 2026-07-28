# Custom Domain Setup — auricrux.futurecontractorsofamerica.com

**Claim:** AUX-020 (PARTIAL until public DNS routes to the live API)  
**Azure App Service:** `fca-auricrux-api` in resource group `Auricrux_group`  
**Default host (live today):** https://fca-auricrux-api.azurewebsites.net

## Current status (verified 2026-07-28)

| Endpoint | `fca-auricrux-api.azurewebsites.net` | `auricrux.futurecontractorsofamerica.com` |
|----------|--------------------------------------|-------------------------------------------|
| `GET /api/health` | ✅ 200 | ❌ 404 (DNS not pointed) |
| `GET /api/models` | ✅ 200 | ❌ 404 |
| `POST /api/chat` | ✅ 200 | ❌ 404 |

The Azure App Service is running the real Auricrux stack (see `scripts/smoke_prod.ps1`). The custom domain hostname is **not yet bound** in Azure and **DNS at Porkbun does not route** to this App Service.

## Step 1 — DNS at Porkbun (founder action required)

Log in to [Porkbun](https://porkbun.com) → DNS for `futurecontractorsofamerica.com`:

| Type | Host | Value | TTL |
|------|------|-------|-----|
| CNAME | `auricrux` | `fca-auricrux-api.azurewebsites.net` | 600 |

Alternatively use an **A record** to the App Service outbound IP (less portable):

```powershell
az webapp show --name fca-auricrux-api --resource-group Auricrux_group --query outboundIpAddresses -o tsv
```

## Step 2 — Bind hostname in Azure

After DNS propagates (usually 5–30 minutes):

```powershell
# Add custom hostname binding
az webapp config hostname add `
  --webapp-name fca-auricrux-api `
  --resource-group Auricrux_group `
  --hostname auricrux.futurecontractorsofamerica.com

# Enable managed TLS certificate (free)
az webapp config ssl bind `
  --certificate-thumbprint (az webapp config ssl create --name fca-auricrux-api --resource-group Auricrux_group --hostname auricrux.futurecontractorsofamerica.com --validation-method CNAME --query thumbprint -o tsv) `
  --ssl-type SNI `
  --name fca-auricrux-api `
  --resource-group Auricrux_group
```

Or run the helper script:

```powershell
./scripts/bind_custom_domain.ps1
```

## Step 3 — Verify (promote AUX-020 to PASS only after this passes)

```powershell
./scripts/smoke_prod.ps1 -BaseUrl "https://auricrux.futurecontractorsofamerica.com"
```

All 5 checks must PASS. The scheduled GitHub Actions workflow `.github/workflows/prod-smoke.yml` also probes the custom domain and logs a notice when it becomes reachable.

## App settings already configured

| Setting | Value |
|---------|-------|
| `Auricrux__PublicBaseUrl` | `https://auricrux.futurecontractorsofamerica.com/` |
| `Auricrux__ApiEndpoint` | `https://fca-auricrux-api.azurewebsites.net/` |
| `Cors:AllowedOrigins` | includes custom domain |

## Blocker

**Only the founder can complete Step 1** (Porkbun DNS). Without that CNAME, Azure hostname binding and TLS validation will fail.
