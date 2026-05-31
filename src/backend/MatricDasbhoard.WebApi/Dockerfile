# Use the official ASP.NET Core runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-wide config files
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["nuget.config", "."]

# Copy csproj files
COPY ["MatricDasbhoard.Shared/MatricDasbhoard.Shared.csproj", "MatricDasbhoard.Shared/"]
COPY ["MatricDasbhoard.Domain/MatricDasbhoard.Domain.csproj", "MatricDasbhoard.Domain/"]
COPY ["MatricDasbhoard.Application/MatricDasbhoard.Application.csproj", "MatricDasbhoard.Application/"]
COPY ["MatricDasbhoard.Infrastructure/MatricDasbhoard.Infrastructure.csproj", "MatricDasbhoard.Infrastructure/"]
COPY ["MatricDasbhoard.ServiceDefaults/MatricDasbhoard.ServiceDefaults.csproj", "MatricDasbhoard.ServiceDefaults/"]
COPY ["MatricDasbhoard.WebApi/MatricDasbhoard.WebApi.csproj", "MatricDasbhoard.WebApi/"]
COPY ["HealthProbe/HealthProbe.csproj", "HealthProbe/"]

# Restore dependencies
RUN dotnet restore "MatricDasbhoard.WebApi/MatricDasbhoard.WebApi.csproj" \
    && dotnet restore "HealthProbe/HealthProbe.csproj"

# Copy the entire source code
COPY . .

# Build the application
WORKDIR "/src/MatricDasbhoard.WebApi"
RUN dotnet build "MatricDasbhoard.WebApi.csproj" -c Release

# Publish stage
FROM build AS publish

# StripDevConfig controls whether appsettings.Development/Testing.json are excluded.
# Default: true (production). Pass STRIP_DEV_CONFIG=false as a build arg to keep them.
ARG STRIP_DEV_CONFIG=true

RUN dotnet publish "MatricDasbhoard.WebApi.csproj" -c Release -o /app/publish \
    /p:UseAppHost=false /p:DebugType=None /p:DebugSymbols=false \
    /p:StripDevConfig=${STRIP_DEV_CONFIG} \
    && dotnet publish "/src/HealthProbe/HealthProbe.csproj" -c Release -o /app/healthprobe \
    /p:DebugType=None /p:DebugSymbols=false

# Defense-in-depth: strip non-production config even if .csproj exclusion is bypassed
RUN if [ "$STRIP_DEV_CONFIG" = "true" ]; then \
        rm -f /app/publish/appsettings.Development.json /app/publish/appsettings.Testing.json; \
    fi

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=publish /app/healthprobe ./healthprobe/

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "MatricDasbhoard.WebApi.dll"]
