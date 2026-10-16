# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/MatPlay/MatPlay.csproj MatPlay/
RUN dotnet restore MatPlay/MatPlay.csproj
COPY src/MatPlay/ MatPlay/
RUN dotnet publish MatPlay/MatPlay.csproj -c Release -o /app /p:UseAppHost=false

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ARG APP_VERSION=local
ENV APP_VERSION=${APP_VERSION}
ENV MATPLAY_DATA=/data
ENV ASPNETCORE_URLS=http://+:8080

# curl für den HEALTHCHECK (das aspnet-Image bringt keins mit)
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

VOLUME /data
EXPOSE 8080

# Plattform-Health: 200 bei ok/degraded, 503 bei down (ein "degraded" MatPlay wird nicht neu gestartet)
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "MatPlay.dll"]
