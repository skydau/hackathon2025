FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/AdminUI/AdminUI.csproj", "src/AdminUI/"]
RUN dotnet restore "src/AdminUI/AdminUI.csproj"
COPY . .
WORKDIR "/src/src/AdminUI"
RUN dotnet build "AdminUI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AdminUI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AdminUI.dll"]
