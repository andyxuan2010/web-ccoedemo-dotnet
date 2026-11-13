FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["NuGet.config", "./"]
COPY ["WebCcoeDemo.Web.csproj", "./"]
RUN dotnet restore "WebCcoeDemo.Web.csproj" --configfile NuGet.config

COPY . .
RUN dotnet publish "WebCcoeDemo.Web.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "WebCcoeDemo.Web.dll"]
