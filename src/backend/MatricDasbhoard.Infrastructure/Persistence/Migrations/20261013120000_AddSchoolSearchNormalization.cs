using MatricDasbhoard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MatricDasbhoard.Infrastructure.Persistence.Migrations;

/// <summary>
/// Installs PostgreSQL's trusted unaccent extension for literal, accent-insensitive school search.
/// The extension lives in public so queries do not depend on the connection search path.
/// </summary>
[DbContext(typeof(MatricDasbhoardDbContext))]
[Migration("20261013120000_AddSchoolSearchNormalization")]
public sealed class AddSchoolSearchNormalization : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent WITH SCHEMA public;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // An extension can be shared by other database consumers. Retain it on rollback.
    }
}
