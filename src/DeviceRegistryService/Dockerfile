# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/DeviceRegistryService/DeviceRegistryService.csproj", "DeviceRegistryService/"]
COPY ["src/SharedLibrary/SharedLibrary.csproj", "SharedLibrary/"]
RUN dotnet restore "DeviceRegistryService/DeviceRegistryService.csproj"

# Copy everything else and build
COPY src/DeviceRegistryService/ DeviceRegistryService/
COPY src/SharedLibrary/ SharedLibrary/
WORKDIR "/src/DeviceRegistryService"
RUN dotnet build "DeviceRegistryService.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "DeviceRegistryService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DeviceRegistryService.dll"]
