FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY "src/" .
RUN dotnet build "./Pwneu.Play/Pwneu.Play.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Pwneu.Play/Pwneu.Play.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
COPY ["scripts/wait-for-it.sh", "/usr/bin/wait-for-it.sh"]
USER root
RUN chmod +x /usr/bin/wait-for-it.sh
USER app
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["sh", "-c", "\
  wait-for-it.sh -t 0 pwneu.mq:5672 && \
  wait-for-it.sh -t 0 pwneu.play.db:5432 && \
  dotnet Pwneu.Play.dll \
"]
