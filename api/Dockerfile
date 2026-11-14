# Imagem da API Hakari em duas etapas: compila com o SDK do .NET e roda só com o runtime do ASP.NET.
# Usada pelo servidor Linux (infra/config/docker-compose.yml).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primeiro só os .csproj: o restore fica em cache e só roda de novo quando as dependências mudam.
COPY src/Hakari.Domain/Hakari.Domain.csproj src/Hakari.Domain/
COPY src/Hakari.Infrastructure/Hakari.Infrastructure.csproj src/Hakari.Infrastructure/
COPY src/Hakari.Api/Hakari.Api.csproj src/Hakari.Api/
RUN dotnet restore src/Hakari.Api/Hakari.Api.csproj

COPY src/ src/
RUN dotnet publish src/Hakari.Api/Hakari.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# A imagem oficial já traz um usuário comum (APP_UID); a API não roda como root.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Hakari.Api.dll"]
