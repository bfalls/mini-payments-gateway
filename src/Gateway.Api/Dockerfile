# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/Gateway.Api/Gateway.Api.csproj", "src/Gateway.Api/"]
COPY ["src/Gateway.Data/Gateway.Data.csproj", "src/Gateway.Data/"]
COPY ["src/Gateway.Domain/Gateway.Domain.csproj", "src/Gateway.Domain/"]
COPY ["src/Gateway.Worker/Gateway.Worker.csproj", "src/Gateway.Worker/"]
COPY ["src/PspStub.Api/PspStub.Api.csproj", "src/PspStub.Api/"]

# Restore only once
RUN dotnet restore "src/Gateway.Api/Gateway.Api.csproj"

# Copy the rest of the source
COPY . .

# Publish
RUN dotnet publish "src/Gateway.Api/Gateway.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Kestrel will listen on 8080 inside the container
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_LOGGING__CONSOLE__FORMATTERNAME=Simple
ENV Logging__LogLevel__Microsoft.AspNetCore=Information

EXPOSE 8080

ENTRYPOINT ["dotnet", "Gateway.Api.dll"]
