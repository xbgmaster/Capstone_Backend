# ---- Stage 1: build & publish -------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project files first so 'dotnet restore' is cached as long as
# the .csproj files don't change (much faster rebuilds).
COPY ["src/JobNet.Api/JobNet.Api.csproj", "src/JobNet.Api/"]
COPY ["src/JobNet.Infrastructure/JobNet.Infrastructure.csproj", "src/JobNet.Infrastructure/"]
COPY ["src/JobNet.Domain/JobNet.Domain.csproj", "src/JobNet.Domain/"]
RUN dotnet restore "src/JobNet.Api/JobNet.Api.csproj"

# Copy the rest of the source and publish a Release build.
COPY . .
RUN dotnet publish "src/JobNet.Api/JobNet.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false


# ---- Stage 2: runtime ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Kestrel listens on 8080 inside the container (HTTP).
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

# Run as the non-root user that ships with the .NET 8 image (best practice).
USER $APP_UID

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "JobNet.Api.dll"]
