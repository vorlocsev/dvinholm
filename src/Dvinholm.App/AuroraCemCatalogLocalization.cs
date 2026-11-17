namespace Dvinholm.App;

/// <summary>
/// UI-only bilingual labels for the exact, immutable fourteen-entry CEM
/// research manifest. Canonical IDs, categories, provenance and vehicle
/// capabilities are never translated, mutated or inferred.
/// </summary>
internal sealed record AuroraCemCategoryChoice(string? CanonicalCategory, string Label)
{
    public override string ToString() => Label;
}

internal static class AuroraCemCatalogLocalization
{
    internal static IReadOnlyList<AuroraCemCategoryChoice> Categories(bool english)
    {
        var categories = VolvoP2CemConfigOptionManifest.Entries
            .Select(option => option.Category)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(category => category, StringComparer.Ordinal)
            .Select(category => new AuroraCemCategoryChoice(
                category, CategoryLabel(category, english)));
        return Array.AsReadOnly(
            new[] { new AuroraCemCategoryChoice(null,
                    english ? "All categories" : "Все категории") }
                .Concat(categories).ToArray());
    }

    internal static string CategoryLabel(string category, bool english)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (english) return category switch
        {
            "AuxiliaryHeating" => "Auxiliary heating",
            "ExteriorLighting" => "Exterior lighting",
            "Region" => "Market and region",
            "ParkingAssistance" => "Parking assistance",
            "LockingAndAlarm" => "Locking and alarm",
            "Maintenance" => "Maintenance",
            "WheelAndTyre" => "Wheels and tyres",
            "VehicleSpeedPolicy" => "Speed limit policy",
            "XC90 CEM service menu" => "XC90 CEM service menu",
            _ => category
        };
        return category switch
        {
            "AuxiliaryHeating" => "Дополнительный отопитель",
            "ExteriorLighting" => "Наружное освещение",
            "Region" => "Рынок и регион",
            "ParkingAssistance" => "Помощь при парковке",
            "LockingAndAlarm" => "Замки и сигнализация",
            "Maintenance" => "Техническое обслуживание",
            "WheelAndTyre" => "Колёса и шины",
            "VehicleSpeedPolicy" => "Ограничение скорости",
            "XC90 CEM service menu" => "Сервисное меню CEM XC90",
            _ => category // Unrecognized category is unverified, not guessed.
        };
    }

    internal static string OptionLabel(
        VolvoP2CemConfigOptionDefinition option, bool english)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (english) return option.DisplayName;
        return option.CanonicalId switch
        {
            "p2.xc90.auxiliary-heater" => "Дополнительный отопитель",
            "p2.xc90.headlamp-washers" => "Омыватели фар",
            "p2.xc90.market-region" => "Рынок / регион",
            "p2.xc90.parking-assistance" => "Помощь при парковке",
            "p2.xc90.automatic-locking" => "Автоматическое запирание",
            "p2.xc90.service-interval" => "Сервисный интервал",
            "p2.xc90.wheel-size" => "Размер колёс",
            "p2.xc90.speed-policy" => "Политика ограничения скорости",
            "xc90.oem.cem.homesafe-lighting" => "Освещение Home Safe",
            "xc90.oem.cem.daytime-running-lamps" => "Дневные ходовые огни",
            "xc90.oem.cem.tailgate-wiper" => "Стеклоочиститель задней двери",
            "xc90.oem.cem.fuel-filler-lock-delay" => "Задержка блокировки лючка топливного бака",
            "xc90.oem.cem.bixenon-lowbeam-position-warning" =>
                "Предупреждение о положении ближнего света Bi-Xenon",
            "xc90.oem.cem.three-flash-indicator" =>
                "Трёхкратное мигание указателя поворота",
            _ => option.DisplayName
        };
    }
}
