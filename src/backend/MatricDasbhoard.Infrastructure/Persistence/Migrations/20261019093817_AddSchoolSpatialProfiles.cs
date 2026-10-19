using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatricDasbhoard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolSpatialProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration owns profiles/history/views only. The source table is provisioned
            // for fresh installations using the existing init.sql schema, never replaced or seeded.
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS postgis;
                CREATE SCHEMA IF NOT EXISTS geo;
                CREATE TABLE IF NOT EXISTS geo.school_locations (
                    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    emis_number text NOT NULL, name text NOT NULL, province text NOT NULL,
                    district text, quintile integer, wrote integer NOT NULL DEFAULT 0,
                    passed integer NOT NULL DEFAULT 0, is_sample boolean NOT NULL DEFAULT true,
                    geom geometry(Point,4326) NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ux_school_locations_emis ON geo.school_locations(emis_number);
                CREATE INDEX IF NOT EXISTS ix_school_locations_geom ON geo.school_locations USING gist(geom);

                CREATE TABLE geo.school_spatial_profiles (
                    school_id uuid PRIMARY KEY REFERENCES marks."Schools"("Id") ON DELETE RESTRICT,
                    boundary geometry(MultiPolygon,4326), location_override geometry(Point,4326),
                    revision bigint NOT NULL CHECK(revision > 0), actor_id uuid NOT NULL,
                    changed_at timestamptz NOT NULL, reason text NOT NULL CHECK(length(trim(reason)) BETWEEN 1 AND 2000),
                    source_description text NOT NULL CHECK(length(trim(source_description)) BETWEEN 1 AND 1000),
                    CONSTRAINT school_spatial_boundary_valid CHECK(boundary IS NULL OR
                      (ST_IsValid(boundary) AND NOT ST_IsEmpty(boundary) AND ST_NDims(boundary)=2
                       AND ST_XMin(Box3D(boundary)) >= -180 AND ST_XMax(Box3D(boundary)) <= 180
                       AND ST_YMin(Box3D(boundary)) >= -90 AND ST_YMax(Box3D(boundary)) <= 90
                       AND ST_Area(boundary::geography)>0 AND ST_NPoints(boundary)<=10000
                       AND ST_NumGeometries(boundary)<=100)),
                    CONSTRAINT school_spatial_point_valid CHECK(location_override IS NULL OR
                      (NOT ST_IsEmpty(location_override) AND ST_NDims(location_override)=2
                       AND ST_X(location_override) BETWEEN -180 AND 180
                       AND ST_Y(location_override) BETWEEN -90 AND 90))
                );
                CREATE INDEX ix_school_spatial_boundary ON geo.school_spatial_profiles USING gist(boundary);
                CREATE INDEX ix_school_spatial_location ON geo.school_spatial_profiles USING gist(location_override);

                CREATE TABLE geo.school_spatial_history (
                    school_id uuid NOT NULL REFERENCES marks."Schools"("Id") ON DELETE RESTRICT,
                    revision bigint NOT NULL CHECK(revision > 0), actor_id uuid NOT NULL, changed_at timestamptz NOT NULL,
                    reason text NOT NULL, source_description text NOT NULL,
                    before_profile jsonb, after_profile jsonb NOT NULL,
                    PRIMARY KEY(school_id,revision)
                );
                COMMENT ON TABLE geo.school_spatial_history IS 'Immutable complete profile snapshots. API returns latest 100 revisions; all revisions retained.';
                CREATE FUNCTION geo.reject_school_spatial_history_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  RAISE EXCEPTION 'School spatial history is append-only';
                END $$;
                CREATE TRIGGER school_spatial_history_immutable BEFORE UPDATE OR DELETE OR TRUNCATE
                  ON geo.school_spatial_history FOR EACH STATEMENT EXECUTE FUNCTION geo.reject_school_spatial_history_mutation();

                CREATE VIEW geo.school_locations_effective AS
                SELECT s."Id" AS id, s."EmisNumber" AS emis_number, s."CentreName" AS name,
                  s."Province" AS province, s."DistrictName" AS district, s."Quintile" AS quintile,
                  COALESCE(perf."TotalWrote",0) AS wrote, COALESCE(perf."TotalAchieved",0) AS passed,
                  CASE WHEN p.location_override IS NOT NULL THEN false ELSE COALESCE(l.is_sample,false) END AS is_sample,
                  COALESCE(p.location_override,l.geom)::geometry(Point,4326) AS geom
                FROM marks."Schools" s
                  LEFT JOIN geo.school_locations l ON l.emis_number=s."EmisNumber"
                  LEFT JOIN geo.school_spatial_profiles p ON p.school_id=s."Id"
                  LEFT JOIN LATERAL (SELECT "TotalWrote","TotalAchieved" FROM marks."SchoolPerformances"
                    WHERE "SchoolId"=s."Id" AND NOT "IsDeleted" ORDER BY "Year" DESC LIMIT 1) perf ON true
                WHERE NOT s."IsDeleted" AND COALESCE(p.location_override,l.geom) IS NOT NULL;

                CREATE VIEW geo.school_boundaries AS
                SELECT s."Id" AS id, s."EmisNumber" AS emis_number, s."CentreName" AS name,
                  s."Province" AS province, s."DistrictName" AS district, s."Quintile" AS quintile,
                  COALESCE(perf."TotalWrote",0) AS wrote, COALESCE(perf."TotalAchieved",0) AS passed,
                  CASE WHEN p.location_override IS NOT NULL THEN false ELSE COALESCE(l.is_sample,false) END AS is_sample,
                  p.boundary::geometry(MultiPolygon,4326) AS geom
                FROM marks."Schools" s
                  JOIN geo.school_spatial_profiles p ON p.school_id=s."Id"
                  LEFT JOIN geo.school_locations l ON l.emis_number=s."EmisNumber"
                  LEFT JOIN LATERAL (SELECT "TotalWrote","TotalAchieved" FROM marks."SchoolPerformances"
                    WHERE "SchoolId"=s."Id" AND NOT "IsDeleted" ORDER BY "Year" DESC LIMIT 1) perf ON true
                WHERE NOT s."IsDeleted" AND p.boundary IS NOT NULL AND NOT ST_IsEmpty(p.boundary);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP VIEW geo.school_boundaries;
                DROP VIEW geo.school_locations_effective;
                DROP TABLE geo.school_spatial_history;
                DROP FUNCTION geo.reject_school_spatial_history_mutation();
                DROP TABLE geo.school_spatial_profiles;
                """);
            // Never drop imported geo.school_locations, geo schema, or the shared PostGIS extension.
        }
    }
}
