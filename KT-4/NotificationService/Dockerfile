# ── Сборка ────────────────────────────────────────────────────────────────
# Контекст сборки — корень репозитория (нужен проект Contracts и общие .props).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Сначала только файлы проектов: слой restore кэшируется, пока не меняются зависимости.
COPY Directory.Build.props Directory.Packages.props ./
COPY Contracts/Contracts.csproj Contracts/
COPY NotificationService/NotificationService.csproj NotificationService/
RUN dotnet restore NotificationService/NotificationService.csproj

COPY Contracts/ Contracts/
COPY NotificationService/ NotificationService/
RUN dotnet publish NotificationService/NotificationService.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Runtime ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# curl нужен для HEALTHCHECK в docker-compose.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Непривилегированный пользователь из базового образа.
USER $APP_UID
ENTRYPOINT ["dotnet", "NotificationService.dll"]
