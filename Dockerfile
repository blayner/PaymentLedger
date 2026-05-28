# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from project files only, so the layer is cached until a dependency changes.
COPY global.json Directory.Build.props ./
COPY src/PaymentLedger.Domain/PaymentLedger.Domain.csproj src/PaymentLedger.Domain/
COPY src/PaymentLedger.Application/PaymentLedger.Application.csproj src/PaymentLedger.Application/
COPY src/PaymentLedger.Infrastructure/PaymentLedger.Infrastructure.csproj src/PaymentLedger.Infrastructure/
COPY src/PaymentLedger.Api/PaymentLedger.Api.csproj src/PaymentLedger.Api/
RUN dotnet restore src/PaymentLedger.Api/PaymentLedger.Api.csproj

COPY src/ src/
RUN dotnet publish src/PaymentLedger.Api/PaymentLedger.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# The aspnet image ships a non-root "app" user; never run the ledger as root.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PaymentLedger.Api.dll"]
