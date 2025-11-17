# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Gateway.Worker/Gateway.Worker.csproj", "src/Gateway.Worker/"]
COPY ["src/Gateway.Data/Gateway.Data.csproj", "src/Gateway.Data/"]
COPY ["src/Gateway.Domain/Gateway.Domain.csproj", "src/Gateway.Domain/"]

RUN dotnet restore "src/Gateway.Worker/Gateway.Worker.csproj"

COPY . .

RUN dotnet publish "src/Gateway.Worker/Gateway.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Gateway.Worker.dll"]
