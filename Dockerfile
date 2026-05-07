# --- Stage 1: build the API ----------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS builder

WORKDIR /src

# Restore deps separately so Docker can cache the layer when only source changes
COPY *.csproj ./
RUN dotnet restore

# Build + publish a self-contained release output
COPY . .
RUN dotnet publish -c Release -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# --- Stage 2: lightweight runtime ----------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

WORKDIR /app
COPY --from=builder /app/publish .

# Render-friendly runtime config:
#   • Listen on 0.0.0.0 so the container is reachable from outside
#   • Bind to the PORT env var Render injects (defaults to 8080 locally)
#   • Disable HTTPS redirection — Render terminates SSL at the load balancer
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_NOLOGO=true

EXPOSE 8080

# Health check Render can poll
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:${PORT:-8080}/swagger/v1/swagger.json || exit 1

# Use sh so $PORT expansion happens at runtime, not at image build time
ENTRYPOINT ["sh", "-c", "exec dotnet RealEstateApi.dll --urls http://+:${PORT:-8080}"]
