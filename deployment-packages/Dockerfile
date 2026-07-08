# Multi-stage Dockerfile for Auricrux.Web Blazor Application

# Stage 1: Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy project files
COPY ["Auricrux.Web/Auricrux.Web.csproj", "Auricrux.Web/"]
COPY ["Auricrux.Shared/Auricrux.Shared.csproj", "Auricrux.Shared/"]

# Restore dependencies
RUN dotnet restore "Auricrux.Web/Auricrux.Web.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/Auricrux.Web"
RUN dotnet build "Auricrux.Web.csproj" -c Release -o /app/build

# Publish the application
RUN dotnet publish "Auricrux.Web.csproj" -c Release -o /app/publish

# Stage 2: Runtime stage (Alpine Linux for minimal image size)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

# Set metadata
LABEL maintainer="michael@futurecontractorsofamerica.com"
LABEL description="Auricrux Web Application - Blazor Server"
LABEL version="1.0.0"

WORKDIR /app

# Copy published files from build stage
COPY --from=build /app/publish .

# Create non-root user for security
RUN addgroup -S appgroup && \
    adduser -S appuser -G appgroup && \
    chown -R appuser:appgroup /app
USER appuser

# Expose ports
EXPOSE 80 443

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:80/health || exit 1

# Environment variables
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Run the application
ENTRYPOINT ["dotnet", "Auricrux.Web.dll"]
