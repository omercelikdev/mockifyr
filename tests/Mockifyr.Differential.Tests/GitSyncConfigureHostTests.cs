using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Server;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Host-level self-test for dashboard-configured Git sync (#151, no oracle — WireMock has no Git
/// surface): a completely flag-less host is connected to a bare repository over
/// <c>POST /__admin/git/configure</c>, pushes, and a RESTARTED flag-less host adopts the working
/// copy (found at the default location) and serves the synced stub. Requires the git binary.
/// </summary>
public sealed class GitSyncConfigureHostTests : IDisposable
{
    private const string StubJson =
        """{"request":{"method":"GET","url":"/cfg"},"response":{"status":200,"body":"configured"}}""";

    private readonly DirectoryInfo _base = Directory.CreateTempSubdirectory("mockifyr-gitcfg-");

    public void Dispose() => _base.Delete(recursive: true);

    [Fact]
    public async Task DashboardConfigure_Pushes_AndSurvivesARestart()
    {
        var bare = Path.Combine(_base.FullName, "remote.git");
        Directory.CreateDirectory(bare);
        RunGit(bare, "init", "--bare", "-b", "main");
        var workDir = Path.Combine(_base.FullName, "work");

        // Host 1: flag-less (in-memory) apart from the test's working-copy override.
        await using (var host = await StartAsync(workDir))
        {
            using var client = Client(host);

            // Unconfigured status advertises the resolved working copy; push refuses with guidance.
            using var before = await client.GetAsync("/__admin/git/status");
            var beforeJson = await before.Content.ReadAsStringAsync();
            Assert.Contains("\"configured\":false", beforeJson);
            using var refusedPush = await client.PostAsync("/__admin/git/push", content: null);
            Assert.Equal(HttpStatusCode.NotFound, refusedPush.StatusCode);

            // A stub created BEFORE the connect must be snapshotted, not lost.
            using var stub = new StringContent(StubJson, Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsync("/__admin/mappings", stub)).StatusCode);

            using var configureBody = new StringContent(
                $$"""{"remoteUrl":{{System.Text.Json.JsonSerializer.Serialize(bare)}},"branch":"main"}""",
                Encoding.UTF8, "application/json");
            using var configured = await client.PostAsync("/__admin/git/configure", configureBody);
            var configuredJson = await configured.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, configured.StatusCode);
            Assert.Contains("\"configured\":true", configuredJson);
            Assert.Contains("\"configuredBy\":\"repository\"", configuredJson);

            using var push = await client.PostAsync("/__admin/git/push", content: null);
            Assert.Equal(HttpStatusCode.OK, push.StatusCode);
            Assert.Contains("\"pushed\":true", await push.Content.ReadAsStringAsync());
        }

        Assert.Contains("mappings/", RunGit(bare, "ls-tree", "-r", "--name-only", "main"));

        // Host 2: same flag-less start — it must ADOPT the working copy and serve the stub.
        await using (var restarted = await StartAsync(workDir))
        {
            using var client = Client(restarted);
            using var served = await client.GetAsync("/cfg");
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);
            Assert.Equal("configured", await served.Content.ReadAsStringAsync());

            using var status = await client.GetAsync("/__admin/git/status");
            var json = await status.Content.ReadAsStringAsync();
            Assert.Contains("\"configured\":true", json);
            Assert.Contains("\"configuredBy\":\"repository\"", json);
        }
    }

    private static async Task<WebApplication> StartAsync(string workDir)
    {
        var app = MockifyrHost.Build(["--port", "0", "--git-work-dir", workDir]);
        await app.StartAsync();
        return app;
    }

    private static HttpClient Client(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses
            .First(a => a.StartsWith("http://", StringComparison.Ordinal))
            .Replace("[::]", "127.0.0.1").Replace("0.0.0.0", "127.0.0.1");
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static string RunGit(string workDir, params string[] args)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', args)} failed: {stderr}");
        return stdout;
    }
}
