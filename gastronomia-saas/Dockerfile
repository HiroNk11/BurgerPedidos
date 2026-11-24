# syntax=docker/dockerfile:1
FROM node:22-alpine AS panel
WORKDIR /web/admin
COPY admin/package.json admin/package-lock.json ./
RUN npm ci
COPY admin/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props ./
COPY src/Gastronomia.Domain/Gastronomia.Domain.csproj src/Gastronomia.Domain/
COPY src/Gastronomia.Application/Gastronomia.Application.csproj src/Gastronomia.Application/
COPY src/Gastronomia.Infrastructure/Gastronomia.Infrastructure.csproj src/Gastronomia.Infrastructure/
COPY src/Gastronomia.Api/Gastronomia.Api.csproj src/Gastronomia.Api/
COPY tools/Gastronomia.Migrate/Gastronomia.Migrate.csproj tools/Gastronomia.Migrate/
RUN dotnet restore src/Gastronomia.Api/Gastronomia.Api.csproj && dotnet restore tools/Gastronomia.Migrate/Gastronomia.Migrate.csproj
COPY src/ src/
COPY tools/Gastronomia.Migrate/ tools/Gastronomia.Migrate/
RUN dotnet publish src/Gastronomia.Api/Gastronomia.Api.csproj -c Release --no-restore -o /out/api /p:UseAppHost=false && dotnet publish tools/Gastronomia.Migrate/Gastronomia.Migrate.csproj -c Release --no-restore -o /out/migrate /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* && mkdir -p /app/App_Data && chown "$APP_UID:$APP_UID" /app/App_Data
COPY --from=build /out/api/ ./
COPY --from=build /out/migrate/ /migrator/
COPY --from=panel /web/src/Gastronomia.Api/wwwroot/admin/ ./wwwroot/admin/
COPY sql/ ./sql/
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gastronomia.Api.dll"]
