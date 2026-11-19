# syntax=docker/dockerfile:1
# Single image: Angular is built to static files and served by the .NET API (one container, one port).

# ---- 1. Build the Angular client ---------------------------------------------
FROM node:22-alpine AS client
WORKDIR /client
COPY client/package.json client/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY client/ ./
RUN npx ng build --configuration production

# ---- 2. Build and publish the .NET API ---------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS api
WORKDIR /src
# Restore first (cached unless the project file changes)
COPY src/VehicleExplorer.Api/VehicleExplorer.Api.csproj src/VehicleExplorer.Api/
RUN dotnet restore src/VehicleExplorer.Api/VehicleExplorer.Api.csproj
COPY src/ src/
RUN dotnet publish src/VehicleExplorer.Api/VehicleExplorer.Api.csproj -c Release -o /app --no-restore

# ---- 3. Runtime -----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app
COPY --from=api /app ./
COPY --from=client /client/dist/client/browser ./wwwroot

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

# Built-in non-root user shipped with the .NET 8+ images
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "VehicleExplorer.Api.dll"]
