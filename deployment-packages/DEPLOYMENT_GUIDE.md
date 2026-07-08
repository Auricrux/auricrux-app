# Auricrux App Deployment Guide

## Version 1.0.0 - Release Date: July 8, 2026

### Project Overview
Auricrux is a cross-platform .NET application consisting of:
- **Auricrux.Mobile**: MAUI-based mobile application (iOS, Android, Windows, macOS)
- **Auricrux.Web**: Blazor Server web application
- **Auricrux.Shared**: Shared libraries and services

---

## 1. Azure Web App Deployment

### Current Deployment Status: ✅ DEPLOYED

**Deployment Details:**
- **Service**: Azure App Service (fca-bid-saas)
- **Resource Group**: Auricrux_group
- **Region**: Central US
- **Runtime**: .NET 10.0
- **URL**: https://fca-bid-saas-c8cnbhdndhfyg8h0.centralus-01.azurewebsites.net

### Deployment Method:
The web application is deployed using Azure's zip deployment method. The application files are packaged into a zip archive and deployed directly to the Azure App Service.

### Redeployment Instructions:

1. Build the web app:
   ```powershell
   cd Auricrux.Web
   dotnet publish -c Release -o ".\bin\Release\publish"
   ```

2. Create deployment zip:
   ```powershell
   Add-Type -AssemblyName System.IO.Compression.FileSystem
   [System.IO.Compression.ZipFile]::CreateFromDirectory(".\bin\Release\publish", "..\deployment-packages\auricrux-web.zip")
   ```

3. Deploy to Azure:
   ```powershell
   az webapp deploy --resource-group "Auricrux_group" --name "fca-bid-saas" --src-path "..\deployment-packages\auricrux-web.zip" --type "zip"
   ```

---

## 2. Mobile App (MAUI) Deployment

### Current Status: ⚠️ NOT DEPLOYED (Prerequisites Required)

**Prerequisites for Android APK Build:**
- Android SDK (API 21 or higher)
- Java Development Kit (JDK)
- Environment variables:
  - `ANDROID_SDK_ROOT`: Path to Android SDK
  - `JAVA_HOME`: Path to JDK

**Current Blocker:**
Android target is currently commented out in the project file. To enable Android builds:

1. Edit `Auricrux.Mobile\Auricrux.Mobile.csproj`
2. Uncomment the Android target framework line:
   ```xml
   <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">net10.0-android;$(TargetFrameworks)</TargetFrameworks>
   ```

3. Install Android SDK and JDK
4. Set environment variables
5. Build APK:
   ```powershell
   cd Auricrux.Mobile
   dotnet publish -f net10.0-android -c Release
   ```

### APK Signing for Google Play:
1. Generate signing key (if not exists):
   ```bash
   keytool -genkey -v -keystore com.auricrux.app.keystore -keyalg RSA -keysize 2048 -validity 10000 -alias upload
   ```

2. Configure signing in `.csproj`:
   ```xml
   <AndroidKeyStore>true</AndroidKeyStore>
   <AndroidSigningKeyStore>true</AndroidSigningKeyStore>
   <AndroidSigningKeyAlias>upload</AndroidSigningKeyAlias>
   <AndroidSigningKeyPass>[PASSWORD]</AndroidSigningKeyPass>
   <AndroidSigningStorePass>[PASSWORD]</AndroidSigningStorePass>
   ```

3. Build signed APK:
   ```powershell
   dotnet publish -f net10.0-android -c Release
   ```

---

## 3. GitHub Repository

### Status: ✅ REPOSITORY CREATED & CODE PUSHED

**Repository URL**: https://github.com/Auricrux/auricrux-app

**Commit Details:**
- Initial commit: f26d3bc
- Commit message: "Initial Auricrux .NET/C# app - MAUI + Blazor"
- Files: 107 files committed (63,307 insertions)

**To clone the repository:**
```bash
git clone https://github.com/Auricrux/auricrux-app.git
```

---

## 4. Docker Deployment

### Docker Image Creation:

**Dockerfile for Auricrux.Web:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY bin/Release/publish/ .
EXPOSE 80 443
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "Auricrux.Web.dll"]
```

**Build and push Docker image:**
```bash
# Build
docker build -t auricrux/web:1.0.0 -f Dockerfile .

# Tag for container registry
docker tag auricrux/web:1.0.0 [registry]/auricrux/web:1.0.0

# Push
docker push [registry]/auricrux/web:1.0.0
```

**Run Docker container:**
```bash
docker run -p 80:80 -p 443:443 auricrux/web:1.0.0
```

---

## 5. Deployment Checklist

### Pre-Deployment:
- [ ] All tests passing
- [ ] Code review completed
- [ ] Security scanning completed
- [ ] Dependencies updated
- [ ] Environment variables configured

### Deployment:
- [x] Web app published to Azure
- [ ] Mobile app APK built (blocked - requires Android SDK)
- [x] Code pushed to GitHub
- [ ] Docker image built and pushed
- [ ] Release notes published

### Post-Deployment:
- [ ] Health checks passed
- [ ] Smoke tests executed
- [ ] User acceptance testing completed
- [ ] Performance monitoring enabled
- [ ] Backup/rollback plan verified

---

## 6. Environment Configuration

### Azure App Service Settings:
Configure these in Azure Portal > App Settings:

```
ASPNETCORE_ENVIRONMENT = Production
ConnectionString = [Your database connection string]
ApiKey = [Your API keys]
```

### Application Secrets:
Use Azure Key Vault for sensitive data:
```powershell
az keyvault secret set --vault-name "auricrux-vault" --name "ConnectionString" --value "[connection-string]"
```

---

## 7. Monitoring & Logging

### Azure Monitor:
- Application Insights for web app monitoring
- Log Analytics for aggregated logs
- Alerts configured for critical errors

### Dashboards:
- Azure Portal dashboard for deployment status
- Application Insights dashboard for performance metrics

---

## 8. Rollback Procedure

If deployment fails or issues are discovered:

1. **Web App Rollback:**
   ```powershell
   az webapp deployment slot swap --resource-group "Auricrux_group" --name "fca-bid-saas" --slot "staging"
   ```

2. **GitHub Revert:**
   ```bash
   git revert [commit-hash]
   git push origin main
   ```

3. **Docker Rollback:**
   ```bash
   docker run -p 80:80 -p 443:443 auricrux/web:[previous-version]
   ```

---

## Support & Troubleshooting

### Common Issues:

**1. Web App deployment fails:**
- Check Azure App Service logs in Azure Portal
- Verify application settings are correct
- Ensure database connectivity

**2. Mobile app won't build:**
- Verify Android SDK installation
- Check JAVA_HOME environment variable
- Update NuGet packages: `dotnet restore`

**3. Docker image too large:**
- Use multi-stage builds
- Remove unnecessary files
- Use Alpine base image

---

## Release Schedule

- **Development**: Main branch (continuous)
- **Staging**: Staging environment (daily)
- **Production**: Release tagged branches (weekly)

---

## Contact & Support

For deployment questions or issues:
- GitHub Issues: https://github.com/Auricrux/auricrux-app/issues
- Email: michael@futurecontractorsofamerica.com

---

*This deployment guide was generated on July 8, 2026*
