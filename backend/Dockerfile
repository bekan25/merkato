# Build the Angular storefront.
FROM node:22-alpine AS frontend-build
WORKDIR /frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build -- --configuration production

# Build and publish the ASP.NET Core 10 API with the Angular application.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
RUN dotnet tool install --global dotnet-ef --version 10.0.14
ENV PATH="$PATH:/root/.dotnet/tools"

COPY backend/Merkato.Domain/Merkato.Domain.csproj backend/Merkato.Domain/
COPY backend/Merkato.Application/Merkato.Application.csproj backend/Merkato.Application/
COPY backend/Merkato.Infrastructure/Merkato.Infrastructure.csproj backend/Merkato.Infrastructure/
COPY backend/Merkato.Api/Merkato.Api.csproj backend/Merkato.Api/
RUN dotnet restore backend/Merkato.Api/Merkato.Api.csproj

COPY backend/ backend/
COPY --from=frontend-build /frontend/dist/markatoo-web/browser/ backend/Merkato.Api/wwwroot/
RUN dotnet publish backend/Merkato.Api/Merkato.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Npgsql/GSSAPI may load the Kerberos GSSAPI shared library while connecting
# to managed PostgreSQL. Install it in the runtime image (not just the SDK stage).
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Merkato.Api.dll"]
