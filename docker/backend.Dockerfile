FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY backend/ ./backend/
RUN dotnet restore backend/src/Api/Api.csproj --locked-mode
RUN dotnet publish backend/src/Api/Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
