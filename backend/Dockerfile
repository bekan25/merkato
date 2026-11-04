# Build the Angular storefront.
FROM node:20-alpine AS frontend-build
WORKDIR /frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build -- --configuration production

# Build the ASP.NET Core API and package the Angular app into wwwroot.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
RUN dotnet tool install --global dotnet-ef --version 8.0.8
ENV PATH="$PATH:/root/.dotnet/tools"

COPY backend/Merkato.Domain/Merkato.Domain.csproj backend/Merkato.Domain/
COPY backend/Merkato.Application/Merkato.Application.csproj backend/Merkato.Application/
COPY backend/Merkato.Infrastructure/Merkato.Infrastructure.csproj backend/Merkato.Infrastructure/
COPY backend/Merkato.Api/Merkato.Api.csproj backend/Merkato.Api/
RUN dotnet restore backend/Merkato.Api/Merkato.Api.csproj

COPY backend/ backend/
COPY --from=frontend-build /frontend/dist/markatoo-web/browser/ backend/Merkato.Api/wwwroot/
RUN dotnet publish backend/Merkato.Api/Merkato.Api.csproj -c Release -o /app/publish --no-restore

# Small ASP.NET runtime image; the app serves both UI and API on one origin.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Merkato.Api.dll"]
