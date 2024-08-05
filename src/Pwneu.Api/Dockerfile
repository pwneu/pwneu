FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
EXPOSE 7638
EXPOSE 7639

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Pwneu.Api/Pwneu.Api.csproj", "./Pwneu.Api/"]
RUN dotnet restore "./Pwneu.Api/Pwneu.Api.csproj"
COPY "src/" .
WORKDIR "/src/Pwneu.Api"
RUN dotnet build "./Pwneu.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Pwneu.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY ["scripts/wait-for-it.sh", "/usr/bin/wait-for-it.sh"]
USER root
RUN chmod +x /usr/bin/wait-for-it.sh
USER app
COPY --from=publish /app/publish .
ENTRYPOINT ["wait-for-it.sh", "-t", "0", "pwneu.db:5432", "--", "dotnet", "Pwneu.Api.dll"]