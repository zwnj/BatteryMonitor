using System;
using System.Threading.Tasks;
using System.Windows;

using BatteryMonitor.Helpers;

using Velopack;
using Velopack.Sources;

namespace BatteryMonitor.Services
{
    public sealed class UpdateService
    {
        private readonly string _repositoryUrl;
        private readonly string? _accessToken;
        private readonly bool _includePrerelease;

        public UpdateService(string repositoryUrl, bool includePrerelease = false, string? accessToken = null)
        {
            _repositoryUrl = repositoryUrl;
            _includePrerelease = includePrerelease;
            _accessToken = accessToken;
        }

        private UpdateManager CreateManager()
        {
            return new UpdateManager(new GithubSource(_repositoryUrl, _accessToken, _includePrerelease));
        }

        public async Task CheckForUpdatesSilentlyAsync()
        {
            try
            {
                var manager = CreateManager();
                if (!manager.IsInstalled)
                {
                    Logger.Info("Update check skipped: app is not installed");
                    return;
                }

                var updateInfo = await manager.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    Logger.Info("Update check: no updates available");
                    return;
                }

                Logger.Info($"Update check: available version {updateInfo.TargetFullRelease.Version}");
            }
            catch (Exception ex)
            {
                Logger.Error("Update check failed", ex);
            }
        }

        public async Task<bool> CheckPromptAndApplyAsync(Window? owner = null)
        {
            try
            {
                var manager = CreateManager();
                if (!manager.IsInstalled)
                {
                    ShowMessage(owner, "インストール版として起動したときにのみ更新できます。", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }

                var updateInfo = await manager.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    ShowMessage(owner, "最新バージョンです。", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    Logger.Info("Manual update check: no updates available");
                    return false;
                }

                var versionText = updateInfo.TargetFullRelease.Version.ToString();
                Logger.Info($"Manual update check: update available {versionText}");

                var result = owner == null
                    ? MessageBox.Show($"更新版 {versionText} が見つかりました。ダウンロードして再起動しますか？", "更新", MessageBoxButton.YesNo, MessageBoxImage.Question)
                    : MessageBox.Show(owner, $"更新版 {versionText} が見つかりました。ダウンロードして再起動しますか？", "更新", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    Logger.Info("Manual update check: user canceled");
                    return false;
                }

                Logger.Info("Update download started");
                await manager.DownloadUpdatesAsync(updateInfo, progress =>
                {
                    Logger.Info($"Update download progress: {progress}%");
                });

                Logger.Info("Applying downloaded update and restarting");
                manager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Manual update flow failed", ex);
                ShowMessage(owner, $"更新に失敗しました。\n{ex.Message}", "更新", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static void ShowMessage(Window? owner, string message, string caption, MessageBoxButton button, MessageBoxImage image)
        {
            if (owner == null)
            {
                MessageBox.Show(message, caption, button, image);
                return;
            }

            MessageBox.Show(owner, message, caption, button, image);
        }
    }
}
