# CEM PIN — подготовка физической приёмки

Статус: офлайн-шаблон. Реальное чтение CEM P/N, PIN и проверки адаптера не выполнялись.

Сценарий eng/Prepare-CemPinBenchAcceptance.ps1 запускается в Windows PowerShell 5.1.
Он проверяет ZIP с одним корневым Dvinholm.exe, вычисляет SHA-256 архива и создаёт
в новом каталоге вне репозитория JSON с восемью пунктами NotTested.
Программа не извлекается и не запускается; к J2534, CAN и автомобилю обращений нет.

Пример из корня клона PR #702:

    & '.\eng\Prepare-CemPinBenchAcceptance.ps1' -BuildZip 'C:\Review\Dvinholm-Windows-x64.zip' -CommitSha '<EXACT_HEAD_40_HEX>' -OutputDirectory 'C:\Dvinholm-CemBench-Review-01' -IConfirmOfflineNoSecrets

CommitSha — введённый оператором SHA, а не независимо проверенная привязка ZIP
к исходному коммиту. Происхождение exact-head сборки проверять отдельно.
Полученный cem-pin-bench-acceptance.json не содержит локальных путей, VIN, PIN,
номеров адаптера, сырых CAN-кадров или идентификаторов драйвера.

Восемь независимых gates:

1. ExactHeadBuildAndDriver — исходники, x64 пакет и версия драйвера.
2. OwnerConsentAndServiceEntitlement — согласие и сервисное право.
3. IndependentCemHardwareSoftwareIdentity — независимое HW/SW CEM.
4. DocumentedReadOnlyTransportAndPartNumber — документированный безопасный P/N маршрут.
5. VerifiedResponseSourceAndRepeatability — повторяемость и проверка источника.
6. CancellationAndTimeoutOnBench — отмена и срок ожидания на стенде.
7. DisconnectAndFilterCleanupOnBench — отключение и очистка фильтров на стенде.
8. NoSecretsInLogsAndUiRecovery — секреты не попали в логи, восстановление GUI.

Все пункты изначально NotTested. Хеш ZIP, сборка и синтетические тесты никогда
не меняют их автоматически на Passed. Самостоятельно отмеченные пользователем
результаты не становятся криптографической или физической аттестацией CEM.

Документ не разрешает подбор PIN, программирование или запись конфигурации.
Stable и Virtual Car остаются без изменений.