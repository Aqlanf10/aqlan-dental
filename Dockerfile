# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first (layer caching for NuGet restore)
COPY backend/AqlanDentalPro.sln .
COPY backend/src/AqlanDentalPro.Domain/AqlanDentalPro.Domain.csproj src/AqlanDentalPro.Domain/
COPY backend/src/AqlanDentalPro.Application/AqlanDentalPro.Application.csproj src/AqlanDentalPro.Application/
COPY backend/src/AqlanDentalPro.Infrastructure/AqlanDentalPro.Infrastructure.csproj src/AqlanDentalPro.Infrastructure/
COPY backend/src/AqlanDentalPro.API/AqlanDentalPro.API.csproj src/AqlanDentalPro.API/

RUN dotnet restore AqlanDentalPro.sln

# Copy full source and publish
COPY backend/ .
RUN dotnet publish src/AqlanDentalPro.API/AqlanDentalPro.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---- Runtime Stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install necessary dependencies for ONNX Runtime + ICU (Arabic text support)
RUN apt-get update && apt-get install -y --no-install-recommends \
    libicu-dev \
    && rm -rf /var/lib/apt/lists/*

# Non-root user for security
RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup appuser

# Create uploads directory
RUN mkdir -p wwwroot/uploads ai-models && chown -R appuser:appgroup wwwroot ai-models

COPY --from=build --chown=appuser:appgroup /app/publish .

USER appuser

EXPOSE 8080

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "AqlanDentalPro.API.dll"]
