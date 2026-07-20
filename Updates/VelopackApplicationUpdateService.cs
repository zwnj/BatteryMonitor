using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace BatteryMonitor.Updates;

internal sealed class VelopackApplicationUpdateService : IApplicationUpdateService
{
    private readonly UpdateManager manager;
    private UpdateInfo? pendingUpdate;

    public VelopackApplicationUpdateService(
        string repositoryUrl,
        bool includePrereleases = false)
    {
        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("A valid HTTPS repository URL is required.", nameof(repositoryUrl));
        }

        manager = new UpdateManager(
            new GithubSource(uri.ToString().TrimEnd('/'), accessToken: null, prerelease: includePrereleases));
    }

    public string CurrentVersionText
    {
        get
        {
            string? version = manager.CurrentVersion?.ToString();
            if (!string.IsNullOrWhiteSpace(version))
            {
                return FormatVersion(version);
            }

            Assembly? assembly = Assembly.GetEntryAssembly();
            string? informationalVersion = assembly?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                string normalized = informationalVersion.Split(
                    '+',
                    StringSplitOptions.RemoveEmptyEntries)[0];
                return FormatVersion(normalized);
            }

            return "v?";
        }
    }

    public async Task<ApplicationUpdateCheckResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        pendingUpdate = null;

        if (!manager.IsInstalled)
        {
            return new ApplicationUpdateCheckResult(false, false, null);
        }

        pendingUpdate = await manager.CheckForUpdatesAsync().WaitAsync(cancellationToken);
        return new ApplicationUpdateCheckResult(
            true,
            pendingUpdate is not null,
            pendingUpdate?.TargetFullRelease.Version.ToString());
    }

    public async Task DownloadAsync(
        IProgress<int> progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progress);
        UpdateInfo update = pendingUpdate ??
            throw new InvalidOperationException("No checked update is ready to download.");

        cancellationToken.ThrowIfCancellationRequested();
        await manager.DownloadUpdatesAsync(update, progress.Report, cancellationToken);
    }

    public void ApplyAndRestart()
    {
        UpdateInfo update = pendingUpdate ??
            throw new InvalidOperationException("No downloaded update is ready to apply.");

        manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }

    private static string FormatVersion(string version) =>
        version.StartsWith('v') ? version : $"v{version}";
}
