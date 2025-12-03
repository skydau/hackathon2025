# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/TenantCatalogService/TenantCatalogService.csproj", "TenantCatalogService/"]
COPY ["src/SharedLibrary/SharedLibrary.csproj", "SharedLibrary/"]
RUN dotnet restore "TenantCatalogService/TenantCatalogService.csproj"

# Copy everything else and build
COPY src/TenantCatalogService/ TenantCatalogService/
COPY src/SharedLibrary/ SharedLibrary/
WORKDIR "/src/TenantCatalogService"
RUN dotnet build "TenantCatalogService.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "TenantCatalogService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TenantCatalogService.dll"]
