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
EXPOSE 8080

FROM base AS final
COPY ["scripts/wait-for-it.sh", "/usr/bin/wait-for-it.sh"]
USER root
RUN chmod +x /usr/bin/wait-for-it.sh
USER app
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["sh", "-c", "\
  wait-for-it.sh -t 0 pwneu.mq:5672 && \
  wait-for-it.sh -t 0 pwneu.play:8080 && \
  wait-for-it.sh -t 0 pwneu.identity:8080 && \
  dotnet Pwneu.Smtp.dll \
"]