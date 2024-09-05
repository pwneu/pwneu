FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY "src/" .
RUN dotnet build "./Pwneu.Gateway/Pwneu.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Pwneu.Gateway/Pwneu.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
COPY ["scripts/wait-for-it.sh", "/usr/bin/wait-for-it.sh"]
USER root
RUN chmod +x /usr/bin/wait-for-it.sh
USER app
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["/bin/sh", "-c", "\
  wait-for-it.sh -t 0 pwneu.mq:5672 && \
  wait-for-it.sh -t 0 pwneu.play:8081 && \
  wait-for-it.sh -t 0 pwneu.identity:8081 && \
  wait-for-it.sh -t 0 pwneu.chat:8081 && \
  dotnet Pwneu.Gateway.dll \
"]