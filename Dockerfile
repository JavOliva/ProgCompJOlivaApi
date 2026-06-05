# See https://aka.ms/customizecontainer to learn how to customize your debug container
# and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["ProgCompJOlivaApi/ProgCompJOlivaApi.csproj", "ProgCompJOlivaApi/"]
RUN dotnet restore "./ProgCompJOlivaApi/ProgCompJOlivaApi.csproj"

COPY . .
WORKDIR "/src/ProgCompJOlivaApi"
RUN dotnet build "./ProgCompJOlivaApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ProgCompJOlivaApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .
# Crear carpeta para uploads y dar permisos antes de cambiar de usuario
USER root
RUN mkdir -p /app/wwwroot/organizations/logos \
    && chown -R $APP_UID:0 /app/wwwroot \
    && chmod -R 775 /app/wwwroot
USER $APP_UID

ENTRYPOINT ["dotnet", "ProgCompJOlivaApi.dll"]