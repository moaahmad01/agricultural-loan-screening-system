# syntax=docker/dockerfile:1
FROM node:24-slim AS frontend-build
WORKDIR /src
COPY AgriculturalLoanSystem/package.json AgriculturalLoanSystem/package-lock.json ./
RUN npm ci
COPY AgriculturalLoanSystem/tailwind.config.js ./
COPY AgriculturalLoanSystem/scripts ./scripts
COPY AgriculturalLoanSystem/Views ./Views
COPY AgriculturalLoanSystem/wwwroot ./wwwroot
RUN npm run build:css

FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src
COPY AgriculturalLoanSystem/AgriculturalLoanSystem.csproj AgriculturalLoanSystem/packages.lock.json ./
RUN dotnet restore AgriculturalLoanSystem.csproj --locked-mode
COPY AgriculturalLoanSystem/ ./
COPY --from=frontend-build /src/wwwroot ./wwwroot
RUN dotnet publish AgriculturalLoanSystem.csproj -c Release -o /app/publish --no-restore

# Explicit, one-shot database migration step.
FROM build AS migrator
RUN dotnet tool install --global dotnet-ef --version 10.0.13
ENV PATH="$PATH:/root/.dotnet/tools"
ENTRYPOINT ["dotnet", "ef", "database", "update", "--configuration", "Release", "--no-build"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS final
WORKDIR /app
# Preserve UID/GID 1000 for private-data volumes created by the .NET 9 image.
RUN usermod --login appuser --groups "" ubuntu \
    && groupmod --new-name appuser ubuntu \
    && mkdir -p /app/App_Data/Documents /app/App_Data/Keys \
    && chown -R appuser:appuser /app/App_Data
COPY --from=build /app/publish .
USER appuser
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AgriculturalLoanSystem.dll"]
