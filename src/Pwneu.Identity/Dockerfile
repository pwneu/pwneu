﻿FROM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY "src/Pwneu.Identity/Pwneu.Identity.csproj" "./Pwneu.Identity/"
COPY "src/Pwneu.Shared/Pwneu.Shared.csproj" "./Pwneu.Shared/"
RUN dotnet restore "./Pwneu.Identity/Pwneu.Identity.csproj"
COPY "src/Pwneu.Identity" "./Pwneu.Identity"
COPY "src/Pwneu.Shared" "./Pwneu.Shared"
RUN dotnet publish "./Pwneu.Identity/Pwneu.Identity.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt-get update && apt-get install -y curl && apt-get clean && rm -rf /var/lib/apt/lists/*
EXPOSE 8080

FROM base AS final
USER app
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Pwneu.Identity.dll"]