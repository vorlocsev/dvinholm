using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dvinholm.App;

namespace Dvinholm.Wpf;

/// <summary>
/// First verified WPF migration stage. Navigation and read-only driver inventory are
/// real. Diagnostic writes, PIN services and self-replacement remain fail-closed
/// until their existing backends are migrated and acceptance-tested.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly (string Key, string Glyph, string Ru, string En, string RuDetail, string EnDetail)[] Sections =
    [
        ("connection", "⌁", "Подключение", "Connection",
         "Поиск и настройка J2534-адаптеров", "Discover and configure J2534 adapters"),
        ("updates", "↻", "Обновление", "Updates",
         "Канал сборок и безопасная установка", "Release channel and safe installation"),
        ("pin", "♙", "CEM PIN", "CEM PIN",
         "Идентификация и исследование CEM PIN", "CEM identification and PIN research"),
        ("configuration", "☷", "Конфигурация CEM", "CEM configuration",
         "Чтение, сравнение и подготовка параметров", "Read, compare and prepare parameters"),
        ("dtc", "△", "Ошибки (DTC)", "Faults (DTC)",
         "Коды неисправностей и описание", "Diagnostic trouble codes and descriptions"),
        ("ecu", "▣", "Блоки (ECU)", "Modules (ECU)",
         "Состав автомобиля и состояние модулей", "Vehicle modules and response status")
    ];

    private readonly Dictionary<string, Grid> _pages;
    private readonly UpdateSourcePreferenceStore _channels;
    private readonly bool _skipPreferenceWrite;
    private bool _initialized;
    private string _language = "RU";
    private string _currentPage = "connection";
    private string _adapterInventoryRu = "Проверка адаптеров не выполнена";
    private string _adapterInventoryEn = "Adapter inventory not checked";

    public MainWindow(bool skipAdapterScan = false, bool skipPreferenceWrite = false)
    {
        _skipPreferenceWrite = skipPreferenceWrite;
        InitializeComponent();

        _pages = new Dictionary<string, Grid>(StringComparer.Ordinal)
        {
            ["connection"] = ConnectionPage,
            ["updates"] = UpdatesPage,
            ["pin"] = PinPage,
            ["configuration"] = ConfigPage,
            ["dtc"] = DtcPage,
            ["ecu"] = EcuPage
        };
        var settingsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dvinholm");
        _channels = new UpdateSourcePreferenceStore(
            Path.Combine(settingsRoot, UpdateSourcePreferenceStore.FileName));

        // A corrupt or unauthorized user preference cannot prevent offline GUI startup.
        var savedChannel = UpdateSourceChannel.Development;
        try { savedChannel = _channels.Load(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        UpdateChannel.SelectedIndex = savedChannel == UpdateSourceChannel.Stable ? 0 : 1;

        _initialized = true;
        PopulateNavigation();
        ShowPage("connection");
        ApplyLanguage();
        if (!skipAdapterScan)
            RefreshAdapterInventory();
    }

    private void PopulateNavigation()
    {
        Navigation.Items.Clear();
        foreach (var section in Sections)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text = section.Glyph, Width = 34, FontSize = 21, Foreground =
                    System.Windows.Media.Brushes.LightSkyBlue
            });
            row.Children.Add(new TextBlock
            {
                Text = _language == "RU" ? section.Ru : section.En,
                VerticalAlignment = VerticalAlignment.Center
            });
            Navigation.Items.Add(new ListBoxItem { Tag = section.Key, Content = row });
        }
        var selectedIndex = Array.FindIndex(Sections, s => s.Key == _currentPage);
        Navigation.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
    }

    private void ShowPage(string key)
    {
        if (!_pages.TryGetValue(key, out var selected)) return;
        _currentPage = key;
        foreach (var page in _pages.Values)
            page.Visibility = ReferenceEquals(page, selected) ? Visibility.Visible : Visibility.Collapsed;
        var section = Sections.Single(s => s.Key == key);
        PageTitle.Text = _language == "RU" ? section.Ru : section.En;
        PageSubtitle.Text = _language == "RU" ? section.RuDetail : section.EnDetail;
    }

    private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        if (Navigation.SelectedItem is ListBoxItem { Tag: string key })
            ShowPage(key);
    }

    private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        _language = LanguageSelector.SelectedIndex == 1 ? "EN" : "RU";
        PopulateNavigation();
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        bool ru = _language == "RU";
        HeaderAdapter.Text = ru ? "J2534: не подключён" : "J2534: disconnected";
        HeaderVehicle.Text = ru ? "Автомобиль: не определён" : "Vehicle: not identified";
        HeaderCem.Text = ru ? "CEM: не прочитан" : "CEM: not read";
        FooterState.Text = ru ? "●  Готово к работе (без подключения)" : "●  Ready (disconnected)";

        // All values remain truthful, unknown or disconnected until actual
        // transport evidence is returned by a migrated and tested service.
        Set(AdapterTitle, ru ? "Выбор адаптера J2534" : "Select J2534 adapter");
        Set(AdapterIntro, ru
            ? "Проверяются реальные регистрации J2534 (32-bit); найденный драйвер ещё не означает подключение к автомобилю."
            : "Registered 32-bit J2534 drivers only. A driver is not a vehicle connection.");
        AdapterInventoryStatus.Text = ru ? _adapterInventoryRu : _adapterInventoryEn;
        RefreshAdaptersButton.Content = ru ? "Обновить список" : "Refresh list";
        Set(CanTitle, ru ? "Параметры подключения" : "Connection parameters");
        Set(CanHint, ru
            ? "Настройки используются только после реализации проверенного транспортного подключения."
            : "These parameters are for the future verified transport connection.");
        ConnectButton.Content = ru ? "Подключиться" : "Connect";
        Set(DriverTitle, ru ? "Драйвер и состояние" : "Driver and status");
        Set(ConnectionStateTitle, ru ? "Автомобиль не подключён" : "Vehicle disconnected");
        Set(ConnectionStateNote, ru
            ? "Доступны обнаружение драйверов и подготовка параметров. Сетевой обмен с ECU на этом этапе заблокирован."
            : "Only driver discovery and parameter preparation are available. ECU transport is disabled.");
        UpdateDriverDetails();

        Set(InstalledTitle, ru ? "Текущая версия" : "Current version");
        Set(VersionNote, ru
            ? "Демонстрационная сборка нового интерфейса. Удалённая версия не проверялась."
            : "WPF interface preview. No remote release has been checked.");
        Set(ChannelTitle, ru ? "Канал обновлений" : "Update channel");
        Set(ChannelNote, ru
            ? "Выбор сохраняется локально. Загрузка и установка до миграции проверенного обновлятора недоступны."
            : "Channel is saved locally. Download and install require the verified updater migration.");
        Set(SafetyTitle, ru ? "Проверка и безопасность" : "Integrity and safety");
        Set(SafetyNote, ru
            ? "Обновления не запрашивались. SHA-256, подлинность пакета, резервная копия и откат будут подключены к проверенному механизму."
            : "No update request has been made. Hash verification, backup and rollback are pending migration.");
        CheckUpdateButton.Content = ru ? "Проверить обновления" : "Check updates";
        InstallUpdateButton.Content = ru ? "Установить" : "Install";

        Set(PinIdentityTitle, ru ? "1. Идентификация CEM" : "1. CEM identification");
        Set(PinIdentity, ru
            ? "P/N: не прочитан\nHW: не прочитан\nSW: не прочитан"
            : "P/N: not read\nHW: not read\nSW: not read");
        Set(PinProcessTitle, ru ? "2. Поиск PIN → 3. Результат" : "2. PIN search → 3. Result");
        Set(PinProcessNote, ru
            ? "Пока не подтверждены протокол, ревизия CEM и безопасный сервисный доступ, поиск PIN недоступен. Никакой фиктивный прогресс не отображается."
            : "PIN operations require verified ECU protocol, revision and authorized service access. No simulated progress.");
        PinStartButton.Content = ru ? "Начать поиск PIN" : "Start PIN search";
        Set(CategoriesTitle, ru ? "Категории" : "Categories");
        Set(CategoriesList, ru ? "Основные\n\nКомфорт\n\nОсвещение\n\nАудио\n\nБезопасность"
            : "General\n\nComfort\n\nLighting\n\nAudio\n\nSecurity");
        Set(ParameterTitle, ru ? "Параметры CEM" : "CEM parameters");
        Set(ConfigEmpty, ru ? "Нет прочитанных и подтверждённых параметров."
            : "No verified configuration parameters have been read.");
        Set(ParameterDetailTitle, ru ? "Описание параметра" : "Parameter description");
        Set(ParameterDetail, ru
            ? "Выберите параметр после чтения конфигурации. Доступ к записи закрыт до проверки совместимости и создания резервной копии."
            : "Choose a parameter after configuration readback. Writing requires compatibility and backup verification.");
        CemWriteButton.Content = ru ? "Записать в CEM" : "Write to CEM";
        Set(DtcListTitle, ru ? "Ошибки блоков управления" : "ECU fault codes");
        DtcClearButton.Content = ru ? "Стереть ошибки" : "Clear fault codes";
        Set(DtcEmpty, ru ? "DTC не считаны. Автомобиль не подключён."
            : "No DTC readback. Vehicle disconnected.");
        Set(EcuListTitle, ru ? "Список ECU" : "ECU inventory");
        Set(EcuEmpty, ru ? "Нет обнаруженных ECU." : "No discovered ECUs.");
        Set(EcuTopology, ru
            ? "Топология станет доступна после подтверждённого чтения ECU."
            : "Vehicle topology will appear after verified ECU discovery.");
        Set(EcuDetailsTitle, ru ? "Модуль" : "Module");
        Set(EcuDetails, ru
            ? "Модуль не выбран. Данные и команды доступны только после идентификации."
            : "No ECU selected. Data and commands require verified identification.");
        ShowPage(_currentPage);
    }

    private static void Set(TextBlock block, string value) => block.Text = value;

    private void RefreshAdapters_Click(object sender, RoutedEventArgs e) => RefreshAdapterInventory();

    private void RefreshAdapterInventory()
    {
        AdapterList.ItemsSource = null;
        try
        {
            var devices = J2534DeviceDiscovery.FindRegisteredDevices();
            AdapterList.ItemsSource = devices;
            _adapterInventoryRu = devices.Count == 0
                ? "J2534-драйверы не обнаружены."
                : $"Зарегистрировано J2534-драйверов: {devices.Count}.";
            _adapterInventoryEn = devices.Count == 0
                ? "No registered J2534 drivers found."
                : $"Registered J2534 drivers: {devices.Count}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                   or System.Security.SecurityException)
        {
            _adapterInventoryRu = "Не удалось прочитать реестр J2534.";
            _adapterInventoryEn = "Failed to read J2534 registry.";
        }
        AdapterInventoryStatus.Text = _language == "RU" ? _adapterInventoryRu : _adapterInventoryEn;
        UpdateDriverDetails();
    }

    private void AdapterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initialized) UpdateDriverDetails();
    }

    private void UpdateDriverDetails()
    {
        if (AdapterList.SelectedItem is J2534Device device)
            DriverDetails.Text = _language == "RU"
                ? $"{device.Label}\nDLL зарегистрирована. Связь с автомобилем не проверена."
                : $"{device.Label}\nDLL registered. Vehicle connection not verified.";
        else
            DriverDetails.Text = _language == "RU"
                ? "Нет выбранного J2534-адаптера."
                : "No J2534 adapter selected.";
    }

    private void UpdateChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _skipPreferenceWrite) return;
        var choice = UpdateChannel.SelectedIndex == 0
            ? UpdateSourceChannel.Stable : UpdateSourceChannel.Development;
        try
        {
            _channels.Save(choice);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, _language == "RU"
                    ? "Не удалось сохранить канал обновлений."
                    : "Could not save update channel.",
                "Dvinholm", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Actual constructed WPF controls, never a synthetic success state.</summary>
    public void AssertEightOfflineContracts()
    {
        var checks = 0;
        static void Require(bool value, string why)
        {
            if (!value) throw new InvalidOperationException(why);
        }

        Require(Navigation.Items.Count == 6 && Sections.Length == 6,
            "Expected exactly six navigation entries.");
        checks++;
        Require(!Sections.Any(s => s.Ru.Contains("Настройки", StringComparison.OrdinalIgnoreCase)
                                || s.En.Contains("Settings", StringComparison.OrdinalIgnoreCase)),
            "Removed Settings page returned.");
        checks++;
        Require(_pages.Count == 6 && _pages.Values.Distinct().Count() == 6,
            "WPF pages are missing or aliased.");
        checks++;
        foreach (var key in _pages.Keys)
        {
            ShowPage(key);
            Require(_pages.Values.Count(p => p.Visibility == Visibility.Visible) == 1
                 && _pages[key].Visibility == Visibility.Visible,
                "Page navigation does not isolate content: " + key);
        }
        checks++;
        Require(!ConnectButton.IsEnabled && !PinStartButton.IsEnabled
            && !CemWriteButton.IsEnabled && !DtcClearButton.IsEnabled
            && !CheckUpdateButton.IsEnabled && !InstallUpdateButton.IsEnabled,
            "Unverified transport, PIN, write or update action enabled.");
        checks++;
        Require(HeaderAdapter.Text.Contains("не подключ", StringComparison.Ordinal)
            && HeaderVehicle.Text.Contains("не определ", StringComparison.Ordinal)
            && HeaderCem.Text.Contains("не прочитан", StringComparison.Ordinal),
            "Startup presented fictional vehicle evidence.");
        checks++;
        _language = "EN"; PopulateNavigation(); ApplyLanguage();
        Require(Navigation.Items.Count == 6 && PageTitle.Text == "Modules (ECU)"
            && HeaderAdapter.Text == "J2534: disconnected",
            "RU/EN shell navigation or status did not translate.");
        checks++;
        Require(AdapterList.Items.Count == 0 && DtcGrid.Items.Count == 0
            && ConfigGrid.Items.Count == 0 && EcuList.Items.Count == 0,
            "Smoke mode fabricated registered devices, DTCs, CEM values or ECUs.");
        checks++;
        Require(checks == 8, "Incorrect contract count.");
        Console.WriteLine("Dvinholm WPF constructed shell: 8/8 offline contracts passed.");
    }
}
