FROM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY "src/Pwneu.Smtp/Pwneu.Smtp.csproj" "./Pwneu.Smtp/"
COPY "src/Pwneu.Shared/Pwneu.Shared.csproj" "./Pwneu.Shared/"
RUN dotnet restore "./Pwneu.Smtp/Pwneu.Smtp.csproj"
COPY "src/Pwneu.Smtp" "./Pwneu.Smtp"
COPY "src/Pwneu.Shared" "./Pwneu.Shared"
RUN dotnet publish "./Pwneu.Smtp/Pwneu.Smtp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt-get update && apt-get install -y curl && apt-get clean && rm -rf /var/lib/apt/lists/*
EXPOSE 8080

FROM base AS final
USER app
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Pwneu.Smtp.dll"]