using Microsoft.UI.Xaml;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;

namespace Dvinholm.App;

/// <summary>
/// Opt-in staging and integrity verification only. This partial class does not
/// extract files, execute a helper or provide an Install button. Signed release
/// provenance and transaction rollback remain independent mandatory gates.
/// </summary>
public sealed partial class MainWindow
{
    private UnifiedX64UpdateCandidate? _unifiedCandidate;
    private UnifiedX64StagedPackage? _unifiedStagedPackage;
    private CancellationTokenSource? _unifiedStageCts;

    private bool UnifiedNetworkAllowed =>
        !_closing && Environment.Is64BitProcess &&
        !DvinholmUserDataDirectory.IsAcceptanceLaunch;

    private void RefreshUnifiedDownloadButtons()
    {
        if (_closing) return;
        bool checking = _unifiedCheckCts is not null;
        bool staging = _unifiedStageCts is not null;
        UnifiedCheckUpdateButton.IsEnabled = UnifiedNetworkAllowed && !checking && !staging;
        UnifiedStageUpdateButton.IsEnabled = UnifiedNetworkAllowed && !checking && !staging &&
            _unifiedCandidate is not null && _unifiedStagedPackage is null;
        UnifiedCancelStageButton.IsEnabled = staging;
        UnifiedPreflightButton.IsEnabled = UnifiedNetworkAllowed && !checking &&
            !staging && _unifiedStagedPackage is not null;
        UnifiedStageProgress.Visibility = staging ? Visibility.Visible : Visibility.Collapsed;
    }

    private void InvalidateUnifiedCandidate()
    {
        _unifiedCandidate = null;
        _unifiedStageCts?.Cancel();
        DeleteStagedArchive();
        RefreshUnifiedDownloadButtons();
    }

    private void DeleteStagedArchive()
    {
        var completed = _unifiedStagedPackage;
        _unifiedStagedPackage = null;
        if (completed is null) return;
        // The staging service returns a freshly generated pending-GUID path.
        // Never delete an installation directory or a user-selected path.
        var root = Path.GetFullPath(Path.Combine(
            DvinholmUserDataDirectory.Current, "update-staging"));
        var expectedParent = Path.GetDirectoryName(Path.GetFullPath(completed.Directory));
        var leaf = Path.GetFileName(Path.TrimEndingDirectorySeparator(completed.Directory));
        if (!string.Equals(expectedParent, root, StringComparison.OrdinalIgnoreCase) ||
            !System.Text.RegularExpressions.Regex.IsMatch(leaf, "^pending-[a-f0-9]{32}$"))
            return;
        try { Directory.Delete(completed.Directory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void UnifiedPreflight_Click(object sender, RoutedEventArgs e)
    {
        var staged = _unifiedStagedPackage;
        if (!UnifiedNetworkAllowed || _unifiedStageCts is not null ||
            _unifiedCheckCts is not null || staged is null)
            return;

        try
        {
            var backups = Path.Combine(DvinholmUserDataDirectory.Current, "update-backups");
            var plan = UnifiedX64RecoveryPreflight.Inspect(
                AppContext.BaseDirectory, staged, backups);
            if (!plan.CanPlanBackup)
            {
                SetUnifiedStatus(
                    "Проверка резервного копирования заблокирована: " +
                        string.Join(", ", plan.Blockers) + ". Файлы приложения не изменены.",
                    "Backup preflight blocked: " +
                        string.Join(", ", plan.Blockers) + ". No application files changed.");
                return;
            }

            SetUnifiedStatus(
                $"Каталог проверен: {plan.FilesToBackUp} файлов, " +
                    $"{plan.BytesToBackUp:N0} байт для резервной копии. " +
                    "Подпись издателя и механизм отката пока не подтверждены. " +
                    "Резервная копия не создана, установка запрещена.",
                $"Directory audited: {plan.FilesToBackUp} files, " +
                    $"{plan.BytesToBackUp:N0} bytes to back up. " +
                    "Publisher signature and rollback are still unverified. " +
                    "No backup created and installation forbidden.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            SecurityException or ArgumentException or InvalidDataException)
        {
            SetUnifiedStatus(
                "Не удалось проверить подготовку резервной копии. Установка запрещена.",
                "Backup preparation could not be validated. Installation forbidden.");
        }
    }

    private void UnifiedCancelStage_Click(object sender, RoutedEventArgs e)
    {
        _unifiedStageCts?.Cancel();
        UnifiedCancelStageButton.IsEnabled = false;
        SetUnifiedStatus(
            "Отмена загрузки… Частичный пакет будет удалён.",
            "Cancelling download… The partial archive will be deleted.");
    }

    private async void UnifiedStageUpdate_Click(object sender, RoutedEventArgs e)
    {
        var candidate = _unifiedCandidate;
        if (!UnifiedNetworkAllowed || _unifiedCheckCts is not null ||
            _unifiedStageCts is not null || _unifiedStagedPackage is not null ||
            candidate is null || !UnifiedX64StageDownloader.ValidateCandidate(candidate))
            return;

        using var cts = new CancellationTokenSource();
        _unifiedStageCts = cts;
        RefreshUnifiedDownloadButtons();
        SetUnifiedStatus(
            $"Загрузка и проверка x64 ZIP {candidate.Tag}… Установка не выполняется.",
            $"Downloading and verifying x64 ZIP {candidate.Tag}… Nothing will be installed.");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(8) };
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Dvinholm", "0.2.4"));
            var root = Path.Combine(DvinholmUserDataDirectory.Current, "update-staging");
            var staged = await UnifiedX64StageDownloader.StageAsync(
                candidate, root, client, cts.Token);

            if (cts.IsCancellationRequested || _closing ||
                !ReferenceEquals(_unifiedCandidate, candidate))
            {
                // User changed channel or left this stage while the transfer
                // completed; do not retain stale verified bytes.
                _unifiedStagedPackage = staged;
                DeleteStagedArchive();
                return;
            }

            _unifiedStagedPackage = staged;
            SetUnifiedStatus(
                $"Архив {candidate.Tag} скачан в закрытый временный каталог. SHA-256 и AMD64 проверены ({staged.ZipBytes:N0} байт, {staged.EntryCount} файлов). Подлинность издателя и откат ещё не подтверждены. Установка отключена.",
                $"Archive {candidate.Tag} staged privately; SHA-256 and AMD64 checked ({staged.ZipBytes:N0} bytes, {staged.EntryCount} entries). Publisher provenance and rollback are not yet verified. Installation disabled.");
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            if (!_closing)
                SetUnifiedStatus("Загрузка отменена, временные файлы очищены.",
                    "Download cancelled; temporary files removed.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or
            UnauthorizedAccessException or SecurityException or InvalidDataException or
            OperationCanceledException)
        {
            if (!_closing && !cts.IsCancellationRequested)
                SetUnifiedStatus(
                    "Не удалось проверить x64 ZIP. Пакет не сохранён для установки; проверьте сеть и контрольную сумму.",
                    "x64 ZIP verification failed. No installable package retained. Check network and checksum.");
        }
        finally
        {
            if (ReferenceEquals(_unifiedStageCts, cts))
                _unifiedStageCts = null;
            RefreshUnifiedDownloadButtons();
        }
    }
}
