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

WORKDIR /src/Auricrux.Web
RUN dotnet publish "Auricrux.Web.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Runtime (Alpine, minimal image size)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

LABEL maintainer="michael@futurecontractorsofamerica.com"
LABEL description="Auricrux Web Application - Blazor Server construction specialist"
LABEL version="1.1.0"

WORKDIR /app
COPY --from=build /app/publish .
# Ensure product honesty manifest is present for CapabilitiesService (AUX-017/019).
COPY auricrux/system/model_manifest.json /app/auricrux/system/model_manifest.json

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
