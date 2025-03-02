FROM mcr.microsoft.com/dotnet/sdk:9.0 AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY "src/Pwneu.Api/Pwneu.Api.csproj" "./Pwneu.Api/"
COPY "src/Pwneu.ServiceDefaults/Pwneu.ServiceDefaults.csproj" "./Pwneu.ServiceDefaults/"
RUN dotnet restore "./Pwneu.Api/Pwneu.Api.csproj"

COPY "src/Pwneu.Api" "./Pwneu.Api/"
COPY "src/Pwneu.ServiceDefaults" "./Pwneu.ServiceDefaults/"
RUN dotnet publish "./Pwneu.Api/Pwneu.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
RUN apt-get update && apt-get install -y curl && apt-get clean && rm -rf /var/lib/apt/lists/*
EXPOSE 8080

FROM base AS final
USER app
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Pwneu.Api.dll"]
