# ============================================================
# Urban Boutique Web — Railway / container-friendly Dockerfile
# ============================================================

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for better layer caching
COPY UrbanBoutiqueWeb.csproj ./
RUN dotnet restore UrbanBoutiqueWeb.csproj

# Copy the rest and publish
COPY . ./
RUN dotnet publish UrbanBoutiqueWeb.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Railway provides $PORT at runtime; Kestrel must listen on 0.0.0.0:$PORT.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

COPY --from=build /app/publish ./

EXPOSE 8080

# Allow Railway to override the port at runtime without rebuilding.
# Kestrel picks up ASPNETCORE_URLS, which we rewrite here if PORT is set.
ENTRYPOINT ["sh", "-c", "export ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} && exec dotnet UrbanBoutiqueWeb.dll"]
