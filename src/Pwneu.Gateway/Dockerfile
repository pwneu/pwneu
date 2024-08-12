FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY "src/" .
RUN dotnet restore "./Pwneu.Gateway/Pwneu.Gateway.csproj"
RUN dotnet build "./Pwneu.Gateway/Pwneu.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Pwneu.Gateway/Pwneu.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["sh", "-c", "wait-for-it.sh -t 0 pwneu.api:8081 && wait-for-it.sh -t 0 pwneu.mq:5672 && dotnet Pwneu.Gateway.dll"]