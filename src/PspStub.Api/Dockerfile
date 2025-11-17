# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/PspStub.Api/PspStub.Api.csproj", "src/PspStub.Api/"]
COPY ["src/Gateway.Domain/Gateway.Domain.csproj", "src/Gateway.Domain/"]

RUN dotnet restore "src/PspStub.Api/PspStub.Api.csproj"

COPY . .

RUN dotnet publish "src/PspStub.Api/PspStub.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PspStub.Api.dll"]
