_default:
    @just --list

# Run the Aspire AppHost for full-stack development
dev:
    dotnet watch --project src/backend/MatricDasbhoard.AppHost/

# Run the WebApi directly (without Aspire orchestration)
run:
    dotnet watch --project src/backend/MatricDasbhoard.WebApi/

# Build the backend solution
build:
    dotnet build src/backend/MatricDasbhoard.slnx

# Run all backend tests
test:
    dotnet test src/backend/MatricDasbhoard.slnx -c Release

# Start PostGIS + GeoServer for the spatial school map (docker compose)
geo-up:
    docker compose up -d postgres geoserver

# Seed/publish school layers (run after geo-up and API database migrations)
geo-bootstrap:
    ./scripts/geoserver/bootstrap.sh

# Import real school coordinates from the DBE EMIS masterlists (run after geo-bootstrap)
geo-locations:
    ./scripts/import_school_locations.sh

# Build a cleaned school-name list from the DBE EMIS masterlist
clean-school-names:
    python3 scripts/clean_school_names.py

# Apply cleaned names to SQLite and Postgres
clean-school-names-apply:
    ./scripts/apply_clean_school_names.sh

# Add a new EF Core migration
migrate name:
    dotnet ef migrations add {{name}} \
        -c MatricDasbhoardDbContext \
        -p src/backend/MatricDasbhoard.Infrastructure/MatricDasbhoard.Infrastructure.csproj \
        -s src/backend/MatricDasbhoard.WebApi/MatricDasbhoard.WebApi.csproj

# Apply pending EF Core migrations to the database
db-update connection="Host=localhost;Database=matric_dashboard;Username=matric;Password=matric":
    dotnet ef database update \
        -c MatricDasbhoardDbContext \
        -p src/backend/MatricDasbhoard.Infrastructure/MatricDasbhoard.Infrastructure.csproj \
        -s src/backend/MatricDasbhoard.WebApi/MatricDasbhoard.WebApi.csproj \
        --connection "{{connection}}"
