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

VOLUME /data
EXPOSE 8080

ENTRYPOINT ["dotnet", "MatPlay.dll"]
