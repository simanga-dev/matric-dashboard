using System.Text.Json;
using MatricDasbhoard.Application.Features.SchoolSpatial;
using MatricDasbhoard.Application.Identity;
using MatricDasbhoard.Application.Identity.Constants;
using MatricDasbhoard.Component.Tests.Fixtures;
using MatricDasbhoard.Domain.Entities;
using MatricDasbhoard.Infrastructure.Features.SchoolSpatial.Services;
using MatricDasbhoard.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace MatricDasbhoard.Component.Tests.Services;

public sealed class SchoolSpatialServiceTests(PostgisFixture fixture) : IClassFixture<PostgisFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly JsonElement Boundary = JsonSerializer.Deserialize<JsonElement>(
        """{"type":"Polygon","coordinates":[[[28,-26],[28.002,-26],[28.002,-26.002],[28,-26.002],[28,-26]]]}""");
    private static readonly JsonElement Point = JsonSerializer.Deserialize<JsonElement>("""{"type":"Point","coordinates":[28.001,-26.001]}""");
    private static readonly JsonElement Footprint = JsonSerializer.Deserialize<JsonElement>(
        """{"type":"MultiPolygon","coordinates":[[[[27.9998,-26.0012],[28.0002,-26.0012],[28.0002,-26.0008],[27.9998,-26.0008],[27.9998,-26.0012]]]]}""");
    private static SaveSpatialInput Input(long revision = 0) => new(revision, Boundary, "override", Point, "Correct grounds", "Field visit", Footprint);

    private static IUserContext Editor()
    {
        var user = Substitute.For<IUserContext>();
        user.IsAuthenticated.Returns(true);
        user.AuthenticatedUserId.Returns(Actor);
        user.HasPermission(AppPermissions.Schools.SpatialEdit).Returns(true);
        return user;
    }

    private async Task<School> SchoolAsync(bool imported = false)
    {
        await using var db = fixture.Context();
        var school = new School("GAUTENG", "District", "0" + Guid.NewGuid().ToString("N")[..18], "123", "Test school", 3);
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        if (imported)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO geo.school_locations(emis_number,name,province,is_sample,geom)
                VALUES({school.EmisNumber},'Source school','GAUTENG',false,ST_SetSRID(ST_MakePoint(27,-25),4326))
                """);
        return school;
    }

    [PostgisFact]
    public async Task Save_PersistsCanonicalStateCompleteHistoryAndImportPrecedence()
    {
        var school = await SchoolAsync(true);
        await using var db = fixture.Context();
        Assert.False(db.Database.HasPendingModelChanges());
        var service = new SchoolSpatialService(db, Editor(), new FakeTimeProvider(Now));
        var initial = await service.GetAsync(school.EmisNumber, default);
        Assert.Equal("default", initial.Value.FootprintSource);
        Assert.Equal("MultiPolygon", initial.Value.Footprint?.GetProperty("type").GetString());
        var first = await service.SaveAsync(school.EmisNumber, Input(), default);
        Assert.True(first.IsSuccess, first.Error);
        Assert.Equal(1, first.Value.Revision);
        Assert.Equal("MultiPolygon", first.Value.Boundary?.GetProperty("type").GetString());
        Assert.Equal("MultiPolygon", first.Value.Footprint?.GetProperty("type").GetString());
        Assert.Equal("editor", first.Value.FootprintSource);
        Assert.Equal("editor", first.Value.LocationSource);
        Assert.True(first.Value.AreaSquareMetres is > 40000 and < 50000);
        Assert.Contains(first.Value.Warnings, x => x.Contains("1 km"));
        var second = await service.SaveAsync(school.EmisNumber, Input(1) with { Reason = "Refine grounds" }, default);
        Assert.True(second.IsSuccess, second.Error);
        var history = await service.GetHistoryAsync(school.EmisNumber, default);
        Assert.Equal(2, history.Value.Count);
        Assert.Equal(Now, history.Value[0].ChangedAt);
        Assert.NotNull(history.Value[0].BeforeBoundary);
        Assert.Null(history.Value[1].BeforeBoundary);
        Assert.Null(history.Value[1].BeforeLocationOverride);
        Assert.NotNull(history.Value[1].AfterLocationOverride);
        Assert.NotNull(history.Value[0].BeforeFootprint);
        Assert.Null(history.Value[1].BeforeFootprint);
        Assert.NotNull(history.Value[1].AfterFootprint);
        // Real imports and sample bootstrap touch source rows only, not corrections.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE geo.school_locations SET geom=ST_SetSRID(ST_MakePoint(29,-27),4326),is_sample=false
            WHERE emis_number={school.EmisNumber}
            """);
        var effective = await service.GetAsync(school.EmisNumber, default);
        Assert.Equal("editor", effective.Value.LocationSource);
        Assert.Equal(28.001, effective.Value.Location?.GetProperty("coordinates")[0].GetDouble());
        Assert.Equal(2, effective.Value.Revision);
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT after_profile->>'actorId' FROM geo.school_spatial_history WHERE school_id=@id LIMIT 1", connection);
        command.Parameters.AddWithValue("id", school.Id);
        Assert.Equal(Actor.ToString(), await command.ExecuteScalarAsync());
    }

    [PostgisFact]
    public async Task OverlapWarnings_IgnoreArchivedSchoolsButIncludeActiveSchools()
    {
        var school = await SchoolAsync();
        var neighbour = await SchoolAsync();
        var boundary = JsonSerializer.Deserialize<JsonElement>(
            """{"type":"Polygon","coordinates":[[[20,-30],[20.002,-30],[20.002,-30.002],[20,-30.002],[20,-30]]]}""");
        var input = Input() with { Boundary = boundary, LocationMode = "imported", Location = null };
        await using var db = fixture.Context();
        var service = new SchoolSpatialService(db, Editor(), TimeProvider.System);
        Assert.True((await service.SaveAsync(neighbour.EmisNumber, input, default)).IsSuccess);
        var active = await service.SaveAsync(school.EmisNumber, input, default);
        Assert.True(active.IsSuccess, active.Error);
        Assert.Contains(active.Value.Warnings, warning => warning.Contains("overlap"));

        var archived = await db.Schools.SingleAsync(x => x.Id == neighbour.Id);
        archived.SoftDelete();
        await db.SaveChangesAsync();
        var state = await service.GetAsync(school.EmisNumber, default);
        Assert.True(state.IsSuccess, state.Error);
        Assert.DoesNotContain(state.Value.Warnings, warning => warning.Contains("overlap"));
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var retained = new NpgsqlCommand("SELECT boundary IS NOT NULL FROM geo.school_spatial_profiles WHERE school_id=@id", connection);
        retained.Parameters.AddWithValue("id", neighbour.Id);
        Assert.Equal(true, await retained.ExecuteScalarAsync());

        archived.Restore();
        await db.SaveChangesAsync();
        var restored = await service.GetAsync(school.EmisNumber, default);
        Assert.Contains(restored.Value.Warnings, warning => warning.Contains("overlap"));
    }

    [PostgisFact]
    public async Task Boundary_PolygonWithHoleAndDetachedMultiPolygon_RoundTripWithoutLosingRings()
    {
        var school = await SchoolAsync();
        var polygon = JsonSerializer.Deserialize<JsonElement>("""
            {"type":"Polygon","coordinates":[
              [[22,-30],[22.01,-30],[22.01,-30.01],[22,-30.01],[22,-30]],
              [[22.002,-30.002],[22.002,-30.004],[22.004,-30.004],[22.004,-30.002],[22.002,-30.002]]
            ]}
            """);
        var multiPolygon = JsonSerializer.Deserialize<JsonElement>("""
            {"type":"MultiPolygon","coordinates":[
              [
                [[22,-30],[22.01,-30],[22.01,-30.01],[22,-30.01],[22,-30]],
                [[22.002,-30.002],[22.002,-30.004],[22.004,-30.004],[22.004,-30.002],[22.002,-30.002]]
              ],
              [[[22.02,-30],[22.022,-30],[22.022,-30.002],[22.02,-30.002],[22.02,-30]]]
            ]}
            """);
        await using var db = fixture.Context();
        var service = new SchoolSpatialService(db, Editor(), TimeProvider.System);
        var first = await service.SaveAsync(school.EmisNumber, Input() with { Boundary = polygon }, default);
        Assert.True(first.IsSuccess, first.Error);
        Assert.NotNull(first.Value.Boundary);
        var firstCoordinates = first.Value.Boundary.Value.GetProperty("coordinates");
        Assert.Equal("MultiPolygon", first.Value.Boundary.Value.GetProperty("type").GetString());
        Assert.Single(firstCoordinates.EnumerateArray());
        Assert.True(JsonElement.DeepEquals(polygon.GetProperty("coordinates"), firstCoordinates[0]));
        var readFirst = await service.GetAsync(school.EmisNumber, default);
        Assert.True(JsonElement.DeepEquals(first.Value.Boundary.Value, readFirst.Value.Boundary!.Value));

        var second = await service.SaveAsync(school.EmisNumber, Input(1) with { Boundary = multiPolygon }, default);
        Assert.True(second.IsSuccess, second.Error);
        Assert.NotNull(second.Value.Boundary);
        Assert.True(JsonElement.DeepEquals(multiPolygon, second.Value.Boundary.Value));
        Assert.True(second.Value.AreaSquareMetres > first.Value.AreaSquareMetres);
        var readSecond = await service.GetAsync(school.EmisNumber, default);
        Assert.True(JsonElement.DeepEquals(multiPolygon, readSecond.Value.Boundary!.Value));
        var history = (await service.GetHistoryAsync(school.EmisNumber, default)).Value;
        Assert.Equal(2, history.Count);
        Assert.True(JsonElement.DeepEquals(first.Value.Boundary.Value, history[0].BeforeBoundary!.Value));
        Assert.True(JsonElement.DeepEquals(multiPolygon, history[0].AfterBoundary!.Value));
    }

    [PostgisFact]
    public async Task Boundary_HoleOutsideShell_IsRejectedWithoutChangingStateOrHistory()
    {
        var school = await SchoolAsync();
        await using var db = fixture.Context();
        var service = new SchoolSpatialService(db, Editor(), TimeProvider.System);
        var saved = await service.SaveAsync(school.EmisNumber, Input(), default);
        Assert.True(saved.IsSuccess, saved.Error);
        var invalidHole = JsonSerializer.Deserialize<JsonElement>("""
            {"type":"Polygon","coordinates":[
              [[28,-26],[28.002,-26],[28.002,-26.002],[28,-26.002],[28,-26]],
              [[28.01,-26.01],[28.01,-26.012],[28.012,-26.012],[28.012,-26.01],[28.01,-26.01]]
            ]}
            """);
        var invalid = await service.SaveAsync(school.EmisNumber, Input(1) with { Boundary = invalidHole }, default);
        Assert.False(invalid.IsSuccess);
        Assert.Equal(SpatialErrors.InvalidGeometry, invalid.Error);
        var state = (await service.GetAsync(school.EmisNumber, default)).Value;
        Assert.Equal(saved.Value.Revision, state.Revision);
        Assert.Equal(saved.Value.AreaSquareMetres, state.AreaSquareMetres);
        Assert.True(JsonElement.DeepEquals(saved.Value.Boundary!.Value, state.Boundary!.Value));
        Assert.True(JsonElement.DeepEquals(saved.Value.Location!.Value, state.Location!.Value));
        var history = (await service.GetHistoryAsync(school.EmisNumber, default)).Value;
        var entry = Assert.Single(history);
        Assert.Equal(1, entry.Revision);
        Assert.True(JsonElement.DeepEquals(saved.Value.Boundary.Value, entry.AfterBoundary!.Value));
    }

    [PostgisFact]
    public async Task Saves_FirstCreationAndRegularRaces_HaveExactlyOneWinner()
    {
        var school = await SchoolAsync();
        foreach (var revision in new long[] { 0, 1 })
        {
            async Task<Result<SpatialStateOutput>> Save()
            {
                await using var db = fixture.Context();
                return await new SchoolSpatialService(db, Editor(), new FakeTimeProvider(Now))
                    .SaveAsync(school.EmisNumber, Input(revision), default);
            }
            var results = await Task.WhenAll(Save(), Save());
            Assert.Single(results, x => x.IsSuccess);
            Assert.Single(results, x => x.ErrorType == ErrorType.Conflict);
        }
        await using var read = fixture.Context();
        var history = await new SchoolSpatialService(read, Editor(), TimeProvider.System).GetHistoryAsync(school.EmisNumber, default);
        Assert.Equal(2, history.Value.Count);
    }

    [PostgisFact]
    public async Task Save_InvalidTopologyAndClearing_LeaveStateAndHistoryUntouched()
    {
        var school = await SchoolAsync();
        await using var db = fixture.Context();
        var service = new SchoolSpatialService(db, Editor(), TimeProvider.System);
        var selfIntersection = JsonSerializer.Deserialize<JsonElement>(
            """{"type":"Polygon","coordinates":[[[28,-26],[28.002,-26.002],[28.002,-26],[28,-26.002],[28,-26]]]}""");
        var invalid = await service.SaveAsync(school.EmisNumber, Input() with { Boundary = selfIntersection }, default);
        Assert.False(invalid.IsSuccess);
        Assert.Empty((await service.GetHistoryAsync(school.EmisNumber, default)).Value);
        Assert.True((await service.SaveAsync(school.EmisNumber, Input(), default)).IsSuccess);
        Assert.False((await service.SaveAsync(school.EmisNumber, Input(1) with { Boundary = null }, default)).IsSuccess);
        Assert.False((await service.SaveAsync(school.EmisNumber, Input(1) with { LocationMode = "imported", Location = null }, default)).IsSuccess);
        Assert.Single((await service.GetHistoryAsync(school.EmisNumber, default)).Value);
    }

    [PostgisFact]
    public async Task ImportedMode_DoesNotCreateOverride_AndMissingPointCanBeAdded()
    {
        var school = await SchoolAsync(true);
        await using var db = fixture.Context();
        var service = new SchoolSpatialService(db, Editor(), TimeProvider.System);
        var state = await service.SaveAsync(school.EmisNumber, Input() with { LocationMode = "imported", Location = null }, default);
        Assert.True(state.IsSuccess, state.Error);
        Assert.False(state.Value.HasLocationOverride);
        Assert.Equal("imported", state.Value.LocationSource);
        var missing = await SchoolAsync();
        var initial = await service.GetAsync(missing.EmisNumber, default);
        Assert.Equal("missing", initial.Value.LocationSource);
        var added = await service.SaveAsync(missing.EmisNumber, Input() with { Boundary = null }, default);
        Assert.True(added.IsSuccess, added.Error);
        Assert.True(added.Value.HasLocationOverride);
    }

    [PostgisFact]
    public async Task History_IsImmutableAndForeignKeysRestrictSchoolDeletion()
    {
        var school = await SchoolAsync();
        await using var db = fixture.Context();
        var service = new SchoolSpatialService(db, Editor(), TimeProvider.System);
        Assert.True((await service.SaveAsync(school.EmisNumber, Input(), default)).IsSuccess);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM geo.school_spatial_history WHERE school_id={school.Id}"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM marks.\"Schools\" WHERE \"Id\"={school.Id}"));
    }

    [PostgisFact]
    public async Task HistoryFailure_RollsBackProfile_AndHistoryResponseIsBounded()
    {
        var school = await SchoolAsync();
        await using var db = fixture.Context();
        var service = new SchoolSpatialService(db, Editor(), TimeProvider.System);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE OR REPLACE FUNCTION geo.test_reject_history() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.reason='test rollback' THEN RAISE EXCEPTION 'Simulated history failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER test_reject_history BEFORE INSERT ON geo.school_spatial_history
              FOR EACH ROW EXECUTE FUNCTION geo.test_reject_history();
            """);
        try
        {
            await Assert.ThrowsAsync<PostgresException>(() => service.SaveAsync(school.EmisNumber, Input() with { Reason = "test rollback" }, default));
            Assert.Equal(0, (await service.GetAsync(school.EmisNumber, default)).Value.Revision);
            Assert.Empty((await service.GetHistoryAsync(school.EmisNumber, default)).Value);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_reject_history ON geo.school_spatial_history; DROP FUNCTION geo.test_reject_history();");
        }
        for (var revision = 0; revision < 101; revision++)
            Assert.True((await service.SaveAsync(school.EmisNumber, Input(revision), default)).IsSuccess);
        var history = (await service.GetHistoryAsync(school.EmisNumber, default)).Value;
        Assert.Equal(100, history.Count);
        Assert.Equal(101, history[0].Revision);
        Assert.Equal(2, history[^1].Revision);
    }

    [PostgisFact]
    public async Task Migration_ExistingImportedSourceSurvivesUpgradeAndDowngrade()
    {
        // Separate disposable database inside the fixture container. Never a live application database.
        var database = "migration_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(fixture.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await create.ExecuteNonQueryAsync();
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = database }.ConnectionString;
        await using var db = new Infrastructure.Persistence.MatricDasbhoardDbContext(
            new DbContextOptionsBuilder<Infrastructure.Persistence.MatricDasbhoardDbContext>().UseNpgsql(connectionString).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261014083355_AddSchoolSubjects");
        await db.Database.ExecuteSqlRawAsync("""
            CREATE EXTENSION IF NOT EXISTS postgis; CREATE SCHEMA geo;
            CREATE TABLE geo.school_locations (
              id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, emis_number text NOT NULL,
              name text NOT NULL,province text NOT NULL,district text,quintile integer,
              wrote integer NOT NULL DEFAULT 0,passed integer NOT NULL DEFAULT 0,
              is_sample boolean NOT NULL DEFAULT true,geom geometry(Point,4326) NOT NULL);
            INSERT INTO geo.school_locations(emis_number,name,province,is_sample,geom)
              VALUES('001234567','Original imported point','GAUTENG',false,ST_SetSRID(ST_MakePoint(28,-26),4326));
            """);
        await migrator.MigrateAsync();
        await migrator.MigrateAsync("20261014083355_AddSchoolSubjects");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var query = new NpgsqlCommand("SELECT name,ST_X(geom),is_sample FROM geo.school_locations WHERE emis_number='001234567'", connection);
        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Original imported point", reader.GetString(0));
        Assert.Equal(28, reader.GetDouble(1));
        Assert.False(reader.GetBoolean(2));
    }

    [PostgisFact]
    public async Task PublicViews_HaveOnlySafeFieldsStableIdsAndTypedGeometry()
    {
        var school = await SchoolAsync();
        await using var db = fixture.Context();
        Assert.True((await new SchoolSpatialService(db, Editor(), TimeProvider.System).SaveAsync(school.EmisNumber, Input(), default)).IsSuccess);
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        foreach (var view in new[] { "school_boundaries", "school_footprints" })
        {
            await using var command = new NpgsqlCommand($"SELECT * FROM geo.{view} WHERE id=@id", connection);
            command.Parameters.AddWithValue("id", school.Id);
            await using (var reader = await command.ExecuteReaderAsync())
            {
                Assert.True(await reader.ReadAsync());
                Assert.Equal(school.Id, reader.GetGuid(0));
                Assert.Equal(new[] { "id", "emis_number", "name", "province", "district", "quintile", "wrote", "passed", "is_sample", "geom" },
                    Enumerable.Range(0, reader.FieldCount).Select(reader.GetName));
            }
            await using var geometry = new NpgsqlCommand("SELECT type FROM geometry_columns WHERE f_table_schema='geo' AND f_table_name=@view AND srid=4326", connection);
            geometry.Parameters.AddWithValue("view", view);
            Assert.Equal("MULTIPOLYGON", await geometry.ExecuteScalarAsync());
        }
        await using var legacyGeometry = new NpgsqlCommand(
            "SELECT type FROM geometry_columns WHERE f_table_schema='geo' AND f_table_name='school_locations_effective' AND srid=4326",
            connection);
        Assert.Equal("POINT", await legacyGeometry.ExecuteScalarAsync());
    }
}
