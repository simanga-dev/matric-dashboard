using System.Diagnostics;
using MatricDasbhoard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MatricDasbhoard.Component.Tests.Fixtures;

/// <summary>Disposable isolated PostGIS server. Never connects to a configured application database.</summary>
public sealed class PostgisFixture : IAsyncLifetime
{
    private readonly string _name = "matric-spatial-tests-" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; private set; } = string.Empty;
    internal MatricDasbhoardDbContext Context() => new(new DbContextOptionsBuilder<MatricDasbhoardDbContext>()
        .UseNpgsql(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        if (!DockerAvailable()) return;
        await DockerAsync("run", "--detach", "--rm", "--name", _name, "-e", "POSTGRES_PASSWORD=spatial-test-password",
            "-p", "127.0.0.1::5432", "postgis/postgis:18-3.6-alpine");
        var port = (await DockerAsync("port", _name, "5432/tcp")).Trim().Split(':').Last();
        ConnectionString = $"Host=127.0.0.1;Port={port};Username=postgres;Password=spatial-test-password;Database=postgres;Timeout=3";
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(ConnectionString);
                await connection.OpenAsync();
                break;
            }
            catch (NpgsqlException) when (attempt < 59) { await Task.Delay(500); }
        }
        await using var context = Context();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrEmpty(ConnectionString)) await DockerAsync("stop", _name);
    }

    internal static bool DockerAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "info --format {{.ServerVersion}}")
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
            return process is not null && process.WaitForExit(5000) && process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    private static async Task<string> DockerAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Docker.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException(await error);
        return await output;
    }
}

/// <summary>Explicitly skips PostGIS integration tests if Docker is unavailable.</summary>
public sealed class PostgisFactAttribute : FactAttribute
{
    public PostgisFactAttribute()
    {
        if (!PostgisFixture.DockerAvailable()) Skip = "Docker is required for isolated PostGIS integration tests.";
    }
}
