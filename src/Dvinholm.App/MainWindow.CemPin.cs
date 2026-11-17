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
    private bool _cemPartPrerequisitesInspected;
#if DVINHOLM_DEVELOPMENT_FEATURES
    private bool _cemPinAttemptActive;
#endif

    // Display-only three-step state. Neither this method nor its presenter
    // sends requests, reveals bytes, reads the vault or changes the session.
    private void RefreshCemPinProgress()
    {
        if (CemPinStepIdentity is null || CemPinStepResearch is null ||
            CemPinStepResult is null)
            return;
        var snapshot = new CemPinProgressSnapshot(
            Development: false, SessionConnected: false, SessionReady: false,
            PartNumberObserved: false, SourceProfileKnown: false, RangeValid: false,
            AttemptRunning: false, CandidateAvailable: false, FromSaved: false,
            Revealed: false);
#if DVINHOLM_DEVELOPMENT_FEATURES
        var observed = _cemIdentityGuard?.IsCurrent == true && _cemIdentity is { };
        snapshot = new CemPinProgressSnapshot(
            Development: !_closing && _demoTimer is null &&
                !DvinholmUserDataDirectory.IsAcceptanceLaunch,
            SessionConnected: _connected && _session.IsConnected,
            SessionReady: CanUseCemPartRoute(requireIdle: true),
            PartNumberObserved: observed,
            SourceProfileKnown: observed && ExperimentalCemPinCanProvider.HasSourceProfile(
                _cemIdentity!.PartNumber),
            RangeValid: TryGetCemPinRange(out _, out _),
            AttemptRunning: _cemPinAttemptActive,
            CandidateAvailable: _cemPinResult?.HasPin == true &&
                (_cemPinLoadedFromStore || _cemPinResultGuard?.IsCurrent == true),
            FromSaved: _cemPinLoadedFromStore,
            Revealed: _cemPinShownAt != 0);
#endif
        var copy = CemPinProgressPresenter.Format(snapshot, _unifiedEnglish);
        CemPinStepIdentity.Text = copy.Identity;
        CemPinStepResearch.Text = copy.Search;
        CemPinStepResult.Text = copy.Result;
        RefreshCemPinExperimentReadiness();
    }

    // The inspector reads existing session flags and the in-memory guarded
    // P/N observation. It neither opens a driver nor enumerates PIN bytes.
    private void RefreshCemPinExperimentReadiness()
    {
        if (CemPinExperimentReadinessText is null) return;
        var prereqs = InspectCemReadOnlyPrerequisites();
#if DVINHOLM_DEVELOPMENT_FEATURES
        var observed = _cemIdentityGuard?.IsCurrent == true && _cemIdentity is { };
        var knownSourceProfile = observed &&
            ExperimentalCemPinCanProvider.HasSourceProfile(_cemIdentity!.PartNumber);
        var validRange = TryGetCemPinRange(out _, out _);
        var busy = _cemPinAttemptActive;
#else
        const bool observed = false;
        const bool knownSourceProfile = false;
        const bool validRange = false;
        const bool busy = false;
#endif
        var assessment = CemPinExperimentReadiness.Assess(
            prereqs, observed, knownSourceProfile, validRange, busy);
        CemPinExperimentReadinessText.Text =
            CemPinExperimentReadiness.Format(assessment, _unifiedEnglish);
    }

    /// <summary>
    /// Relabel the Development-only PIN research panel without resetting a
    /// result, revealing credentials, changing an attempt range or sending
    /// J2534 traffic. This is called for each RU/EN switch.
    /// </summary>
    private void RefreshCemDevelopmentLabels()
    {
        CemPinSearchHeading.Text = L("02  ·  Экспериментальный поиск PIN",
            "02  ·  Experimental PIN search");
        CemPinSearchIntro.Text = L(
            "Только Development. Результат эксперимента не подтверждает PIN физического CEM.",
            "Development only. An experimental result does not verify a physical CEM PIN.");
#if DVINHOLM_DEVELOPMENT_FEATURES
        CanConnectionMode.Header = L("ID канала", "Channel IDs");
        CemPartNumberReadButton.Content = L(
            "Прочитать номер CEM (экспериментальный HS-CAN)",
            "Read CEM P/N (experimental HS-CAN)");
        CemPinRangeStart.Header = L("Начальная позиция перебора", "Search starting position");
        CemPinRangeCount.Header = L("Лимит попыток (1–10000)", "Attempt limit (1–10000)");
        CemPinReadButton.Content = L("Найти PIN · эксперимент", "Read CEM PIN (experimental)");
        CemPinRevealButton.Content = L("Показать на 15 секунд", "Reveal for 15 seconds");
        CemPinSavedButton.Content = L("Сохранённые PIN", "Saved PINs");
        ToolTipService.SetToolTip(CemPartNumberReadButton, L(
            "Экспериментальное чтение номера CEM только в Development. Требуется проверенный канал 500 кбит/с с 29-битными ID; монитор и пользовательские фильтры должны быть отключены. Идентичность блока не подтверждена.",
            "Experimental Development-only CEM part-number read. Requires a verified 500 kbit/s, 29-bit channel with monitor and user filters stopped. ECU identity remains unverified."));
        ToolTipService.SetToolTip(CemPinReadButton, L(
            "Экспериментальный поиск PIN. Физическая совместимость и корректность результата не подтверждены.",
            "Experimental PIN research. Physical compatibility and result validity are unverified."));
        ToolTipService.SetToolTip(CemPinRevealButton, L(
            "Показывает локальный результат на ограниченное время. Не отправляет данные автомобилю.",
            "Temporarily reveals a local result. Does not transmit to the vehicle."));
        ToolTipService.SetToolTip(CemPinSavedButton, L(
            "Локально сохранённые PIN. Не подтверждают идентичность подключённого CEM.",
            "Locally stored PINs. They do not verify connected CEM identity."));
#endif
        RefreshCemPinExperimentReadiness();
    }

    private CemReadOnlyPrerequisiteAssessment InspectCemReadOnlyPrerequisites()
    {
        // No CAN/J2534 calls. This is just a momentary snapshot of the
        // session state already owned by MainWindow; no PIN material is used.
#if DVINHOLM_DEVELOPMENT_FEATURES
        var safeResearchContext = !_closing && _demoTimer is null &&
            !DvinholmUserDataDirectory.IsAcceptanceLaunch;
#else
        // The experimental CEM P/N route is excluded from Stable builds.
        const bool safeResearchContext = false;
#endif
        return CemReadOnlyPrerequisiteReport.Assess(new(
            Connected: _connected && _session.IsConnected,
            Baud500K: _session.ConnectedBaudRate == 500_000,
            Extended29Bit: _session.ConnectedExtended == true,
            HardwareCanFilters: _session.AdapterCapabilities?.HardwareCanFilters == true,
            NoUserFilter: _activeHardwareFilterId is null,
            MonitorStopped: _monitorTask is null,
            DiagnosticGateIdle: _udsGate.CurrentCount != 0 && _discoveryCts is null,
            SafeResearchContext: safeResearchContext));
    }

    private void CheckCemPrerequisites_Click(object sender, RoutedEventArgs e)
    {
        if (_closing) return;
        _cemPartPrerequisitesInspected = true;
        RefreshCemPrerequisitePresentation();
        CemPartPrerequisiteExpander.IsExpanded = true;
        // Not even a read-only CEM request is sent by this callback.
    }

    private void RefreshCemPrerequisitePresentation()
    {
        if (CemPartPrerequisiteButton is null) return;
        CemPartPrerequisiteButton.Content = L(
            "Проверить условия локально", "Inspect prerequisites locally");
        // Single authoritative CEM tooltip for both locale changes and
        // the explicit local-prerequisite action; no vehicle request is made.
        ToolTipService.SetToolTip(CemPartPrerequisiteButton, L(
            "Только локальная проверка состояния приложения, без CAN или J2534 запросов.",
            "Inspect local application state only. No CAN or J2534 requests."));
        CemPartPrerequisiteExpander.Header = L(
            "Результат проверки (без запросов)", "Preflight result (no requests)");
        CemPartPrerequisiteText.Text = _cemPartPrerequisitesInspected
            ? CemReadOnlyPrerequisiteReport.Format(
                InspectCemReadOnlyPrerequisites(), _unifiedEnglish)
            : L("Нажмите «Проверить условия локально». Это не подтверждает доступ к CEM.",
                "Select local prerequisite inspection. This does not establish CEM access.");
    }

    /// <summary>
    /// The two visible Aurora CEM workspaces share one payload-free status.
    /// Never use a profile label or DID snapshot as authenticated CEM identity.
    /// On every refresh, re-evaluate the original session guard; no value is
    /// persisted, sent to the vehicle or inserted into the activity log.
    /// </summary>
    private void RefreshCemIdentityEvidencePresentation()
    {
        if (CemIdentityEvidencePinText is null ||
            CemIdentityEvidenceConfigText is null)
            return;
        CemPinIdentitySectionTitle.Text = L(
            "01  ·  Идентификация CEM", "01  ·  CEM identification");
        CemPinIdentitySectionIntro.Text = L(
            "Идентификация CEM пока не подтверждена. Локальная проверка не обращается к автомобилю; экспериментальное чтение доступно только в Development.",
            "CEM identity is not yet verified. Local inspection does not contact the vehicle; experimental reading is Development-only.");
#if DVINHOLM_DEVELOPMENT_FEATURES
        uint? observed = _cemIdentity?.PartNumber;
        var guardCurrent = _cemIdentityGuard?.IsCurrent == true;
        var developmentContext = !_closing && _demoTimer is null &&
            !DvinholmUserDataDirectory.IsAcceptanceLaunch;
#else
        uint? observed = null;
        const bool guardCurrent = false;
        const bool developmentContext = false;
#endif
        var evidence = CemIdentityEvidenceReport.Assess(
            observed, guardCurrent, !_closing && _connected && _session.IsConnected,
            developmentContext);
        var text = CemIdentityEvidenceReport.Format(evidence, _unifiedEnglish);
        CemIdentityEvidencePinText.Text = text;
        CemIdentityEvidenceConfigText.Text = text;
        RefreshCemPinProgress();
    }

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
            RefreshCemPinProgress();
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
        RefreshCemIdentityEvidencePresentation();
    }

    /// <summary>
    /// Discard an already-revealed PIN as soon as the WinUI window loses focus.
    /// No transport access: clearing wipes the owned secret and the TextBlock.
    /// A hidden, unopened vault record is not touched by focus changes.
    /// </summary>
    private void OnCemPinWindowActivationChanged(bool windowDeactivated)
    {
        if (!windowDeactivated || _cemPinShownAt == 0)
            return;
        ClearCemPinMaterial();
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
        RefreshCemPinProgress();
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
            _cemPinAttemptActive = true;
            RefreshCemPinProgress();
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
                CemPinStore? store = null;
                CemPinSavedEntry? savedEntry = null;
                try
                {
                    // A second Dvinholm process can hold the PIN vault mutex
                    // for up to 15 seconds. Do not block WinUI's dispatcher:
                    // cancellation/closing and lease invalidation must remain
                    // responsive while DPAPI and encrypted file I/O finish.
                    store = OpenCemPinStore();
                    var candidate = result;
                    var partNumber = identity.PartNumber;
                    savedEntry = await Task.Run(() => store.Save(candidate, partNumber));
                    saved = true;
                }
                catch (Exception) { /* Never echo private storage errors. */ }

                // A cancellation, disconnect, identity edit or window close
                // may have arrived while Save was awaiting the vault mutex.
                // Do not promote a revoked result into a revealable UI state.
                // Keep the diagnostic gate until the worker has terminated.
                if (_closing || !_udsResultLifetime.CanDisplay(generation) ||
                    cts.IsCancellationRequested || guard.IsCurrent != true || !result.HasPin)
                {
                    // Cancellation may land just after Save's final lease check,
                    // but before this WinUI continuation runs. Delete only the
                    // UUID returned by this Save; never touch older vault entries.
                    // Keep the diagnostic gate until rollback has terminated.
                    if (savedEntry is not null && store is not null)
                    {
                        try
                        {
                            var abandonedId = savedEntry.Id;
                            var rollbackStore = store;
                            await Task.Run(() => rollbackStore.Delete(abandonedId));
                        }
                        catch (Exception) { /* Rollback is best effort; redact private file errors. */ }
                    }

                    if (!_closing && _udsResultLifetime.CanDisplay(generation))
                        SetStatus(!_session.IsConnected
                            ? CemText("Сессия CEM недоступна. Переподключите адаптер.",
                                "CEM session unavailable. Reconnect the adapter.")
                            : CemText("Операция PIN CEM отменена или контекст изменён.",
                                "CEM PIN operation cancelled or context changed."));
                    return;
                }
                _cemPinResult = result;
                result = null;
                _cemPinResultGuard = guard;
                guard = null;
                CemPinResultText.Text = CemText("Кандидат принят дважды адаптером. PIN скрыт. Физическое происхождение не подтверждено.",
                    "Candidate accepted twice by the adapter. PIN hidden. Physical provenance is unverified.");
                CemPinRevealButton.IsEnabled = true;
                StartCemPinExpiry();
                RefreshCemPinProgress();
                SetStatus(saved
                    ? CemText("Непроверенный кандидат PIN CEM сохранён в зашифрованном хранилище Windows.", "Unverified CEM PIN candidate saved with Windows user encryption.")
                    : CemText("Кандидат PIN CEM принят адаптером, но сохранить не удалось. Результат не подтверждён; доступен только временный показ.",
                        "Adapter accepted a CEM PIN candidate, but saving failed. Result is unverified; temporary reveal only."));
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
            _cemPinAttemptActive = false;
            _udsCts = null;
            CancelUdsButton.Content = cancelLabel;
            RefreshCancellationButtonLanguage();
            CancelUdsButton.IsEnabled = false;
            DiscoverButton.IsEnabled = _connected && _session.IsConnected && !_closing;
            ConnectButton.IsEnabled = !_closing;
            HardwareFilterButton.IsEnabled = filterEnabled && _session.IsConnected && !_closing;
            StartButton.IsEnabled = monitorEnabled && _session.IsConnected && !_closing;
            _udsGate.Release();
            SetUdsEnabled(_connected && _session.IsConnected && !_closing);
            RefreshCemPinProgress();
        }
#else
        await Task.CompletedTask;
#endif
    }
}
