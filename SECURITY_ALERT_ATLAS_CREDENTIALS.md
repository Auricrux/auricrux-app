# SECURITY ALERT: Atlas Credentials Exposure

**Date**: August 28, 2026  
**Severity**: CRITICAL  
**Status**: CREDENTIALS REMOVED FROM CODE

## Issue

MongoDB Atlas connection string with embedded credentials was committed to the repository in:
- `Auricrux.Web/appsettings.json`

**Exposed credentials**:
- Username: `michael@futurecontractorsofamerica.com`
- Password: `MyGodiswithme01!`
- Cluster: `auricrux-prod.plzuwk.mongodb.net`

## Immediate Actions Required

### 1. Rotate Atlas Password (URGENT)

**Access MongoDB Atlas Console**:
1. Log in to https://cloud.mongodb.com
2. Navigate to Database Access
3. Find user `michael@futurecontractorsofamerica.com`
4. Click "Edit" → "Edit Password"
5. Generate a strong random password (use password manager)
6. Save the new password securely

### 2. Update GitHub Secrets

Add the new connection string as a GitHub secret:

1. Go to https://github.com/FCA-Ecosystem/auricrux-app/settings/secrets/actions
2. Click "New repository secret"
3. Name: `ATLAS_CONNECTION_STRING`
4. Value: `mongodb+srv://michael%40futurecontractorsofamerica.com:<NEW_PASSWORD>@auricrux-prod.plzuwk.mongodb.net/auricrux?appName=auricrux-prod`
5. Click "Add secret"

### 3. Update Deployment Workflows

GitHub Actions workflows already configured to use secrets. Verify:
- `.github/workflows/*.yml` files use `${{ secrets.ATLAS_CONNECTION_STRING }}`

### 4. Update Production Environment Variables

For Oracle Cloud VM deployment:
```bash
export Atlas__ConnectionString="mongodb+srv://michael%40futurecontractorsofamerica.com:<NEW_PASSWORD>@auricrux-prod.plzuwk.mongodb.net/auricrux?appName=auricrux-prod"
```

Add to systemd service file or container environment.

### 5. Audit Git History

**Check if credentials were previously valid**:
```bash
cd /workspace/auricrux-app
git log --all --full-history -- Auricrux.Web/appsettings.json
```

If the exposed password was ever used in production, assume it's compromised and rotate immediately.

## Prevention Measures

### Local Development

Create `appsettings.Development.json` (gitignored):
```json
{
  "Atlas": {
    "ConnectionString": "mongodb+srv://...",
    "Database": "auricrux"
  }
}
```

### Production Configuration

Always use environment variables:
- Docker: Pass via `-e` flag or `docker-compose.yml` env_file
- Kubernetes: Use ConfigMap or Secret
- Oracle Cloud VM: Export in shell or systemd service
- GitHub Actions: Use repository secrets

### Code Review Checklist

- [ ] No credentials in `appsettings*.json`
- [ ] No API keys in source code
- [ ] All secrets use environment variables
- [ ] `.env` files are gitignored

## Status

- [x] Credentials removed from code
- [ ] **PENDING**: Atlas password rotation (requires Atlas console access)
- [ ] **PENDING**: GitHub secret updated
- [ ] **PENDING**: Production environment updated
- [ ] **PENDING**: Git history audit

## Next Steps

1. **IMMEDIATELY**: Rotate Atlas password
2. Store new connection string in GitHub Secrets
3. Update production deployment
4. Test connection with new credentials
5. Delete this alert file after remediation complete
