using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Dvinholm.App;

public sealed partial class MainWindow
{
    private static string AuroraUiText(string russian, string english) =>
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? russian : english;

    private void InitializeAuroraLabels()
    {
        if (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "ru") return;
        foreach (var (control, label) in new (Control, string)[]
        {
            (DllPath, "J2534 DLL · 32 бита"), (J2534Devices, "Установленные адаптеры"),
            (BaudRate, "Скорость CAN"), (UdsRequestId, "Адрес запроса · TX"),
            (UdsResponseId, "Адрес ответа · RX"), (CanIdFormat, "Формат CAN ID"),
            (UdsDid, "Параметр DID · hex"), (DtcStatusMaskSelector, "Статус неисправности"),
            (ProfileSearch, "Найти профиль ECU"), (ProfileSelector, "Профиль ECU"), (IdFilter, "Фильтр CAN ID · hex")
        })
        {
            if (control is TextBox text) text.Header = label;
            if (control is ComboBox combo) combo.Header = label;
        }
        foreach (var (button, label) in new (Button, string)[]
        {
            (ConnectButton, "Подключить"), (StartButton, "Начать мониторинг"),
            (ReadDidButton, "Прочитать DID"), (ReadVinButton, "Прочитать VIN"), (ReadDtcButton, "Прочитать DTC"),
            (CancelUdsButton, "Отменить операцию"), (DiscoverButton, "Найти блоки"),
            (CancelDiscoveryButton, "Отменить поиск"), (CreateProfileButton, "Создать профиль"),
            (EditProfileButton, "Изменить профиль"), (CaptureSnapshotButton, "Прочитать и сохранить снимок"),
            (InspectSnapshotButton, "Открыть снимок"), (CompareSnapshotsButton, "Сравнить снимки"),
            (HardwareFilterButton, "Аппаратный фильтр"), (ClearDiagnosticResultButton, "Очистить результат"),
            (P2ConfigCatalogButton, "Открыть каталог вариантов"), (P2PinReviewButton, "Совместимость и условия доступа")
        }) button.Content = label;
        DllPath.PlaceholderText = "Путь к DLL установленного J2534-драйвера";
        J2534Devices.PlaceholderText = "Выберите адаптер";
        ProfileSelector.PlaceholderText = "Выберите профиль";
        ProfileSearch.PlaceholderText = "Название, CAN ID, DID или источник";
        EcuSelector.PlaceholderText = "Наблюдаемый ответ ECU";
        MarkedVerifiedOnly.Content = "Только отмеченные проверенными";
        DiagnosticResultExpander.Header = "Результат чтения · проверить перед передачей";
        foreach (var item in DtcStatusMaskSelector.Items.OfType<ComboBoxItem>())
            item.Content = (item.Tag as string) switch
            {
                "FF" => "Все статусы", "01" => "Тест не пройден", "04" => "Ожидающие подтверждения",
                "08" => "Подтверждённые", "80" => "Требуется индикатор", _ => item.Content
            };
    }
}
