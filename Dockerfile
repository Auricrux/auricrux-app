# Multi-stage Dockerfile for Auricrux.Web (Blazor Server construction-specialist app).
# Build from the repository root: docker build -t auricrux/web:1.0.0 .

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "global.json", "./"]
COPY ["Auricrux.Web/Auricrux.Web.csproj", "Auricrux.Web/"]
COPY ["Auricrux.Shared/Auricrux.Shared.csproj", "Auricrux.Shared/"]
RUN dotnet restore "Auricrux.Web/Auricrux.Web.csproj"

COPY Auricrux.Web/ Auricrux.Web/
COPY Auricrux.Shared/ Auricrux.Shared/
# Stamp + manifest available to MSBuild Content Include during publish (Linux skips PowerShell stamp target).
COPY auricrux/system/package_stamp.json auricrux/system/package_stamp.json
COPY auricrux/system/model_manifest.json auricrux/system/model_manifest.json

WORKDIR /src/Auricrux.Web
RUN BUILD_UTC="$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
 && CORPUS_SHA="$(sha256sum /src/Auricrux.Web/Data/construction-corpus.json | awk '{print $1}')" \
 && sed -i "s/\"buildTimestampUtc\"[[:space:]]*:[[:space:]]*\"[^\"]*\"/\"buildTimestampUtc\": \"${BUILD_UTC}\"/" \
      /src/auricrux/system/package_stamp.json \
 && if grep -q '"corpusSha256"' /src/auricrux/system/package_stamp.json; then \
      sed -i "s/\"corpusSha256\"[[:space:]]*:[[:space:]]*\"[^\"]*\"/\"corpusSha256\": \"${CORPUS_SHA}\"/" /src/auricrux/system/package_stamp.json; \
    else \
      sed -i "s/\"deploymentSource\"/\"corpusSha256\": \"${CORPUS_SHA}\",\"deploymentSource\"/" /src/auricrux/system/package_stamp.json; \
    fi \
 && dotnet publish "Auricrux.Web.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Runtime (Alpine, minimal image size)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

LABEL maintainer="michael@futurecontractorsofamerica.com"
LABEL description="Auricrux Web Application - Blazor Server construction specialist"
LABEL version="1.1.0"

WORKDIR /app
COPY --from=build /app/publish .
# Ensure product honesty manifest + package identity stamp are present on host.
COPY auricrux/system/model_manifest.json /app/auricrux/system/model_manifest.json
COPY auricrux/system/package_stamp.json /app/auricrux/system/package_stamp.json
# Refresh build timestamp so operators can tell which image build is live.
RUN mkdir -p /app/auricrux/system /app/Data \
 && BUILD_UTC="$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
 && CORPUS_SHA="$(sha256sum /app/Data/construction-corpus.json | awk '{print $1}')" \
 && sed -i "s/\"buildTimestampUtc\"[[:space:]]*:[[:space:]]*\"[^\"]*\"/\"buildTimestampUtc\": \"${BUILD_UTC}\"/" \
      /app/auricrux/system/package_stamp.json \
 && if grep -q '"corpusSha256"' /app/auricrux/system/package_stamp.json; then \
      sed -i "s/\"corpusSha256\"[[:space:]]*:[[:space:]]*\"[^\"]*\"/\"corpusSha256\": \"${CORPUS_SHA}\"/" /app/auricrux/system/package_stamp.json; \
    else \
      sed -i "s/\"deploymentSource\"/\"corpusSha256\": \"${CORPUS_SHA}\",\"deploymentSource\"/" /app/auricrux/system/package_stamp.json; \
    fi \
 && cp /app/auricrux/system/package_stamp.json /app/Data/package_stamp.json

RUN addgroup -S appgroup \
    && adduser -S appuser -G appgroup \
    && mkdir -p /app/Data/media /app/Data/workspace /app/Data/memory /app/Data/accounts \
    && chown -R appuser:appgroup /app
USER appuser

EXPOSE 80 443

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:80/health || exit 1

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "Auricrux.Web.dll"]
