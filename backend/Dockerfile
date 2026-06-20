# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first (layer caching for NuGet restore)
COPY AqlanDentalPro.sln .
COPY src/AqlanDentalPro.Domain/AqlanDentalPro.Domain.csproj src/AqlanDentalPro.Domain/
COPY src/AqlanDentalPro.Application/AqlanDentalPro.Application.csproj src/AqlanDentalPro.Application/
COPY src/AqlanDentalPro.Infrastructure/AqlanDentalPro.Infrastructure.csproj src/AqlanDentalPro.Infrastructure/
COPY src/AqlanDentalPro.API/AqlanDentalPro.API.csproj src/AqlanDentalPro.API/
COPY tests/AqlanDentalPro.UnitTests/AqlanDentalPro.UnitTests.csproj tests/AqlanDentalPro.UnitTests/
COPY tests/AqlanDentalPro.IntegrationTests/AqlanDentalPro.IntegrationTests.csproj tests/AqlanDentalPro.IntegrationTests/

RUN dotnet restore AqlanDentalPro.sln

# Copy full source and publish
COPY . .
RUN dotnet publish src/AqlanDentalPro.API/AqlanDentalPro.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Verify Arabic fonts are included in publish output
RUN ls -la /app/publish/Fonts/ && \
    test -f /app/publish/Fonts/NotoNaskhArabic-Regular.ttf || \
    (echo "ERROR: NotoNaskhArabic-Regular.ttf not found in publish output!" && exit 1) && \
    test -f /app/publish/Fonts/NotoNaskhArabic-Bold.ttf || \
    (echo "WARNING: NotoNaskhArabic-Bold.ttf not found in publish output (non-fatal)")

# ---- Runtime Stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install fontconfig for QuestPDF font rendering at runtime
RUN apt-get update && apt-get install -y --no-install-recommends libfontconfig1 fontconfig && rm -rf /var/lib/apt/lists/*

# Non-root user for security
RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup appuser

COPY --from=build --chown=appuser:appgroup /app/publish .

# Pre-create uploads directories with write permissions for non-root user
# /app/wwwroot/uploads — default path (no UPLOADS_PATH env var)
# /data/uploads — recommended path for Railway persistent volume mount
RUN mkdir -p /app/wwwroot/uploads /data/uploads && \
    chown appuser:appgroup /app/wwwroot/uploads /data/uploads

USER appuser

EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "AqlanDentalPro.API.dll"]
