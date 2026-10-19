using System.Text.Json;
using MatricDasbhoard.Application.Features.SchoolSpatial;
using MatricDasbhoard.Application.Identity;
using MatricDasbhoard.Application.Identity.Constants;
using MatricDasbhoard.Infrastructure.Persistence;
using MatricDasbhoard.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace MatricDasbhoard.Infrastructure.Features.SchoolSpatial.Services;

/// <summary>
/// Parameterized PostGIS persistence. School row locks serialize first creation and subsequent revisions.
/// A dedicated connection keeps transactions independent of EF retry execution strategies.
/// </summary>
internal sealed class SchoolSpatialService(MatricDasbhoardDbContext context, IUserContext userContext,
    TimeProvider timeProvider) : ISchoolSpatialService
{
    /// <inheritdoc />
    public async Task<Result<SpatialStateOutput>> GetAsync(string emis, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await ReadAsync(connection, null, emis, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<SpatialStateOutput>> SaveAsync(string emis, SaveSpatialInput input, CancellationToken cancellationToken)
    {
        if (!CanEdit()) return Result<SpatialStateOutput>.Failure(SpatialErrors.Forbidden, ErrorType.Forbidden);
        if (!SpatialGeometryValidation.IsValid(input)) return Result<SpatialStateOutput>.Failure(SpatialErrors.Invalid);

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // Lock the parent even when no profile exists. A second creator observes the committed revision.
        await using var schoolCommand = Command(connection, transaction,
            """SELECT "Id" FROM marks."Schools" WHERE "EmisNumber" = @emis AND NOT "IsDeleted" FOR UPDATE""", ("emis", emis));
        if (await schoolCommand.ExecuteScalarAsync(cancellationToken) is not Guid schoolId)
            return Result<SpatialStateOutput>.Failure(SpatialErrors.NotFound, ErrorType.NotFound);

        await using var previousCommand = Command(connection, transaction,
            """
            SELECT revision, boundary IS NOT NULL, location_override IS NOT NULL,
              jsonb_build_object('schoolId', school_id, 'boundary', ST_AsGeoJSON(boundary, 15)::jsonb,
                'locationFootprint', ST_AsGeoJSON(location_footprint, 15)::jsonb,
                'locationOverride', ST_AsGeoJSON(location_override, 15)::jsonb,
                'revision', revision, 'reason', reason, 'sourceDescription', source_description,
                'actorId', actor_id, 'changedAt', changed_at)::text
            FROM geo.school_spatial_profiles WHERE school_id = @school
            """, ("school", schoolId));
        long revision = 0;
        string? before = null;
        var hadBoundary = false;
        var hadOverride = false;
        await using (var reader = await previousCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                revision = reader.GetInt64(0);
                hadBoundary = reader.GetBoolean(1);
                hadOverride = reader.GetBoolean(2);
                before = reader.GetString(3);
            }
        }
        if (revision != input.ExpectedRevision)
            return Result<SpatialStateOutput>.Failure(SpatialErrors.Conflict, ErrorType.Conflict);
        var boundary = JsonText(input.Boundary);
        var footprint = JsonText(input.Footprint);
        var location = input.LocationMode == "override" ? JsonText(input.Location) : null;
        if ((hadBoundary && boundary is null) || (hadOverride && location is null))
            return Result<SpatialStateOutput>.Failure(SpatialErrors.CannotClear);
        // A null footprint preserves the existing one rather than clearing it.

        // No ST_MakeValid: invalid drafts must be corrected by the editor, not silently repaired.
        foreach (var value in new[] { boundary, footprint })
        {
            if (value is null) continue;
            await using var validity = Command(connection, transaction,
                """
                SELECT ST_IsValid(g) AND NOT ST_IsEmpty(g) AND ST_Area(g::geography) > 0
                FROM (SELECT ST_SetSRID(ST_GeomFromGeoJSON(@shape),4326) g) draft
                """, ("shape", value));
            if (await validity.ExecuteScalarAsync(cancellationToken) is not true)
                return Result<SpatialStateOutput>.Failure(SpatialErrors.InvalidGeometry);
        }

        var changedAt = timeProvider.GetUtcNow();
        await using var save = Command(connection, transaction,
            """
            WITH saved AS (
              INSERT INTO geo.school_spatial_profiles
                (school_id, boundary, location_override, location_footprint, revision, actor_id, changed_at, reason, source_description)
              VALUES (@school, ST_Multi(ST_SetSRID(ST_GeomFromGeoJSON(@boundary),4326)),
                ST_SetSRID(ST_GeomFromGeoJSON(@location),4326),
                ST_Multi(ST_SetSRID(ST_GeomFromGeoJSON(@footprint),4326)), @revision, @actor, @time, @reason, @source)
              ON CONFLICT (school_id) DO UPDATE SET boundary=EXCLUDED.boundary,
                location_override=EXCLUDED.location_override,
                location_footprint=COALESCE(EXCLUDED.location_footprint, geo.school_spatial_profiles.location_footprint),
                revision=EXCLUDED.revision,
                actor_id=EXCLUDED.actor_id, changed_at=EXCLUDED.changed_at,
                reason=EXCLUDED.reason, source_description=EXCLUDED.source_description
              RETURNING *
            )
            INSERT INTO geo.school_spatial_history
              (school_id, revision, actor_id, changed_at, reason, source_description, before_profile, after_profile)
            SELECT school_id, revision, actor_id, changed_at, reason, source_description, @before::jsonb,
              jsonb_build_object('schoolId', school_id, 'boundary', ST_AsGeoJSON(boundary,15)::jsonb,
                'locationFootprint', ST_AsGeoJSON(location_footprint,15)::jsonb,
                'locationOverride', ST_AsGeoJSON(location_override,15)::jsonb,
                'revision', revision, 'reason', reason, 'sourceDescription', source_description,
                'actorId', actor_id, 'changedAt', changed_at)
            FROM saved
            """, ("school", schoolId), ("boundary", boundary), ("footprint", footprint), ("location", location), ("revision", revision + 1),
            ("actor", userContext.AuthenticatedUserId), ("time", changedAt), ("reason", input.Reason.Trim()),
            ("source", input.SourceDescription.Trim()), ("before", before));
        await save.ExecuteNonQueryAsync(cancellationToken);
        // Read before commit so the returned state is exactly this saved revision, not a later writer's.
        var result = await ReadAsync(connection, transaction, emis, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<SpatialHistoryOutput>>> GetHistoryAsync(string emis, CancellationToken cancellationToken)
    {
        if (!CanEdit()) return Result<IReadOnlyList<SpatialHistoryOutput>>.Failure(SpatialErrors.Forbidden, ErrorType.Forbidden);
        await using var connection = await OpenAsync(cancellationToken);
        await using var exists = Command(connection, null,
            """
            SELECT "Id" FROM marks."Schools" WHERE "EmisNumber"=@emis AND NOT "IsDeleted"
            """, ("emis", emis));
        if (await exists.ExecuteScalarAsync(cancellationToken) is not Guid school)
            return Result<IReadOnlyList<SpatialHistoryOutput>>.Failure(SpatialErrors.NotFound, ErrorType.NotFound);
        await using var command = Command(connection, null,
            """
            SELECT h.revision, h.changed_at, h.reason, h.source_description,
              (h.before_profile->'boundary')::text, (h.after_profile->'boundary')::text,
              (h.before_profile->'locationOverride')::text, (h.after_profile->'locationOverride')::text,
              (h.before_profile->'locationFootprint')::text, (h.after_profile->'locationFootprint')::text,
              h.actor_id, u."FirstName", u."LastName", u."UserName"
            FROM geo.school_spatial_history h
            LEFT JOIN auth."Users" u ON u."Id"=h.actor_id
            WHERE h.school_id=@school ORDER BY h.revision DESC LIMIT 100
            """, ("school", school));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var history = new List<SpatialHistoryOutput>();
        while (await reader.ReadAsync(cancellationToken))
            history.Add(new(reader.GetInt64(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2), reader.GetString(3),
                Geometry(reader, 4), Geometry(reader, 5), Geometry(reader, 6), Geometry(reader, 7),
                Geometry(reader, 8), Geometry(reader, 9), reader.GetGuid(10), ActorName(reader, 11, 12, 13)));
        return Result<IReadOnlyList<SpatialHistoryOutput>>.Success(history);
    }

    private bool CanEdit() => userContext.IsAuthenticated && userContext.HasPermission(AppPermissions.Schools.SpatialEdit);

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        try { await connection.OpenAsync(ct); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static async Task<Result<SpatialStateOutput>> ReadAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        string emis, CancellationToken ct)
    {
        await using var command = Command(connection, transaction,
            """
            SELECT s."EmisNumber", ST_AsGeoJSON(p.boundary,15),
              ST_AsGeoJSON(COALESCE(p.location_override,l.geom),15), p.location_override IS NOT NULL,
              CASE WHEN p.location_override IS NOT NULL THEN 'editor' WHEN l.geom IS NULL THEN 'missing'
                WHEN l.is_sample THEN 'sample' ELSE 'imported' END,
              COALESCE(p.revision,0), ST_Area(p.boundary::geography),
              CASE WHEN p.boundary IS NOT NULL AND COALESCE(p.location_override,l.geom) IS NOT NULL
                THEN NOT ST_Covers(p.boundary,COALESCE(p.location_override,l.geom)) ELSE false END,
              CASE WHEN p.location_override IS NOT NULL AND l.geom IS NOT NULL
                THEN ST_Distance(p.location_override::geography,l.geom::geography)>1000 ELSE false END,
              EXISTS (SELECT 1 FROM geo.school_spatial_profiles other
                JOIN marks."Schools" other_school ON other_school."Id"=other.school_id
                WHERE other.school_id<>s."Id" AND NOT other_school."IsDeleted"
                AND other.boundary && p.boundary AND ST_Intersects(other.boundary,p.boundary)
                AND ST_Area(ST_Intersection(other.boundary,p.boundary)::geography)>1), s."Province",
              ST_AsGeoJSON(COALESCE(p.location_footprint,
                ST_SetSRID(ST_Multi(ST_Buffer(COALESCE(p.location_override,l.geom)::geography,25,3)::geometry),4326)),15),
              CASE WHEN p.location_footprint IS NOT NULL THEN 'editor'
                WHEN COALESCE(p.location_override,l.geom) IS NOT NULL THEN 'default' ELSE 'missing' END
            FROM marks."Schools" s LEFT JOIN geo.school_spatial_profiles p ON p.school_id=s."Id"
              LEFT JOIN geo.school_locations l ON l.emis_number=s."EmisNumber"
            WHERE s."EmisNumber"=@emis AND NOT s."IsDeleted"
            """, ("emis", emis));
        SpatialStateOutput state;
        string province;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return Result<SpatialStateOutput>.Failure(SpatialErrors.NotFound, ErrorType.NotFound);
            var warnings = new List<string>();
            double? area = reader.IsDBNull(6) ? null : reader.GetDouble(6);
            if (area is < 100 or > 1_000_000) warnings.Add("Boundary area is unusual for school grounds.");
            if (reader.GetBoolean(7)) warnings.Add("The school marker is outside the grounds.");
            if (reader.GetBoolean(8)) warnings.Add("The corrected marker is over 1 km from the imported marker.");
            if (reader.GetBoolean(9)) warnings.Add("The grounds overlap another school's grounds. Shared campuses may be valid.");
            state = new(reader.GetString(0), Geometry(reader, 1), Geometry(reader, 2), reader.GetBoolean(3),
                reader.GetString(4), reader.GetInt64(5), area, warnings,
                Geometry(reader, 11), reader.GetString(12));
            province = reader.GetString(10);
        }
        // Administrative boundaries are optional and may not have been imported yet.
        await using var table = Command(connection, transaction, "SELECT to_regclass('geo.provinces') IS NOT NULL");
        if (await table.ExecuteScalarAsync(ct) is true && state.Location is { } location)
        {
            await using var mismatch = Command(connection, transaction,
                """
                SELECT EXISTS(SELECT 1 FROM geo.provinces) AND NOT EXISTS(
                  SELECT 1 FROM geo.provinces WHERE upper(name)=upper(@province)
                    AND ST_Covers(geom,ST_SetSRID(ST_GeomFromGeoJSON(@point),4326)))
                """, ("province", province), ("point", location.GetRawText()));
            if (await mismatch.ExecuteScalarAsync(ct) is true)
                state = state with { Warnings = [.. state.Warnings, "The marker is outside the school's recorded province."] };
        }
        return Result<SpatialStateOutput>.Success(state);
    }

    private static string ActorName(NpgsqlDataReader reader, int firstName, int lastName, int userName)
    {
        var name = string.Join(' ', new[]
        {
            reader.IsDBNull(firstName) ? null : reader.GetString(firstName),
            reader.IsDBNull(lastName) ? null : reader.GetString(lastName)
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (!string.IsNullOrWhiteSpace(name)) return name;
        return reader.IsDBNull(userName) ? "Unknown user" : reader.GetString(userName);
    }

    private static JsonElement? Geometry(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        using var document = JsonDocument.Parse(reader.GetString(ordinal));
        return document.RootElement.ValueKind == JsonValueKind.Null ? null : document.RootElement.Clone();
    }

    private static string? JsonText(JsonElement? value) => value is null or { ValueKind: JsonValueKind.Null } ? null : value.Value.GetRawText();

    private static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            var parameter = new NpgsqlParameter { ParameterName = name, Value = value ?? DBNull.Value };
            if (value is null) parameter.NpgsqlDbType = NpgsqlDbType.Text;
            command.Parameters.Add(parameter);
        }
        return command;
    }
}
