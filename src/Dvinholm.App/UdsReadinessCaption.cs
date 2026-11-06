namespace Dvinholm.App;

/// <summary>
/// Pure RU/EN explanation of the existing fail-closed read-only UDS gate.
/// It does not validate addresses, enable controls or contact an ECU.
/// </summary>
internal static class UdsReadinessCaption
{
    internal static string Text(bool connected, bool ready, bool english)
    {
        // No disconnected state may present a read as ready, even if an
        // inconsistent caller accidentally supplies ready: true.
        if (connected && ready)
            return english
                ? "Read-only UDS address syntax valid. Verify ECU identity and service support before reading."
                : "Синтаксис адресов UDS для чтения корректен. Перед чтением подтвердите идентичность ECU и поддержку сервиса.";

        if (connected)
            return english
                ? "Read-only UDS disabled. Enter two distinct valid source-checked CAN IDs (TX and RX). Passive RX alone is not sufficient."
                : "Чтение UDS отключено. Введите два различных CAN ID (TX и RX), проверенных по надёжному источнику. Одного пассивно наблюдаемого RX недостаточно.";

        return english
            ? "Read-only UDS disabled until connected with manually checked request and response CAN IDs."
            : "Чтение UDS отключено до подключения и ручной проверки CAN-адресов запроса (TX) и ответа (RX).";
    }
}
