# Docker Build & Deployment Instructions for Auricrux Web Application
# Version: 1.0.0
# Generated: 2026-07-08

## Prerequisites
- Docker Engine 20.10+
- Docker Compose 1.29+ (optional)
- 4GB+ available disk space
- 2GB+ RAM available

## Build Instructions

### Option 1: Local Docker Build

```bash
cd /path/to/auricrux-app

# Build Docker image
docker build -t auricrux/web:1.0.0 .

# Verify build
docker images | grep auricrux

# Test locally
docker run -d \
  --name auricrux-web-test \
  -p 80:80 \
  -p 443:443 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  auricrux/web:1.0.0

# Verify container is running
docker ps | grep auricrux-web-test

# Test endpoint
curl http://localhost/

# Stop test container
docker stop auricrux-web-test
docker rm auricrux-web-test
```

### Option 2: Cloud Build (Google Cloud Build)

```bash
# Using gcloud CLI with provided credentials
gcloud builds submit \
  --config=cloudbuild.yaml \
  --project=calyndra-mobile \
  --substitutions=_DOCKER_REPO=gcr.io/calyndra-mobile/auricrux

# Expected output:
# BUILD SUCCESS
# Image URI: gcr.io/calyndra-mobile/auricrux/web:1.0.0
```

### Option 3: Azure Container Registry

```bash
# Using Azure CLI
az acr build \
  --registry auricruxacr \
  --image auricrux/web:1.0.0 \
  --file Dockerfile \
  .

# Expected output:
# Run successful
# Image: auricruxacr.azurecr.io/auricrux/web:1.0.0
```

## Push to Registry

### Docker Hub (if credentials available)

```bash
docker login --username=auricrux

docker tag auricrux/web:1.0.0 auricrux/web:latest
docker push auricrux/web:1.0.0
docker push auricrux/web:latest

# Verify push
curl https://hub.docker.com/v2/repositories/auricrux/web/tags/?page_size=100
```

### Google Container Registry (GCR)

```bash
# Configure docker auth
gcloud auth configure-docker gcr.io

# Tag image
docker tag auricrux/web:1.0.0 gcr.io/calyndra-mobile/auricrux/web:1.0.0

# Push
docker push gcr.io/calyndra-mobile/auricrux/web:1.0.0

# Make public (if desired)
gsutil iam ch serviceAccount:cloud-build@calyndra-mobile.iam.gserviceaccount.com:objectViewer \
  gs://artifacts.calyndra-mobile.appspot.com/
```

### Azure Container Registry (ACR)

```bash
# Tag image
docker tag auricrux/web:1.0.0 auricruxacr.azurecr.io/auricrux/web:1.0.0

# Push
docker push auricruxacr.azurecr.io/auricrux/web:1.0.0

# Verify
az acr repository list --name auricruxacr
az acr repository show-tags --name auricruxacr --repository auricrux/web
```

## Kubernetes Deployment

### Option 1: Using kubectl

```bash
# Set current namespace
kubectl config set-context --current --namespace=auricrux-prod

# Apply all manifests
kubectl apply -f k8s-deployment.yaml
kubectl apply -f k8s-ingress.yaml

# Verify deployment
kubectl get deployments -n auricrux-prod
kubectl get pods -n auricrux-prod
kubectl get svc -n auricrux-prod

# Check pod status
kubectl describe pod <pod-name> -n auricrux-prod

# View logs
kubectl logs -f deployment/auricrux-web -n auricrux-prod

# Port forward for testing
kubectl port-forward -n auricrux-prod svc/auricrux-web-service 8080:80

# Test
curl http://localhost:8080/
```

### Option 2: Using Helm (if applicable)

```bash
# Add Helm repository
helm repo add auricrux https://charts.futurecontractorsofamerica.com

# Install chart
helm install auricrux-web auricrux/web \
  --namespace auricrux-prod \
  --create-namespace \
  --values values.yaml \
  --version 1.0.0

# Verify
helm list -n auricrux-prod

# Upgrade
helm upgrade auricrux-web auricrux/web \
  --namespace auricrux-prod \
  --values values.yaml
```

## Container Registry Access Details

### Google Cloud (Current Active Account)
- **Project ID**: calyndra-mobile
- **Service Account**: github-actions-ci@calyndra-mobile.iam.gserviceaccount.com
- **Registry**: gcr.io/calyndra-mobile
- **Image Path**: gcr.io/calyndra-mobile/auricrux/web:1.0.0

### Azure Container Registry
- **Registry Name**: auricruxacr
- **URL**: auricruxacr.azurecr.io
- **Image Path**: auricruxacr.azurecr.io/auricrux/web:1.0.0

## Image Specifications

- **Base Image**: mcr.microsoft.com/dotnet/aspnet:10.0-alpine
- **Runtime**: .NET 10.0
- **OS**: Alpine Linux (minimal, ~150-200MB)
- **User**: appuser (non-root)
- **Exposed Ports**: 80 (HTTP), 443 (HTTPS)
- **Health Check**: Enabled, checks http://localhost:80/

## Performance Characteristics

- **Build Time**: ~3-5 minutes (local) / ~1-2 minutes (cloud)
- **Image Size**: ~180-220MB (compressed: ~60-80MB)
- **Memory Usage**: 512Mi-1Gi per container
- **CPU Request**: 250m / Limit: 500m
- **Startup Time**: ~40 seconds

## Troubleshooting

### Build Fails
1. Verify .NET SDK 10.0 is installed
2. Check disk space (need 5GB+)
3. Verify internet connectivity for NuGet package download
4. Review Dockerfile for any custom requirements

### Container Won't Start
1. Check logs: `docker logs <container-id>`
2. Verify ASPNETCORE_URLS environment variable
3. Ensure port 80/443 are not in use
4. Check disk space and memory availability

### Image Too Large
- Already using Alpine Linux (minimal base)
- Consider multi-stage builds (already implemented)
- Remove unused NuGet packages from .csproj

## References

- Dockerfile: `./deployment-packages/Dockerfile`
- K8s Manifests: `./k8s-deployment.yaml` and `./k8s-ingress.yaml`
- Docker Hub: https://hub.docker.com/
- Google Container Registry: https://cloud.google.com/container-registry/docs
- Kubernetes Docs: https://kubernetes.io/docs/
