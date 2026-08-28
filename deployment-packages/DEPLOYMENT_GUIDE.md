# Auricrux App Deployment Guide

## Version 1.3.0 - Updated: August 28, 2026

### Project Overview
Auricrux is a Construction Intelligence Platform consisting of:
- **Auricrux.Web**: Blazor Server web application (.NET 10)
  - Full learning loop (Phases 6-10)
  - Predictive intelligence transfer (Phase 9A)
  - Observability dashboard (Phase 9B)
- **Auricrux.Mobile**: MAUI-based mobile application
- **Auricrux.Shared**: Shared libraries and services

**Architecture**: Integrated intelligence layer for FCA Construction Operating System

---

## 1. Oracle Cloud VM Deployment (PRIMARY)

### Current Deployment Status: ✅ LIVE

**Deployment Details:**
- **Platform**: Oracle Cloud Infrastructure (OCI)
- **URL**: https://auricrux.futurecontractorsofamerica.com
- **IP**: 150.136.115.97
- **Runtime**: .NET 10.0 on Linux
- **Database**: MongoDB Atlas (shared with FCA ecosystem)

### Prerequisites

1. **Oracle Cloud VM Access**
   - SSH key configured
   - Firewall rules: ports 80, 443 open
   - Systemd for service management

2. **Required Services**
   - Ollama (running on port 11434)
   - MongoDB Atlas connection
   - .NET 10 runtime

### Deployment Steps

1. **Build the application**:
   ```bash
   cd Auricrux.Web
   dotnet publish -c Release -o ./publish
   ```

2. **Transfer to Oracle VM**:
   ```bash
   scp -r ./publish/* user@150.136.115.97:/opt/auricrux/
   ```

3. **Configure environment variables** (on VM):
   ```bash
   # Create environment file
   sudo nano /etc/auricrux/environment
   
   # Add these variables:
   export ASPNETCORE_ENVIRONMENT=Production
   export Atlas__ConnectionString="mongodb+srv://..."
   export Atlas__Database="auricrux"
   export Auricrux__OllamaUrl="http://127.0.0.1:11434"
   export FcaEcosystem__ApiBaseUrl="https://futurecontractorsofamerica.com/api"
   ```

4. **Create systemd service**:
   ```bash
   sudo nano /etc/systemd/system/auricrux.service
   ```
   
   ```ini
   [Unit]
   Description=Auricrux Intelligence Platform
   After=network.target
   
   [Service]
   Type=notify
   WorkingDirectory=/opt/auricrux
   ExecStart=/usr/bin/dotnet /opt/auricrux/Auricrux.Web.dll
   EnvironmentFile=/etc/auricrux/environment
   Restart=always
   RestartSec=10
   KillSignal=SIGINT
   SyslogIdentifier=auricrux
   User=auricrux
   
   [Install]
   WantedBy=multi-user.target
   ```

5. **Start the service**:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable auricrux
   sudo systemctl start auricrux
   sudo systemctl status auricrux
   ```

6. **Verify deployment**:
   ```bash
   curl http://localhost:5080/api/health
   ```

### Automated Deployment Script

See `/tmp/oracle-deploy.sh` for automated deployment script.

---

## 2. Environment Configuration (CRITICAL)

### Security Best Practices

**⚠️ NEVER commit secrets to git!**

All sensitive configuration must use environment variables:

### Required Environment Variables

```bash
# MongoDB Atlas (REQUIRED)
Atlas__ConnectionString="mongodb+srv://[user]:[password]@[cluster]/[database]"
Atlas__Database="auricrux"

# Ollama (REQUIRED)
Auricrux__OllamaUrl="http://127.0.0.1:11434"

# FCA Ecosystem Integration (REQUIRED for Phase 8+)
FcaEcosystem__ApiBaseUrl="https://futurecontractorsofamerica.com/api"

# Optional
Auth__Enabled="false"
Auth__Authority=""
Auth__Audience="auricrux"
```

### Local Development

Create `appsettings.Development.json` (gitignored):
```json
{
  "Atlas": {
    "ConnectionString": "mongodb+srv://[your-dev-credentials]",
    "Database": "auricrux-dev"
  }
}
```

### GitHub Actions Secrets

Configure these in repository settings:
1. Go to: https://github.com/FCA-Ecosystem/auricrux-app/settings/secrets/actions
2. Add:
   - `ATLAS_CONNECTION_STRING`: MongoDB connection string
   - `ORACLE_SSH_KEY`: SSH private key for VM access
   - `ORACLE_VM_HOST`: VM hostname or IP

### Docker Deployment

```bash
docker run -d \
  -p 80:80 \
  -e Atlas__ConnectionString="mongodb+srv://..." \
  -e Atlas__Database="auricrux" \
  -e Auricrux__OllamaUrl="http://ollama:11434" \
  --name auricrux-web \
  auricrux/web:1.3.0
```

---

## 3. Docker Deployment (Alternative)

### Build Docker Image

Using the provided [`Dockerfile`](../Dockerfile):

```bash
cd /workspace/auricrux-app
docker build -t auricrux/web:1.3.0 .
```

### Docker Compose

Using the provided [`docker-compose.yml`](../docker-compose.yml):

```bash
# Start all services (Ollama + Auricrux Web)
docker-compose up -d

# Check logs
docker-compose logs -f auricrux-web

# Stop services
docker-compose down
```

### Environment Variables in Docker

Create `.env` file (gitignored):
```env
ATLAS_CONNECTION_STRING=mongodb+srv://...
ATLAS_DATABASE=auricrux
OLLAMA_URL=http://ollama:11434
FCA_API_URL=https://futurecontractorsofamerica.com/api
```

---

## 4. Kubernetes Deployment (Advanced)

### Deploy to Kubernetes

Using the provided manifests [`k8s-deployment.yaml`](../k8s-deployment.yaml):

```bash
# Create namespace
kubectl create namespace auricrux

# Create secret for Atlas connection
kubectl create secret generic auricrux-secrets \
  --from-literal=atlas-connection="mongodb+srv://..." \
  --namespace=auricrux

# Apply deployment
kubectl apply -f k8s-deployment.yaml
kubectl apply -f k8s-ingress.yaml

# Check status
kubectl get pods -n auricrux
kubectl logs -f deployment/auricrux-web -n auricrux
```

---

## 5. GitHub Actions CI/CD

### Automated Workflows

The repository includes these workflows:

- `.github/workflows/dotnet-test.yml` - Run tests on push
- `.github/workflows/docker-build.yml` - Build Docker images
- `.github/workflows/prod-smoke.yml` - Smoke tests after deployment

### Manual Deployment Trigger

```bash
# Trigger deployment workflow
gh workflow run deploy-to-production \
  --ref main \
  --field environment=production
```

---

## 6. Database Configuration

### MongoDB Atlas Setup

1. **Create cluster** (if not exists):
   - Login to https://cloud.mongodb.com
   - Create M10+ cluster (production)
   - Enable MongoDB 7.0+

2. **Create database user**:
   - Database Access → Add New Database User
   - Username: `auricrux-app`
   - Password: Generate strong password
   - Database User Privileges: `readWrite` on `auricrux` database

3. **Network Access**:
   - Add Oracle Cloud VM IP to whitelist
   - Or use `0.0.0.0/0` with strong authentication

4. **Collections** (created automatically):
   - `corpus` - RAG knowledge base
   - `conversation_memory` - Chat history
   - `feedback` - User feedback
   - `interactions` - User interactions
   - `construction_events` - Field events
   - `construction_outcomes` - Outcome tracking
   - `learning_recommendations` - AI recommendations
   - `audit_trail` - Provenance tracking
   - `fca_entity_cache` - FCA API cache

---

## 7. Monitoring & Health Checks

### Health Check Endpoints

```bash
# Main health check
curl https://auricrux.futurecontractorsofamerica.com/api/health

# Atlas connection status
curl https://auricrux.futurecontractorsofamerica.com/api/knowledge/health

# Predictive intelligence status
curl https://auricrux.futurecontractorsofamerica.com/api/predictive/health

# Dashboard health
curl https://auricrux.futurecontractorsofamerica.com/api/intelligence/dashboard/health
```

### Monitoring Services

- **Logs**: `journalctl -u auricrux -f` (systemd)
- **Atlas**: MongoDB Atlas monitoring dashboard
- **Ollama**: Check Ollama service status
- **Metrics**: Phase 9B Intelligence Dashboard at `/intelligence`

---

## 8. Rollback Procedure

### Systemd Rollback

```bash
# Stop current version
sudo systemctl stop auricrux

# Restore previous version
sudo cp -r /opt/auricrux-backup-[date]/* /opt/auricrux/

# Restart
sudo systemctl start auricrux
```

### Git Rollback

```bash
# Revert to previous commit
git revert [commit-hash]
git push origin main

# Or reset to previous version
git reset --hard [previous-commit]
git push --force origin main  # Use with caution!
```

### Docker Rollback

```bash
# Stop current container
docker stop auricrux-web

# Start previous version
docker run -d \
  -p 80:80 \
  -e Atlas__ConnectionString="..." \
  --name auricrux-web \
  auricrux/web:1.2.0  # Previous version
```

---

## 9. Deployment Checklist

### Pre-Deployment
- [ ] All tests passing (`dotnet test`)
- [ ] Code review completed
- [ ] Security scan completed
- [ ] Environment variables configured
- [ ] Atlas connection tested
- [ ] Ollama service running

### Deployment
- [ ] Build published successfully
- [ ] Transferred to production server
- [ ] Service restarted
- [ ] Health checks passing
- [ ] Atlas connection verified

### Post-Deployment
- [ ] Smoke tests executed
- [ ] Intelligence dashboard accessible at `/intelligence`
- [ ] Predictive intelligence working
- [ ] Learning loop processing events
- [ ] No errors in logs

---

## 10. Troubleshooting

### Common Issues

**1. Atlas connection fails:**
```bash
# Check connection string format
# Ensure password is URL-encoded
# Verify network access in Atlas console
# Test with mongo shell: mongosh "mongodb+srv://..."
```

**2. Ollama not responding:**
```bash
# Check Ollama service
sudo systemctl status ollama

# Verify Ollama is listening
curl http://127.0.0.1:11434/api/tags

# Restart if needed
sudo systemctl restart ollama
```

**3. Service won't start:**
```bash
# Check logs
journalctl -u auricrux -n 50

# Check permissions
ls -la /opt/auricrux

# Verify .NET runtime
dotnet --info
```

**4. High memory usage:**
```bash
# Ollama models can use significant RAM
# Check memory
free -h

# Consider smaller models or increase VM memory
```

---

## 11. Security Hardening

### Firewall Configuration

```bash
# Allow only necessary ports
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 80/tcp    # HTTP
sudo ufw allow 443/tcp   # HTTPS
sudo ufw enable
```

### SSL/TLS Configuration

```bash
# Install certbot
sudo apt install certbot

# Obtain certificate
sudo certbot certonly --standalone -d auricrux.futurecontractorsofamerica.com

# Configure nginx as reverse proxy with SSL
sudo nano /etc/nginx/sites-available/auricrux
```

### Regular Updates

```bash
# Update system packages
sudo apt update && sudo apt upgrade

# Update .NET runtime
# Update Ollama models
ollama pull auricrux-fca
```

---

## Contact & Support

For deployment questions or issues:
- **GitHub**: https://github.com/FCA-Ecosystem/auricrux-app/issues
- **Email**: michael@futurecontractorsofamerica.com
- **Docs**: See AGENTS.md for operational details

---

*Last Updated: August 28, 2026 - Version 1.3.0*
*Reflects Phase 6-10 learning loop, Phase 9A predictive intelligence, Phase 9B observability dashboard*
