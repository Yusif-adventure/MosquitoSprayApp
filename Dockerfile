FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore --configfile NuGet.config || true

# copy everything else and publish
COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# set up a non-root user
ENV DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish ./

# Expose default HTTP port
EXPOSE 80

ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "SmartMosquitoControl.dll"]
