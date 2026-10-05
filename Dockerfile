FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore as its own layer so it is cached until the project file changes.
# Name the project explicitly: the folder also holds a .sln, which makes a bare `dotnet restore/publish` ambiguous.
COPY SmartMosquitoControl.csproj ./
RUN dotnet restore SmartMosquitoControl.csproj

COPY . ./
RUN dotnet publish SmartMosquitoControl.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish ./

# Everything the app writes (SQLite database, data-protection keys, logs) lives in these two directories.
# Mount volumes on them; they are owned by the non-root user the app runs as.
RUN mkdir -p /app/data /app/logs && chown -R $APP_UID /app/data /app/logs
VOLUME ["/app/data", "/app/logs"]

# Non-root users can't bind port 80, so listen on 8080 (the .NET 8+ container default).
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Data__Directory=/app/data
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "SmartMosquitoControl.dll"]
