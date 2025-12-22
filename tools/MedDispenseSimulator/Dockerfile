# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["tools/MedDispenseSimulator/MedDispenseSimulator.csproj", "MedDispenseSimulator/"]
RUN dotnet restore "MedDispenseSimulator/MedDispenseSimulator.csproj"

# Copy everything else and build
COPY tools/MedDispenseSimulator/ MedDispenseSimulator/
WORKDIR "/src/MedDispenseSimulator"
RUN dotnet build "MedDispenseSimulator.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "MedDispenseSimulator.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MedDispenseSimulator.dll"]