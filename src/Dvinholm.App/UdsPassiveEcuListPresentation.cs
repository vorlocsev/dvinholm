using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Presentation of passive UDS response observations. A response frame is
/// not proof of a particular ECU module or an authorized request address.
/// All results remain read-only and no guessed request ID is sent from here.
/// </summary>
internal static class UdsPassiveEcuListPresentation
{
    internal static string SelectorLabel(UdsEcuCandidate candidate, bool complete, bool english = true)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var observed = Hex(candidate.ResponseId, candidate.IsExtended);
        if (!english)
        {
            var prefixRu = complete ? "Наблюдение" : "Частичное наблюдение";
            var requestRu = candidate.RequestId is uint known
                ? $"запрос 0x{Hex(known, candidate.IsExtended)} (ПРЕДПОЛОЖЕНИЕ)"
                : "запрос НЕИЗВЕСТЕН";
            return $"{prefixRu}: ответ 0x{observed} ({(candidate.IsExtended ? "29-bit" : "11-bit")}); {requestRu}";
        }

        var prefix = complete ? "Observed" : "Partial observation";
        var request = candidate.RequestId is uint id
            ? $"request 0x{Hex(id, candidate.IsExtended)} (INFERRED)"
            : "request UNKNOWN";
        return $"{prefix}: response 0x{observed} ({(candidate.IsExtended ? "29-bit" : "11-bit")}); {request}";
    }

    internal static string ScanSummary(int count, bool complete, bool english = true)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (!english)
        {
            if (!complete)
                return $"НЕПОЛНО: наблюдалось {count} возможных ID ответа. Выбор отключён; повторите сканирование.";
            return count == 0
                ? "Ответы UDS не обнаружены. Идентичность ECU не установлена."
                : $"Наблюдалось {count} возможных ID ответа. Выберите один; адреса запроса не подтверждены.";
        }

        if (!complete)
            return $"INCOMPLETE: {count} response ID candidate(s) observed. Selection disabled; repeat scan.";
        return count == 0
            ? "No UDS response IDs observed. No ECU identity established."
            : $"{count} response ID candidate(s) observed. Choose one; request IDs are not verified.";
    }

    internal static string SelectionDetails(UdsEcuCandidate candidate, bool english = true)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.ResponseCount < 0 || candidate.MaxDataLength is < 0)
            throw new ArgumentOutOfRangeException(nameof(candidate));

        var response = Hex(candidate.ResponseId, candidate.IsExtended);
        var request = candidate.RequestId is uint id
            ? "0x" + Hex(id, candidate.IsExtended) + " (INFERRED ONLY)"
            : "UNKNOWN";
        if (!english)
        {
            var requestRu = candidate.RequestId is uint known
                ? "0x" + Hex(known, candidate.IsExtended) + " (ТОЛЬКО ПРЕДПОЛОЖЕНИЕ)"
                : "НЕИЗВЕСТЕН";
            return new StringBuilder()
                .AppendLine("ПАССИВНОЕ НАБЛЮДЕНИЕ ОТВЕТА ECU (НЕ ИДЕНТИФИКАЦИЯ ECU)")
                .Append("Наблюдаемый ID ответа: 0x").AppendLine(response)
                .Append("Формат CAN: ").AppendLine(candidate.IsExtended ? "29-bit" : "11-bit")
                .Append("Предполагаемый ID запроса: ").AppendLine(requestRu)
                .Append("Наблюдаемых кадров: ")
                .AppendLine(candidate.ResponseCount.ToString(CultureInfo.InvariantCulture))
                .Append("Максимальная длина данных CAN: ")
                .Append(candidate.MaxDataLength?.ToString(CultureInfo.InvariantCulture) ?? "неизвестно")
                .AppendLine(" байт")
                .AppendLine("При пассивном сканировании активные запросы ECU не отправлялись.")
                .AppendLine("Реальная идентичность блока и адрес запроса НЕ ПОДТВЕРЖДЕНЫ.")
                .AppendLine("ID запроса остаётся ПУСТЫМ. Перед активным чтением UDS адрес")
                .AppendLine("нужно проверить отдельно и ввести вручную; выбор записи не отправляет запрос.")
                .AppendLine("Это не идентификация CEM/ECM, PIN, конфигурация или резервная копия.")
                .ToString();
        }

        var result = new StringBuilder()
            .AppendLine("PASSIVE ECU RESPONSE OBSERVATION (NOT ECU IDENTIFICATION)")
            .Append("Observed response ID: 0x").AppendLine(response)
            .Append("CAN format: ").AppendLine(candidate.IsExtended ? "29-bit" : "11-bit")
            .Append("Suggested request ID: ").AppendLine(request)
            .Append("Observed response frames: ")
            .AppendLine(candidate.ResponseCount.ToString(CultureInfo.InvariantCulture))
            .Append("Maximum observed CAN data length: ")
            .Append(candidate.MaxDataLength?.ToString(CultureInfo.InvariantCulture) ?? "unknown")
            .AppendLine(" bytes")
            .AppendLine("No active ECU request was sent during this passive scan.")
            .AppendLine("The responder's actual module identity and request address are UNVERIFIED.")
            .AppendLine("Request ID remains BLANK. Independently verify and enter it manually")
            .AppendLine("before any active read-only UDS operation; selecting a row does not transmit.")
            .AppendLine("This is not a CEM/ECM identity, PIN, configuration or backup.");
        return result.ToString();
    }

    internal static string Hex(uint id, bool extended) =>
        id.ToString(extended ? "X8" : "X3", CultureInfo.InvariantCulture);
}
