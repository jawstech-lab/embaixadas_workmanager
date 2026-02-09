FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_ENV=Production
WORKDIR /src
COPY ["EmbaixadasWorkManager.csproj", "./"]
RUN dotnet restore "EmbaixadasWorkManager.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "EmbaixadasWorkManager.csproj" -c Release -o /app/build

FROM build AS publish
ARG BUILD_ENV=Production
# O appsettings.{Environment}.json será incluído automaticamente no COPY . .
RUN dotnet publish "EmbaixadasWorkManager.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# A variável de ambiente ASPNETCORE_ENVIRONMENT deve ser definida no docker run
# Isso fará o .NET Core carregar o appsettings.Production.json automaticamente
ENTRYPOINT ["dotnet", "EmbaixadasWorkManager.dll"]

