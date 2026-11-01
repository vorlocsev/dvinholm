using System.Diagnostics;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Dvinholm.App;

public sealed partial class MainWindow
{
#if DVINHOLM_DEVELOPMENT_FEATURES
    private bool _cemPinLoadedFromStore;
    private static CemPinStore OpenCemPinStore() => new(Path.Combine(DvinholmUserDataDirectory.Current, "cem-pin-vault"));
#endif
    private void CemPinRangeChanged(object sender, TextChangedEventArgs e)
    {
#if DVINHOLM_DEVELOPMENT_FEATURES
        if (CemPinResultText is null) return;
        ClearDiagnosticReadResult();
        RefreshCemPartReadAvailability();
#endif
    }

    private void RevealCemPin_Click(object sender, RoutedEventArgs e)
    {
#if DVINHOLM_DEVELOPMENT_FEATURES
        if ((!_cemPinLoadedFromStore && _cemPinResultGuard?.IsCurrent != true) || _cemPinShownAt != 0)
        { ClearCemPinMaterial(); return; }
        if (_cemPinResult?.TryReveal(bytes => CemPinResultText.Text =
            (_cemPinResult.Origin == CemPinResultOrigin.Synthetic ? "SYNTHETIC: " :
                _cemPinLoadedFromStore ? CemText("Сохранённый PIN: ", "Saved PIN: ") : "PIN: ") +
            Convert.ToHexString(bytes)) == true)
        {
            _cemPinShownAt = Stopwatch.GetTimestamp();
            CemPinRevealButton.IsEnabled = false;
        }
        else ClearCemPinMaterial();
#endif
    }

#if DVINHOLM_DEVELOPMENT_FEATURES
    private void ClearCemIdentity()
    {
        _cemIdentityGuard?.Dispose();
        _cemIdentityGuard = null;
        _cemIdentity = null;
        Interlocked.Exchange(ref _cemIdentityContext, new object());
    }

    private void ClearCemPinMaterial()
    {
        _cemPinViewTimer?.Stop();
        _cemPinViewTimer = null;
        _cemPinShownAt = 0;
        _cemPinLoadedFromStore = false;
        _cemPinResult?.Dispose();
        _cemPinResult = null;
        _cemPinResultGuard?.Dispose();
        _cemPinResultGuard = null;
        if (CemPinResultText is not null)
            CemPinResultText.Text = CemText("Нет результата PIN.", "No PIN result.");
        if (CemPinRevealButton is not null) CemPinRevealButton.IsEnabled = false;
    }

    private bool TryGetCemPinRange(out ulong first, out ulong count)
    {
        first = count = 0;
        var start = CemPinRangeStart?.Text;
        var limit = CemPinRangeCount?.Text;
        static bool Digits(string? value) => !string.IsNullOrEmpty(value) &&
            value.All(c => c is >= '0' and <= '9');
        return Digits(start) && Digits(limit) &&
            ulong.TryParse(start, NumberStyles.None, CultureInfo.InvariantCulture, out first) &&
            ulong.TryParse(limit, NumberStyles.None, CultureInfo.InvariantCulture, out count) &&
            first < CemPinCandidateRange.SearchSpaceSize && count is > 0 and <= ExperimentalCemPinCanProvider.MaximumBatch &&
            count <= CemPinCandidateRange.SearchSpaceSize - first;
    }

    private void StartCemPinExpiry()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(200);
        timer.Tick += (_, _) =>
        {
            if (_closing || _cemPinResult?.HasPin != true || (!_cemPinLoadedFromStore && _cemPinResultGuard?.IsCurrent != true) ||
                (_cemPinShownAt != 0 && Stopwatch.GetElapsedTime(_cemPinShownAt) >= TimeSpan.FromSeconds(15)))
                ClearCemPinMaterial();
        };
        _cemPinViewTimer = timer;
        timer.Start();
    }
#endif

    private async void ReadCemPin_Click(object sender, RoutedEventArgs e)
    {
#if DVINHOLM_DEVELOPMENT_FEATURES
        if (!CanUseCemPartRoute(true) || _cemIdentityGuard?.IsCurrent != true ||
            _cemIdentity is not { } identity || !ExperimentalCemPinCanProvider.HasSourceProfile(identity.PartNumber) ||
            !TryGetCemPinRange(out var first, out var count) || !await _udsGate.WaitAsync(0)) return;
        var generation = ClearDiagnosticReadResult();
        using var cts = new CancellationTokenSource();
        _udsCts = cts;
        CemPinSessionGuard? guard = null;
        CemPinAttemptResult? result = null;
        var filterEnabled = HardwareFilterButton.IsEnabled;
        var monitorEnabled = StartButton.IsEnabled;
        var cancelLabel = CancelUdsButton.Content;
        try
        {
            SetUdsEnabled(false);
            DiscoverButton.IsEnabled = ConnectButton.IsEnabled = HardwareFilterButton.IsEnabled = StartButton.IsEnabled = false;
            CancelUdsButton.IsEnabled = true;
            CancelUdsButton.Content = CemText("Отмена CEM", "Cancel CEM");
            var consent = new CheckBox { Content = CemText("Я уполномочен выполнять эту процедуру на автомобиле.",
                "I am authorized to perform this procedure on this vehicle.") };
            var content = new StackPanel { Spacing = 10 };
            content.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap,
                Text = CemText("Перебор по согласованному исходнику: до 10000 попыток, максимум 60 секунд. " +
                    "Нужен уже доступный сервисный контекст CEM. Физическая совместимость не проверена. " +
                    "Найденный PIN автоматически сохраняется в зашифрованном хранилище текущего пользователя Windows. " +
                    "Показ PIN временный. Автоматический вход в программирование и сбросы не выполняются.",
                    "Source-defined enumeration: at most 10000 attempts and 60 seconds. Requires an already available CEM service context. " +
                    "Physical compatibility is unverified. Found PINs are saved with Windows current-user encryption. " +
                    "Reveal is temporary. Programming entry and resets are unavailable.") });
            content.Children.Add(consent);
            var dialog = new ContentDialog { XamlRoot = RootLayout.XamlRoot, Content = content,
                Title = CemText("Получение PIN CEM — эксперимент", "CEM PIN source experiment"),
                PrimaryButtonText = CemText("Начать", "Start"), CloseButtonText = CemText("Отмена", "Cancel"),
                DefaultButton = ContentDialogButton.Close, IsPrimaryButtonEnabled = false };
            consent.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
            consent.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || consent.IsChecked != true ||
                !CanUseCemPartRoute(false) || _cemIdentityGuard?.IsCurrent != true ||
                !_udsResultLifetime.CanDisplay(generation)) return;
            var context = new object();
            guard = CemPinSessionGuard.Capture(_session, context,
                () => !Volatile.Read(ref _closing) && _udsResultLifetime.CanDisplay(generation) ? context : null, true);
            var provider = new ExperimentalCemPinCanProvider(_session, guard, identity.PartNumber,
                first, count, true, CemPinResultOrigin.AdapterUnattested);
            SetStatus(CemText("Проверка сервисного контекста и ограниченный поиск PIN CEM…",
                "Checking service context and running bounded CEM PIN search…"));
            var research = new VolvoP2CemServiceResearchContext(null, false, null, null, null,
                VolvoP2ServiceCredentialScope.Unspecified, false, null, null);
            result = await CemPinAttemptCoordinator.ForSourceExperiment(provider).TryRunAsync(
                research, cts.Token, guard, TimeSpan.FromSeconds(60));
            if (_closing || !_udsResultLifetime.CanDisplay(generation)) return;
            if (cts.IsCancellationRequested || !guard.IsCurrent)
            {
                SetStatus(!_session.IsConnected
                    ? CemText("Сессия CEM недоступна. Переподключите адаптер.", "CEM session unavailable. Reconnect the adapter.")
                    : CemText("Операция PIN CEM отменена или контекст изменён.", "CEM PIN operation cancelled or context changed."));
                return;
            }
            if (result.HasPin)
            {
                var saved = false;
                try { OpenCemPinStore().Save(result, identity.PartNumber); saved = true; }
                catch (Exception) { /* Keep the temporary result; never echo storage errors. */ }
                _cemPinResult = result;
                result = null;
                _cemPinResultGuard = guard;
                guard = null;
                CemPinResultText.Text = CemText("Кандидат принят дважды адаптером. PIN скрыт. Физическое происхождение не подтверждено.",
                    "Candidate accepted twice by the adapter. PIN hidden. Physical provenance is unverified.");
                CemPinRevealButton.IsEnabled = true;
                StartCemPinExpiry();
                SetStatus(saved
                    ? CemText("PIN CEM сохранён в зашифрованном хранилище Windows.", "CEM PIN saved with Windows user encryption.")
                    : CemText("PIN CEM получен, но сохранить не удалось. Доступен только временный показ.",
                        "CEM PIN obtained, but saving failed. Only temporary reveal is available."));
            }
            else if (!_session.IsConnected)
                SetStatus(CemText("Ошибка очистки транспорта. Переподключите адаптер.", "Transport cleanup failed. Reconnect the adapter."));
            else if (provider.Failure == CemPinSourceFailure.ServiceContextUnavailable)
                SetStatus(CemText("Сервисный контекст CEM не ответил или номер детали не совпал. Поиск остановлен.",
                    "CEM service context did not reply or its part number differed. Search stopped."));
            else SetStatus(result.Status switch
            {
                CemPinAttemptStatus.FinishedWithoutPin => CemText("Диапазон проверен; принятый PIN не найден.", "Range exhausted; no accepted PIN found."),
                CemPinAttemptStatus.TimedOut => CemText("Истёк срок ожидания CEM. Поиск остановлен.", "CEM deadline expired. Search stopped."),
                CemPinAttemptStatus.Unsupported => CemText("Текущий маршрут CEM недоступен.", "Current CEM route is unavailable."),
                _ => CemText("Операция PIN CEM остановлена; частные данные скрыты.", "CEM PIN operation stopped; private details withheld.")
            });
        }
        catch (Exception)
        {
            if (!_closing) SetStatus(CemText("Операция PIN CEM остановлена; частные данные скрыты.",
                "CEM PIN operation stopped; private details withheld."));
        }
        finally
        {
            result?.Dispose();
            guard?.Dispose();
            _udsCts = null;
            CancelUdsButton.Content = cancelLabel;
            CancelUdsButton.IsEnabled = false;
            DiscoverButton.IsEnabled = _connected && _session.IsConnected && !_closing;
            ConnectButton.IsEnabled = !_closing;
            HardwareFilterButton.IsEnabled = filterEnabled && _session.IsConnected && !_closing;
            StartButton.IsEnabled = monitorEnabled && _session.IsConnected && !_closing;
            _udsGate.Release();
            SetUdsEnabled(_connected && _session.IsConnected && !_closing);
        }
#else
        await Task.CompletedTask;
#endif
    }
}
