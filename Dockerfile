# Build from the backend subdirectory
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY backend/AqlanDentalPro.sln .
COPY backend/src/AqlanDentalPro.Domain/AqlanDentalPro.Domain.csproj src/AqlanDentalPro.Domain/
COPY backend/src/AqlanDentalPro.Application/AqlanDentalPro.Application.csproj src/AqlanDentalPro.Application/
COPY backend/src/AqlanDentalPro.Infrastructure/AqlanDentalPro.Infrastructure.csproj src/AqlanDentalPro.Infrastructure/
COPY backend/src/AqlanDentalPro.API/AqlanDentalPro.API.csproj src/AqlanDentalPro.API/

RUN dotnet restore AqlanDentalPro.sln

COPY backend/ .
RUN dotnet publish src/AqlanDentalPro.API/AqlanDentalPro.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends libicu-dev libfontconfig1 fontconfig \
    fonts-noto-core fonts-noto-extra fonts-freefont-ttf \
    && fc-cache -fv \
    && rm -rf /var/lib/apt/lists/*

RUN addgroup --system --gid 1001 appgroup && adduser --system --uid 1001 --ingroup appgroup appuser
RUN mkdir -p wwwroot/uploads /data/uploads ai-models && chown -R appuser:appgroup wwwroot /data/uploads ai-models

COPY --from=build --chown=appuser:appgroup /app/publish .

USER appuser
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "AqlanDentalPro.API.dll"]
