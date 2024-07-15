FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 7638
EXPOSE 7639

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
RUN mkdir /db
COPY ["src/Pwneu.Api/Pwneu.Api.csproj", "./Pwneu.Api/"]
RUN dotnet restore "./src/Pwneu.Api/Pwneu.Api.csproj"
COPY "src/" .
WORKDIR "/src/Pwneu.Api"
RUN dotnet build "./Pwneu.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Pwneu.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Pwneu.Api.dll"]