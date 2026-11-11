using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace ApplicationFoundry.Tests;

public sealed class HttpSmokeTests
{
    [Fact]
    public async Task First_launch_creates_database_and_second_launch_reuses_it()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "application-foundry-tests", Guid.NewGuid().ToString("N"));
        const string dataDirectoryVariable = "ApplicationFoundry__DataDirectory";
        var previousDataDirectory = Environment.GetEnvironmentVariable(dataDirectoryVariable);
        try
        {
            Environment.SetEnvironmentVariable(dataDirectoryVariable, dataDirectory);
            for (var launch = 0; launch < 2; launch++)
            {
                using var factory = new WebApplicationFactory<Program>();
                using var client = factory.CreateClient();
                var response = await client.GetAsync("/health");
                response.EnsureSuccessStatusCode();
                Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
            }

            var databasePath = Path.Combine(dataDirectory, "app.db");
            Assert.True(File.Exists(databasePath));
            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'AspNetUsers'";
            Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
        }
        finally
        {
            Environment.SetEnvironmentVariable(dataDirectoryVariable, previousDataDirectory);
            if (Directory.Exists(dataDirectory)) Directory.Delete(dataDirectory, recursive: true);
        }
    }
}
