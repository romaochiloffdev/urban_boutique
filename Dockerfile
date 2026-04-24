# ============================================================
# Urban Boutique — root Dockerfile for Railway / container deploys.
# Build context = repo root, so both this file AND the one inside
# UrbanBoutiqueWeb/ are valid. Railway will pick this one first
# because it sits at the top level.
# ============================================================

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the csproj first for better layer caching.
COPY UrbanBoutiqueWeb/UrbanBoutiqueWeb.csproj ./UrbanBoutiqueWeb/
RUN dotnet restore UrbanBoutiqueWeb/UrbanBoutiqueWeb.csproj

# Copy the rest of the web project and publish.
COPY UrbanBoutiqueWeb/ ./UrbanBoutiqueWeb/
RUN dotnet publish UrbanBoutiqueWeb/UrbanBoutiqueWeb.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

COPY --from=build /app/publish ./

EXPOSE 8080

# Railway injects $PORT at runtime — bind Kestrel to it dynamically.
ENTRYPOINT ["sh", "-c", "export ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} && exec dotnet UrbanBoutiqueWeb.dll"]
