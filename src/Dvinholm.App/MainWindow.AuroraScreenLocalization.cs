using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;

namespace Dvinholm.App;

/// <summary>
/// Explicit locale projection for the six approved Aurora workspaces. Language
/// changes only presentation; they do not connect, scan, read, write, restore
/// snapshots, change profile selection or change update-channel preferences.
/// </summary>
public sealed partial class MainWindow
{
    private void ApplyAuroraPrimaryScreenLocalization()
    {
        WorkspaceScopeLabel.Text = L("DVINHOLM / РАБОЧЕЕ ПРОСТРАНСТВО",
            "DVINHOLM / WORKSPACE");
        SidebarFooterLocale.Text = L("AURORA · ЕДИНОЕ ПРИЛОЖЕНИЕ",
            "AURORA · UNIFIED APPLICATION");

        CemPinResearchTitle.Text = L("Совместимость и доступ",
            "Compatibility and access");
        CemPinResearchIntro.Text = L(
            "Сведения о процедуре и ограничениях текущей реализации.",
            "Procedure evidence and current implementation limits.");
        P2PinReviewButton.Content = L("Условия CEM PIN (только офлайн)",
            "CEM PIN readiness (offline only)");
        ToolTipService.SetToolTip(P2PinReviewButton, L(
            "Только справочная проверка; без поиска PIN, авторизации или запросов к автомобилю.",
            "Evidence checklist only. No PIN attempts, authentication or vehicle requests."));

        EcuToolsTitle.Text = L("Дополнительные инструменты ECU", "ECU tools");
        EcuToolsIntro.Text = L(
            "Проверенные функции не удалены: откройте профиль, монитор CAN или чтение данных из рабочей области ECU.",
            "Use ECU profiles, CAN monitor and verified read-only diagnostics from this workspace.");
        EcuDiagnosticsNavigateButton.Content = L("Диагностические данные", "Diagnostic data");
        EcuMonitorNavigateButton.Content = L("CAN-монитор", "CAN monitor");
        EcuProfilesNavigateButton.Content = L("Профили ECU", "ECU profiles");
        EcuDiscoveryTitle.Text = L("Обнаружение блоков", "ECU discovery");
        EcuDiscoveryIntro.Text = L(
            "Пассивное наблюдение ответов. Выбор записи не подтверждает идентичность блока и не отправляет запрос.",
            "Passive response observations only. Selecting a record does not verify ECU identity or transmit a request.");
        EcuListTitle.Text = L("Ответы ECU", "ECU responses");
        EcuListIntro.Text = L(
            "Выберите наблюдаемый RX. Имена блоков не подтверждены.",
            "Select an observed RX frame. Module names are not verified.");
        EcuListObservedHeader.Text = L("Наблюдаемый RX", "Observed RX");
        EcuListInferredHeader.Text = L("Предполагаемый TX · не подтверждён",
            "Inferred TX · unverified");
        EcuVehicleHeading.Text = L("Карта автомобиля", "Vehicle outline");
        EcuVehicleOrientation.Text = L("ПЕРЕД → ЗАД", "FRONT → REAR");
        EcuObservationHeading.Text = L("Наблюдаемые RX · не позиции ECU",
            "Observed RX · not ECU locations");
        EcuDiagramDisclaimer.Text = L(
            "Схема не указывает физические позиции ECU; местоположение и статус блоков не подтверждаются изображением.",
            "The outline does not mark physical ECU locations or confirm module identity or status.");
        EcuSelectedHeading.Text = L("Выбранный ответ", "Selected response");
        EcuSelectionDisclaimer.Text = L(
            "Нет подтверждённого сопоставления наблюдаемых CAN ID с позициями на автомобиле.",
            "Observed CAN IDs have no verified mapping to physical locations.");
        EcuOpenDiagnosticsButton.Content = L("Диагностика ECU", "ECU diagnostics");
        EcuOpenProfilesButton.Content = L("Перейти к профилям ECU →",
            "Open ECU profiles →");
        EcuDiagramCaption.Text = EcuVehicleModel.SelectedIndex == 1
            ? L("XC70 P3 · условный боковой силуэт, выбран вручную",
                "XC70 P3 · schematic side profile, manually selected")
            : L("XC90 P2 · условный боковой силуэт, выбран вручную",
                "XC90 P2 · schematic side profile, manually selected");
        RefreshAuroraEcuObservationLocalization();
        AutomationProperties.SetName(EcuVehicleModel,
            L("Схема автомобиля", "Vehicle outline selection"));
        ToolTipService.SetToolTip(EcuOpenDiagnosticsButton, L(
            "Переход к диагностике не отправляет запросы к автомобилю.",
            "Navigating to diagnostics does not transmit vehicle requests."));

        DtcPageHeading.Text = L("Ошибки / DTC", "Fault codes / DTC");
        DtcPageIntro.Text = L(
            "Чтение неисправностей одного выбранного ECU. Перед чтением подтвердите адреса TX/RX в разделе «Диагностика».",
            "Read DTCs from one selected ECU. Verify the TX/RX addresses under Diagnostics first.");
        DtcCodeHeading.Text = L("Код DTC", "DTC code");
        DtcDescriptionHeading.Text = L("Описание", "Description");
        DtcStatusHeading.Text = L("Статус", "Status");
        DtcEvidenceDisclaimer.Text = L(
            "Коды появляются только после явного чтения. Подтверждённого каталога описаний Volvo пока нет. Ни одна ошибка здесь не подставляется автоматически.",
            "Codes appear only after an explicit read. No verified Volvo fault description catalog is available. No faults are invented.");
        DtcDetailsHeading.Text = L("Подробности", "Details");
        DtcDetailsEvidence.Text = L(
            "После чтения реальные 24-битные коды DTC, маски статуса и поддерживаемые ECU биты появятся в панели «Результат операции» ниже. Описания причин и рекомендаций требуют проверенного каталога для выбранной модели.",
            "After a read, ECU-supplied 24-bit codes and status masks appear in the diagnostic results panel. Fault causes and repairs require a verified model-specific catalog.");
        DtcCheckAddressesButton.Content = L("Проверить адреса ECU",
            "Verify ECU addresses");
        DtcClearButton.Content = L("Стереть ошибки", "Clear DTCs");
        ToolTipService.SetToolTip(DtcClearButton, L(
            "Стирание отключено: нет проверенного сервиса, доступа и восстановления.",
            "Clearing is disabled: no verified service, authorization or recovery."));
        DtcStatusMaskSelector.Header = L("Фильтр статуса DTC", "DTC status filter");
        foreach (var item in DtcStatusMaskSelector.Items.OfType<ComboBoxItem>())
            item.Content = (item.Tag as string) switch
            {
                "FF" => L("Все статусы", "All status bits"),
                "01" => L("Тест не пройден", "Test failed"),
                "04" => L("Ожидающие подтверждения", "Pending DTC"),
                "08" => L("Подтверждённые", "Confirmed DTC"),
                "80" => L("Требуется индикатор", "Warning indicator requested"),
                _ => item.Content
            };
        ReadDtcButton.Content = L("Прочитать DTC", "Read DTC");
        AutomationProperties.SetName(DtcRecordsView,
            L("Коды неисправностей выбранного ECU",
                "Fault codes from the selected ECU"));
        RefreshAuroraDtcTableLocalization();

        DiagnosticsAddressHeading.Text = L("Адресация выбранного ECU",
            "Selected ECU addressing");
        DiagnosticsAddressIntro.Text = L(
            "Введите проверенные CAN-адреса. Пассивное обнаружение не определяет адрес запроса.",
            "Enter verified CAN IDs. Passive discovery does not identify a request address.");
        DiagnosticsReadHeading.Text = L("Чтение данных", "Read ECU data");
        DiagnosticsReadIntro.Text = L(
            "VIN и диагностические параметры выбранного блока. Ошибки вынесены на отдельный экран.",
            "Read VIN and supported identifiers for the selected ECU. DTCs have a separate workspace.");

        ConnectionPageHeading.Text = L("Интерфейс автомобиля", "Vehicle interface");
        ConnectionPageIntro.Text = L(
            "Выберите установленный J2534-адаптер и параметры канала.",
            "Select a registered J2534 adapter and verify the bus parameters.");
        ConnectionDriverHeading.Text = L("Сведения об адаптере", "Adapter information");
        ConnectionDriverIntro.Text = L("Информация текущего подключения.",
            "Information from the current connection.");
        ConnectionRefreshButton.Content = L("Обновить", "Refresh");
        J2534Devices.Header = L("Установленные адаптеры", "Registered adapters");
        J2534Devices.PlaceholderText = L("Выберите адаптер", "Select adapter");
        BaudRate.Header = L("Скорость CAN", "CAN speed");
        DllPath.Header = Environment.Is64BitProcess
            ? L("J2534 DLL · 64 бита", "J2534 DLL · 64-bit")
            : L("J2534 DLL · 32 бита", "J2534 DLL · 32-bit");
        DllPath.PlaceholderText = L("Путь к DLL установленного J2534-драйвера",
            "Path to the installed J2534 driver DLL");
        UdsRequestId.Header = L("Адрес запроса · TX", "Request CAN ID · TX");
        UdsResponseId.Header = L("Адрес ответа · RX", "Response CAN ID · RX");
        UdsDid.Header = L("Параметр DID · hex", "DID (hex)");
        CanIdFormat.Header = L("Формат CAN ID", "CAN ID format");

        CanMonitorHeading.Text = L("Поток CAN", "CAN traffic");
        CanMonitorIntro.Text = L(
            "Запускайте и останавливайте наблюдение независимо от переходов между разделами.",
            "Start and stop monitoring independently of workspace navigation.");
        IdFilter.Header = L("Фильтр CAN ID · hex", "CAN ID filter (hex)");
        CanMonitorClearButton.Content = L("Очистить", "Clear");
        HardwareFilterButton.Content = _activeHardwareFilterId is null
            ? L("Применить аппаратный фильтр", "Apply HW filter")
            : L("Убрать аппаратный фильтр", "Remove HW filter");
        CanMonitorCsvButton.Content = L("Журнал CSV", "CSV log");
        CanRxSummaryButton.Content = L("Сводка RX", "RX summary");
        CanTraceInspectButton.Content = L("Анализ трассы", "Inspect trace");

        ProfileSearch.Header = L("Найти профиль ECU", "Find ECU profile");
        ProfileSearch.PlaceholderText = L("Название, CAN ID, DID или источник",
            "Name, CAN ID, DID or source");
        ProfileSelector.Header = L("Профиль ECU", "ECU profiles");
        ProfileSelector.PlaceholderText = L("Выберите профиль", "Select profile");
        MarkedVerifiedOnly.Content = L("Только отмеченные проверенными",
            "Marked verified only");
        CreateProfileButton.Content = L("Создать профиль", "New profile");
        EditProfileButton.Content = L("Изменить профиль", "Edit selected");
        ProfileImportButton.Content = L("Импорт JSON", "Import JSON");
        ProfileExportButton.Content = L("Экспорт JSON", "Export JSON");
        CaptureSnapshotButton.Content = L("Прочитать и сохранить снимок",
            "Read + save snapshot");
        InspectSnapshotButton.Content = L("Открыть снимок", "Inspect snapshot");
        CompareSnapshotsButton.Content = L("Сравнить снимки",
            "Compare snapshots");

        StartButton.Content = _monitorTask is null
            ? L("Начать мониторинг", "Start monitor")
            : L("Остановить мониторинг", "Stop monitor");
        ConnectButton.Content = _connected && _session.IsConnected
            ? L("Отключить", "Disconnect")
            : L("Подключить", "Connect");

        DiagnosticResultHeading.Text = L("Результат операции", "Operation result");
        DiagnosticResultIntro.Text = L(
            "Данные отображаются локально. Проверьте содержимое перед передачей.",
            "Data stays local. Review it before sharing.");
        DiagnosticResultExpander.Header = L(
            "Результат чтения · проверить перед передачей",
            "Diagnostic read result · review before sharing");
        ClearDiagnosticResultButton.Content = L("Очистить результат",
            "Clear diagnostic result");
        ReadDidButton.Content = L("Прочитать DID", "Read DID");
        ReadVinButton.Content = L("Прочитать VIN", "Read VIN");
        DiscoverButton.Content = L("Найти блоки", "Discover ECUs");
        CancelDiscoveryButton.Content = L("Отменить поиск", "Cancel scan");
        EcuSelector.PlaceholderText = L("Наблюдаемый ответ ECU",
            "Observed ECU response");
    }
}
