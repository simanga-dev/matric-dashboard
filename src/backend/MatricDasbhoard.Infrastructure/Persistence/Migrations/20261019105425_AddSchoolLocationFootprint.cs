using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MatricDasbhoard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolLocationFootprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE geo.school_spatial_profiles
                  ADD COLUMN location_footprint geometry(MultiPolygon,4326);

                ALTER TABLE geo.school_spatial_profiles
                  ADD CONSTRAINT school_spatial_footprint_valid CHECK (
                    location_footprint IS NULL OR (
                      ST_IsValid(location_footprint) AND NOT ST_IsEmpty(location_footprint)
                      AND ST_NDims(location_footprint)=2
                      AND ST_XMin(Box3D(location_footprint)) >= -180
                      AND ST_XMax(Box3D(location_footprint)) <= 180
                      AND ST_YMin(Box3D(location_footprint)) >= -90
                      AND ST_YMax(Box3D(location_footprint)) <= 90
                      AND ST_Area(location_footprint::geography)>0
                      AND ST_NPoints(location_footprint)<=10000
                      AND ST_NumGeometries(location_footprint)<=100
                    )
                  );

                CREATE INDEX ix_school_spatial_footprint
                  ON geo.school_spatial_profiles USING gist(location_footprint);

                CREATE VIEW geo.school_footprints AS
                SELECT s."Id" AS id, s."EmisNumber" AS emis_number, s."CentreName" AS name,
                  s."Province" AS province, s."DistrictName" AS district, s."Quintile" AS quintile,
                  COALESCE(perf."TotalWrote",0) AS wrote, COALESCE(perf."TotalAchieved",0) AS passed,
                  CASE WHEN p.location_override IS NOT NULL THEN false ELSE COALESCE(l.is_sample,false) END AS is_sample,
                  COALESCE(p.location_footprint,
                    ST_SetSRID(ST_Multi(ST_Buffer(COALESCE(p.location_override,l.geom)::geography,25,3)::geometry),4326)
                  )::geometry(MultiPolygon,4326) AS geom
                FROM marks."Schools" s
                  LEFT JOIN geo.school_locations l ON l.emis_number=s."EmisNumber"
                  LEFT JOIN geo.school_spatial_profiles p ON p.school_id=s."Id"
                  LEFT JOIN LATERAL (SELECT "TotalWrote","TotalAchieved" FROM marks."SchoolPerformances"
                    WHERE "SchoolId"=s."Id" AND NOT "IsDeleted" ORDER BY "Year" DESC LIMIT 1) perf ON true
                WHERE NOT s."IsDeleted" AND COALESCE(p.location_override,l.geom) IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP VIEW IF EXISTS geo.school_footprints;
                DROP VIEW IF EXISTS geo.school_locations_effective;
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

                DROP INDEX IF EXISTS geo.ix_school_spatial_footprint;
                ALTER TABLE geo.school_spatial_profiles DROP CONSTRAINT school_spatial_footprint_valid;
                ALTER TABLE geo.school_spatial_profiles DROP COLUMN location_footprint;
                """);
        }
    }
}
