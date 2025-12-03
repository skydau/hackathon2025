FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/MedLogicService/MedLogicService.csproj", "src/MedLogicService/"]
COPY ["src/TenantDBRouter/TenantDBRouter.csproj", "src/TenantDBRouter/"]

# Restore dependencies
RUN dotnet restore "src/MedLogicService/MedLogicService.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/src/MedLogicService"
RUN dotnet build "MedLogicService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MedLogicService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MedLogicService.dll"]
