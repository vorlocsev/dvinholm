using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Dvinholm.App;

public sealed partial class MainWindow : Window
{
    private sealed class Frame
    {
        public uint Timestamp { get; init; }
        public string Id { get; init; } = "";
        public string Type { get; init; } = "";
        public uint Dlc { get; init; }
        public string Data { get; init; } = "";
    }

    private readonly ObservableCollection<Frame> _frames = new();
    private readonly ObservableCollection<J2534Device> _j2534Devices = new();
    private readonly J2534Session _session = new();
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private CanRxDispatcher.Subscription? _monitorSubscription;
    private bool _connected;
    private Task? _disconnectTask;
    private Task? _connectTask;
    private bool _closing;
    private int _total;
    private readonly Stopwatch _rate = new();
    private readonly SemaphoreSlim _udsGate = new(1, 1);
    private CancellationTokenSource? _discoveryCts;
    private Task<IReadOnlyList<UdsEcuCandidate>>? _discoveryTask;
    private CancellationTokenSource? _udsCts;
    private readonly ObservableCollection<EcuRow> _ecus = new();
    private EcuProfileCatalog _profileCatalog = new();
    private bool _loadingProfiles;
    private int _rateFrames;
    private uint? _activeHardwareFilterId;
    private LatestBuildInfo? _latestBuild;
    private bool _updaterBusy;
    private bool _updateAvailable;

    public MainWindow()
    {
        InitializeComponent();
        LoadSavedBaudRate();
        BaudRate.SelectionChanged += BaudRate_SelectionChanged;
        DllPath.Text = LoadSavedDllPath();
        DllPath.TextChanged += DllPath_TextChanged;
        Title = LatestBuildUpdater.CurrentWindowTitle;
        LoadSavedProfileCatalog();
        FramesView.ItemsSource = _frames;
        EcusView.ItemsSource = _ecus;
        J2534Devices.ItemsSource = _j2534Devices;
        RefreshJ2534Devices(updateStatus: false);
        UpdateStatusText.Text = $"Current: {LatestBuildUpdater.CurrentDisplayVersion} · select Check update to look for a new build."; 
        _session.ReadError += ex => DispatcherQueue.TryEnqueue(() =>
        {
            var detail = J2534Native.SanitizeDriverDiagnostic(ex.Message);
            SetStatus(string.IsNullOrWhiteSpace(detail)
                ? "Read error: unexpected adapter error."
                : "Read error: " + detail);
        });
        Closed += (_, _) =>
        {
            _closing = true;

            // Request cancellation synchronously, then perform all waits away
            // from the UI thread. RunUdsOperationAsync releases _udsGate from
            // its UI-context continuation, so synchronously waiting for the gate
            // here would prevent that continuation from running until timeout.
            _discoveryCts?.Cancel();
            _udsCts?.Cancel();

            var trackedTasks = new Task?[]
            {
                _discoveryTask,
                _monitorTask,
                _disconnectTask,
                _connectTask
            };
            StopMonitor();

            _ = Task.Run(async () =>
            {
                // Observe every task without letting one fault skip the others.
                // Keep shutdown bounded if a vendor driver never returns.
                var completionTasks = trackedTasks
                    .Where(task => task is not null)
                    .Select(task => ObserveCompletionAsync(task!))
                    .ToArray();

                try
                {
                    await Task.WhenAll(completionTasks)
                        .WaitAsync(TimeSpan.FromSeconds(2))
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    // An active worker may still own native resources. Process
                    // shutdown will reclaim them rather than racing Dispose.
                    return;
                }

                bool udsStopped;
                try
                {
                    udsStopped = await _udsGate.WaitAsync(TimeSpan.FromSeconds(2))
                        .ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                if (!udsStopped)
                    return;

                _udsGate.Release();

                // Never unload the native DLL until tracked operations and the
                // UDS gate have both drained. Dispose may itself wait on a
                // vendor read that ignores its timeout, so it stays off the UI.
                _session.Dispose();
            });
        };
    }

    private static async Task ObserveCompletionAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Shutdown only needs to know that the operation has stopped.
            // Its operational error is already handled by the owning workflow.
        }
    }

    private static string FormatSafeException(Exception exception, string fallback)
    {
        var detail = J2534Native.SanitizeDriverDiagnostic(exception.Message);
        return string.IsNullOrWhiteSpace(detail) ? fallback : detail;
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updaterBusy)
            return;

        if (_updateAvailable && _latestBuild is not null)
        {
            await DownloadAndLaunchLatestBuildAsync(_latestBuild);
            return;
        }

        await CheckLatestBuildAsync();
    }

    private async Task CheckLatestBuildAsync()
    {
        if (_updaterBusy)
            return;

        _updaterBusy = true;
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "Checking…";
        UpdateStatusText.Text = $"Current: {LatestBuildUpdater.CurrentDisplayVersion} · checking latest successful CI build across all branches…";

        try
        {
            var latest = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync();
            _latestBuild = latest;
            var currentSha = LatestBuildUpdater.CurrentCommitSha;
            _updateAvailable = LatestBuildUpdater.IsUpdateAvailable(currentSha, latest);

            UpdateStatusText.Text = LatestBuildUpdater.FormatUpdateStatus(
                LatestBuildUpdater.CurrentDisplayVersion,
                latest,
                _updateAvailable);
            UpdateButton.Content = _updateAvailable ? "Replace files and restart" : "Check update";
        }
        catch (Exception)
        {
            _latestBuild = null;
            _updateAvailable = false;
            UpdateStatusText.Text = $"Current: {LatestBuildUpdater.CurrentDisplayVersion} · Latest CI build: unavailable";
            UpdateButton.Content = "Retry update check";
        }
        finally
        {
            _updaterBusy = false;
            UpdateButton.IsEnabled = true;
        }
    }

    private async Task DownloadAndLaunchLatestBuildAsync(LatestBuildInfo build)
    {
        _updaterBusy = true;
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "Updating…";
        var progress = new Progress<string>(message =>
        {
            DispatcherQueue.TryEnqueue(() => SetStatus(message));
        });

        try
        {
            var stagingDirectory = await LatestBuildUpdater.DownloadAndPrepareAsync(build, progress);
            SetStatus($"Replacing application files with {build.DisplayVersion}…");
            LatestBuildUpdater.StartReplacement(stagingDirectory, AppContext.BaseDirectory, Environment.ProcessId);
            Close();
        }
        catch (Exception ex)
        {
            var detail = FormatSafeException(ex, "Unexpected update error.");
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Dvinholm",
                "update.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.WriteAllText(logPath,
                    $"Update failed at {DateTimeOffset.Now:O}{Environment.NewLine}{ex}",
                    new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            UpdateButton.Content = "Retry update";
            UpdateStatusText.Text = $"Current: {LatestBuildUpdater.CurrentDisplayVersion} · Available: {build.DisplayVersion}";
            SetStatus($"Update failed: {detail} See %LOCALAPPDATA%\\Dvinholm\\update.log for details.");
            _updateAvailable = true;
        }
        finally
        {
            _updaterBusy = false;
            UpdateButton.IsEnabled = true;
        }
    }


    private static string ProfileCatalogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Dvinholm",
        "ecu-profiles.json");

    private void SetProfileCatalog(EcuProfileCatalog catalog)
    {
        _profileCatalog = catalog;
        _loadingProfiles = true;
        try
        {
            ProfileSelector.ItemsSource = _profileCatalog.All;
            ProfileSelector.SelectedIndex = -1;
            ClearProfileInputs();
            EcuInfoText.Text = "No ECU profile selected. Choose a profile to view its metadata.";
        }
        finally
        {
            _loadingProfiles = false;
        }
    }

    private void LoadSavedProfileCatalog()
    {
        var path = ProfileCatalogPath;
        if (!File.Exists(path))
            return;

        try
        {
            var catalog = new EcuProfileCatalogStore(path).Load();
            SetProfileCatalog(catalog);
            SetStatus($"Loaded {catalog.All.Count} saved ECU profile(s).");
        }
        catch (Exception)
        {
            SetStatus("Saved ECU profiles could not be loaded. Check the local file and its permissions.");
        }
    }

    private static void SaveProfileCatalog(EcuProfileCatalog catalog) =>
        new EcuProfileCatalogStore(ProfileCatalogPath).Save(catalog);

    private async void ImportProfiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.FileTypeFilter.Add(".json");
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            var json = await File.ReadAllTextAsync(file.Path);
            var imported = EcuProfileCatalog.FromJson(json);
            SetProfileCatalog(imported);

            try
            {
                SaveProfileCatalog(imported);
                SetStatus($"Imported and saved {imported.All.Count} ECU profile(s) from {file.Name}. Select a profile to apply its CAN IDs.");
            }
            catch (Exception)
            {
                SetStatus($"Imported {imported.All.Count} profile(s), but could not save the local copy. Check folder permissions and available disk space.");
            }
        }
        catch (Exception)
        {
            _loadingProfiles = false;
            SetStatus("Profile import failed. Check that the selected file is valid JSON and can be read.");
        }
    }

    private async void ExportProfiles_Click(object sender, RoutedEventArgs e)
    {
        if (_profileCatalog.All.Count == 0)
        {
            SetStatus("There are no ECU profiles to export. Import a JSON catalog first.");
            return;
        }

        try
        {
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.SuggestedFileName = "ecu-profiles";
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;

            await File.WriteAllTextAsync(file.Path, _profileCatalog.ToJson());
            SetStatus($"Exported {_profileCatalog.All.Count} ECU profile(s) to {file.Name}.");
        }
        catch (Exception)
        {
            SetStatus("Profile export failed. Check destination permissions and available disk space.");
        }
    }

    private void ClearProfileInputs()
    {
        // A cleared catalog/selection must not leave CAN IDs or a DID from a
        // previously selected profile looking like they belong to no profile.
        UdsRequestId.Text = string.Empty;
        UdsResponseId.Text = string.Empty;
        UdsDid.Text = string.Empty;
        CanIdFormat.SelectedIndex = -1;
    }

    private void ProfileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingProfiles)
            return;

        if (ProfileSelector.SelectedItem is not EcuProfile profile)
        {
            ClearProfileInputs();
            EcuInfoText.Text = "No ECU profile selected.";
            return;
        }

        UdsRequestId.Text = profile.RequestId.ToString(profile.IsExtended ? "X8" : "X3");
        UdsResponseId.Text = profile.ResponseId.ToString(profile.IsExtended ? "X8" : "X3");
        CanIdFormat.SelectedIndex = profile.IsExtended ? 1 : 0;
        UdsDid.Text = profile.ReadableDataIdentifiers.Count > 0
            ? profile.ReadableDataIdentifiers.Order().First().ToString("X4")
            : string.Empty;

        EcuInfoText.Text = $"Profile: {profile.Name} ({profile.Id}) · {(profile.IsVerified ? "verified" : "unverified draft")}" +
            Environment.NewLine + $"Source: {profile.Source}" +
            Environment.NewLine + $"Candidate readable DIDs: {(profile.ReadableDataIdentifiers.Count == 0 ? "none listed" : string.Join(", ", profile.ReadableDataIdentifiers.Order().Select(did => did.ToString("X4"))))}" +
            Environment.NewLine + "Profile metadata does not guarantee that the ECU supports a listed DID.";
        SetStatus($"Applied profile '{profile.Name}'. No diagnostic request was sent.");
    }


    private static string CanBaudRateSettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Dvinholm",
        "can-baud-rate.txt");

    private void LoadSavedBaudRate()
    {
        uint baudRate;
        try
        {
            baudRate = new CanBaudRateStore(CanBaudRateSettingsFile).Load();
        }
        catch (IOException)
        {
            baudRate = CanBaudRateStore.DefaultBaudRate;
        }
        catch (UnauthorizedAccessException)
        {
            baudRate = CanBaudRateStore.DefaultBaudRate;
        }

        foreach (var item in BaudRate.Items.OfType<ComboBoxItem>())
        {
            if (uint.TryParse(item.Tag?.ToString(), out var itemBaudRate) && itemBaudRate == baudRate)
            {
                BaudRate.SelectedItem = item;
                return;
            }
        }

        BaudRate.SelectedIndex = 0;
    }

    private void BaudRate_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BaudRate.SelectedItem is not ComboBoxItem item ||
            !uint.TryParse(item.Tag?.ToString(), out var baudRate))
            return;

        try
        {
            new CanBaudRateStore(CanBaudRateSettingsFile).Save(baudRate);
        }
        catch (IOException)
        {
            // Keep the UI responsive; Connect will still validate the selected rate.
        }
        catch (UnauthorizedAccessException)
        {
            // Settings are a convenience, not a prerequisite for connecting.
        }
    }

    private static string DllPathSettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Dvinholm",
        "j2534-dll-path.txt");

    private static string LoadSavedDllPath()
    {
        try
        {
            return new J2534DllPathStore(DllPathSettingsFile).Load();
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private void DllPath_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            new J2534DllPathStore(DllPathSettingsFile).Save(DllPath.Text);
        }
        catch (IOException)
        {
            // Keep the UI responsive; Connect will still validate the path.
        }
        catch (UnauthorizedAccessException)
        {
            // Settings are a convenience, not a prerequisite for connecting.
        }
    }

    private void RefreshJ2534Devices_Click(object sender, RoutedEventArgs e) =>
        RefreshJ2534Devices(updateStatus: true);

    private void RefreshJ2534Devices(bool updateStatus)
    {
        var selectedPath = (J2534Devices.SelectedItem as J2534Device)?.DllPath;
        var devices = J2534DeviceDiscovery.FindRegisteredDevices();

        _j2534Devices.Clear();
        foreach (var device in devices)
            _j2534Devices.Add(device);

        var selectedIndex = selectedPath is null
            ? -1
            : _j2534Devices.ToList().FindIndex(device =>
                string.Equals(device.DllPath, selectedPath, StringComparison.OrdinalIgnoreCase));

        J2534Devices.SelectedIndex = selectedIndex;
        if (selectedIndex < 0 && devices.Count == 1)
            J2534Devices.SelectedIndex = 0;

        if (updateStatus)
        {
            SetStatus(devices.Count == 0
                ? "No registered J2534 DLL found. You can still enter the DLL path manually."
                : $"Found {devices.Count} registered J2534 adapter(s). Select one or enter a DLL path manually.");
        }
    }

    private void J2534Device_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (J2534Devices.SelectedItem is J2534Device device)
            DllPath.Text = device.DllPath;
    }


    private sealed class EcuRow
    {
        public UdsEcuCandidate Candidate { get; }
        public string Response => Candidate.ResponseId.ToString(Candidate.IsExtended ? "X8" : "X3");
        public string Request => Candidate.RequestId?.ToString(Candidate.IsExtended ? "X8" : "X3") ?? "-";
        public string RequestNote => Candidate.RequestIdNote;
        public string Type => Candidate.IsExtended ? "29-bit" : "11-bit";
        public int Frames => Candidate.ResponseCount;
        public string MaxPayload => Candidate.MaxDataLength?.ToString() ?? "-";

        internal EcuRow(UdsEcuCandidate candidate) => Candidate = candidate;
    }

    private async void Discover_Click(object sender, RoutedEventArgs e)
    {
        if (!_connected) return;

        if (_discoveryCts is not null) return;

        // ECU discovery and active UDS requests share the same CAN bus.
        // Serialize them so passive discovery cannot overlap a diagnostic request.
        if (!await _udsGate.WaitAsync(0))
        {
            DiscoveryStatus.Text = "UDS request is already running";
            return;
        }

        var cts = new CancellationTokenSource();
        _discoveryCts = cts;
        DiscoverButton.IsEnabled = false;
        CancelDiscoveryButton.IsEnabled = true;
        ConnectButton.IsEnabled = false;
        SetUdsEnabled(false);
        DiscoveryStatus.Text = "Scanning...";
        _ecus.Clear();
        EcuSelector.Items.Clear();

        try
        {
            var discovery = new UdsEcuDiscovery(_session.Bus);
            _discoveryTask = discovery.ScanAsync(TimeSpan.FromSeconds(8), cts.Token);
            var found = await _discoveryTask;

            ShowDiscoveredEcus(found);
            DiscoveryStatus.Text = found.Count == 0
                ? "No UDS responses observed"
                : $"{found.Count} ECU(s) found";

            SetStatus("ECU discovery complete.");
        }
        catch (UdsEcuDiscoveryCancelledException ex)
        {
            ShowDiscoveredEcus(ex.Candidates);
            DiscoveryStatus.Text = ex.Candidates.Count == 0
                ? "Cancelled · no ECUs observed"
                : $"Cancelled · {ex.Candidates.Count} ECU(s) observed; scan incomplete";
            SetStatus(ex.Message);
        }
        catch (UdsEcuDiscoveryIncompleteException ex)
        {
            ShowDiscoveredEcus(ex.Candidates);
            DiscoveryStatus.Text = $"Incomplete · {ex.Candidates.Count} ECU(s) observed · {ex.DroppedFrameCount} frame(s) dropped";
            SetStatus(ex.Message);
        }
        catch (OperationCanceledException)
        {
            // Cancellation before the scan worker starts has no observations
            // to preserve.
            DiscoveryStatus.Text = "Cancelled · no scan results available";
        }
        catch (Exception ex)
        {
            DiscoveryStatus.Text = "Failed";
            SetStatus("ECU discovery failed: " + FormatSafeException(ex, "unexpected diagnostic error."));
        }
        finally
        {
            _discoveryTask = null;
            _discoveryCts = null;
            cts.Dispose();
            CancelDiscoveryButton.IsEnabled = false;
            DiscoverButton.IsEnabled = _connected;
            SetUdsEnabled(_connected);
            ConnectButton.IsEnabled = true;
            _udsGate.Release();
        }
    }

    private void ShowDiscoveredEcus(IReadOnlyList<UdsEcuCandidate> found)
    {
        _ecus.Clear();
        EcuSelector.Items.Clear();

        foreach (var candidate in found)
        {
            var row = new EcuRow(candidate);
            _ecus.Add(row);
            EcuSelector.Items.Add(row.Candidate.RequestId is null
                ? $"{row.Request} → {row.Response} (request ID unknown)"
                : $"{row.Request} → {row.Response} (inferred)");
        }

        if (found.Count > 0)
        {
            EcusView.SelectedIndex = 0;
            EcuSelector.SelectedIndex = 0;
        }
    }

    private void CancelDiscovery_Click(object sender, RoutedEventArgs e)
    {
        _discoveryCts?.Cancel();
        DiscoveryStatus.Text = "Cancelling...";
        CancelDiscoveryButton.IsEnabled = false;
    }

    private void Ecu_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EcusView.SelectedItem is not EcuRow row)
            return;

        ApplyEcuSelection(row, EcusView.SelectedIndex);
    }

    private void EcuSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = EcuSelector.SelectedIndex;
        if (index < 0 || index >= _ecus.Count)
            return;

        EcusView.SelectedIndex = index;
        ApplyEcuSelection(_ecus[index], index);
    }

    private void ApplyEcuSelection(EcuRow row, int index)
    {
        // A candidate without a supported inferred request ID must not leave
        // the previous ECU's request ID in the input field.
        UdsRequestId.Text = row.Candidate.RequestIdText;
        UdsResponseId.Text = row.Candidate.ResponseId.ToString(row.Candidate.IsExtended ? "X8" : "X3");
        CanIdFormat.SelectedIndex = row.Candidate.IsExtended ? 1 : 0;

        if (EcuSelector.SelectedIndex != index)
            EcuSelector.SelectedIndex = index;

        EcuInfoText.Text =
            $"Request: {(row.Candidate.RequestId is uint ? $"0x{row.Candidate.RequestId.Value:X}" : "unknown")}  ·  " +
            $"Response: 0x{row.Candidate.ResponseId:X}  ·  " +
            $"Type: {(row.Candidate.IsExtended ? "29-bit" : "11-bit")}  ·  " +
            $"Observed frames: {row.Candidate.ResponseCount}  ·  " +
            $"Max payload: {row.Candidate.MaxDataLength?.ToString() ?? "-"} bytes" + Environment.NewLine +
            row.RequestNote;
    }

    private void SetUdsEnabled(bool enabled)
    {
        ReadDidButton.IsEnabled = enabled;
        ReadVinButton.IsEnabled = enabled;
        ReadDtcButton.IsEnabled = enabled;
    }

    private bool TryGetUdsClient(out UdsClient? client, out string error)
    {
        client = null;
        error = string.Empty;

        if (!_connected)
        {
            error = "Connect to J2534 first.";
            return false;
        }

        if (CanIdFormat.SelectedIndex is not (0 or 1))
        {
            error = "Select 11-bit or 29-bit CAN ID format.";
            return false;
        }

        var canExtendedId = CanIdFormat.SelectedIndex == 1;
        if (!TryParseCanId(UdsRequestId.Text, canExtendedId, out var requestId, out var requestError) ||
            !TryParseCanId(UdsResponseId.Text, canExtendedId, out var responseId, out requestError))
        {
            error = requestError;
            return false;
        }

        try
        {
            var channel = new IsoTpChannel(
                _session.Bus,
                new IsoTpChannel.Options(requestId, responseId, canExtendedId));

            client = new UdsClient(channel);
            return true;
        }
        catch (Exception ex)
        {
            error = FormatSafeException(ex, "Unable to initialize the UDS channel.");
            return false;
        }
    }

    internal static bool TryParseCanId(string text, bool extended, out uint id, out string error)
    {
        error = string.Empty;
        id = 0;

        var value = text.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value[2..];
        var maxId = extended ? 0x1FFFFFFFu : 0x7FFu;
        if (string.IsNullOrWhiteSpace(value) ||
            !uint.TryParse(value, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out id) ||
            id > maxId)
        {
            error = extended
                ? "29-bit CAN ID must be a hexadecimal value between 00000000 and 1FFFFFFF."
                : "11-bit CAN ID must be a hexadecimal value between 000 and 7FF.";
            id = 0;
            return false;
        }

        return true;
    }

    private async void ReadDid_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseDid(out var did)) return;

        await RunUdsOperationAsync("Read DID", (client, token) => Hex(client.ReadDataByIdentifier(did, token)));
    }

    private async void ReadVin_Click(object sender, RoutedEventArgs e)
    {
        await RunUdsOperationAsync("Read VIN", (client, token) => client.ReadVin(token));
    }

    private async void ReadDtc_Click(object sender, RoutedEventArgs e)
    {
        await RunUdsOperationAsync("Read DTC", (client, token) =>
{
    var dtcs = client.ReadDtcByStatusMask(0xFF, token);
    return dtcs.Count == 0
        ? "No DTCs"
        : string.Join(", ", dtcs.Select(d => $"0x{d.Code:X6}/status=0x{d.Status:X2} [{UdsDtcStatusFormatter.Format(d.Status)}]"));
});
    }

    private static string Hex(byte[] data) => BitConverter.ToString(data).Replace('-', ' ');

    private bool TryParseDid(out ushort did)
    {
        did = 0;
        var value = UdsDid.Text.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value[2..];

        if (!ushort.TryParse(value, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out did))
        {
            SetStatus("DID must be a valid 16-bit hexadecimal value.");
            return false;
        }

        return true;
    }

    private async Task RunUdsOperationAsync(
        string operation,
        Func<UdsClient, CancellationToken, string> action)
    {
        if (!await _udsGate.WaitAsync(0))
        {
            SetStatus("Another UDS request is already running.");
            return;
        }

        try
        {
            if (!TryGetUdsClient(out var client, out var error) || client is null)
            {
                SetStatus(error);
                return;
            }

            using var udsClient = client;
            using var cts = new CancellationTokenSource();
            _udsCts = cts;

            SetUdsEnabled(false);
            DiscoverButton.IsEnabled = false;
            ConnectButton.IsEnabled = false;
            CancelUdsButton.IsEnabled = true;
            SetStatus(operation + "...");

            var data = await Task.Run(() => action(client, cts.Token));
            SetStatus(operation + ": " + data);
        }
        catch (OperationCanceledException)
        {
            SetStatus(operation + " cancelled.");
        }
        catch (UdsNegativeResponseException ex)
        {
            // Preserve the NRC explanation supplied by UdsClient so the UI
            // shows a useful reason instead of only the hexadecimal code.
            SetStatus(operation + " rejected: " + ex.Message);
        }
        catch (TimeoutException ex)
        {
            SetStatus(operation + " timeout: " + ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus(operation + " failed: " + FormatSafeException(ex, "unexpected diagnostic error."));
        }
        finally
        {
            _udsCts = null;
            CancelUdsButton.IsEnabled = false;
            SetUdsEnabled(_connected);
            DiscoverButton.IsEnabled = _connected;
            ConnectButton.IsEnabled = true;
            _udsGate.Release();
        }
    }


    private void CancelUds_Click(object sender, RoutedEventArgs e)
    {
        _udsCts?.Cancel();
        CancelUdsButton.IsEnabled = false;
        SetStatus("Cancelling UDS request...");
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (_connected)
        {
            if (_discoveryCts is not null || !_udsGate.Wait(0))
            {
                SetStatus("Wait for the active diagnostic operation to finish before disconnecting.");
                return;
            }
            _udsGate.Release();
            // StopMonitor requests cancellation and waits briefly, but retains
            // the task if its worker has not fully unwound. Do not dispose the
            // session/bus until that subscriber has exited.
            var monitorTask = _monitorTask;
            string? monitorWarning = null;
            StopMonitor();
            if (monitorTask is not null)
            {
                try { await monitorTask; }
                catch (Exception ex)
                {
                    // A monitor failure must not prevent session cleanup, but
                    // preserve its diagnostic hint in the disconnect status.
                    monitorWarning = FormatSafeException(ex, "unexpected monitor error.");
                }
            }

            ConnectButton.IsEnabled = false;
            HardwareFilterButton.IsEnabled = false;
            StartButton.IsEnabled = false;
            SetUdsEnabled(false);
            DiscoverButton.IsEnabled = false;
            SetStatus(monitorWarning is null
                ? "Disconnecting J2534..."
                : "Disconnecting J2534... CAN monitor warning: " + monitorWarning);
            var disconnectTask = Task.Run(() => _session.Disconnect());
            _disconnectTask = disconnectTask;
            try
            {
                await disconnectTask;
                _connected = false;
                _activeHardwareFilterId = null;
                HardwareFilterButton.Content = "Apply HW filter";
                HardwareFilterButton.IsEnabled = false;
                ConnectButton.Content = "Connect";
                SetStatus("Disconnected");
            }
            catch (Exception ex)
            {
                // Disconnect may have partially torn down the native session
                // before a vendor API throws. Do not leave controls enabled
                // against a bus that may already be disposed; the next Connect
                // attempt will first retry session cleanup.
                _connected = false;
                _activeHardwareFilterId = null;
                HardwareFilterButton.Content = "Apply HW filter";
                HardwareFilterButton.IsEnabled = false;
                StartButton.IsEnabled = false;
                SetUdsEnabled(false);
                DiscoverButton.IsEnabled = false;
                ConnectButton.Content = "Connect";
                SetStatus("Disconnect failed; session state reset. Retry Connect to reinitialize the adapter. " + FormatSafeException(ex, "unexpected adapter error."));
            }
            finally
            {
                _disconnectTask = null;
                HardwareFilterButton.IsEnabled = _connected;
                ConnectButton.IsEnabled = true;
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(DllPath.Text))
        {
            SetStatus("Select a J2534 DLL.");
            return;
        }

        if (BaudRate.SelectedItem is not ComboBoxItem baudItem ||
            !uint.TryParse(baudItem.Tag?.ToString(), out var baud))
        {
            SetStatus("Select a valid CAN baud rate.");
            return;
        }

        var dllPath = DllPath.Text.Trim();
        ConnectButton.IsEnabled = false;
        StartButton.IsEnabled = false;
        SetUdsEnabled(false);
        DiscoverButton.IsEnabled = false;
        SetStatus("Connecting to J2534...");
        var connectTask = Task.Run(() =>
        {
            var connected = _session.Connect(dllPath, baud, out var error);
            return (connected, error);
        });
        // Track the actual native connect operation. Its UI continuation must
        // check _closing before inspecting session properties after this await.
        _connectTask = connectTask;
        try
        {
            var result = await connectTask;
            if (_closing) return;
            if (!result.connected)
            {
                SetStatus("Connection failed: " + result.error);
                return;
            }

            _connected = true;
            HardwareFilterButton.IsEnabled = true;
            ConnectButton.Content = "Disconnect";
            StartButton.IsEnabled = true;
            SetUdsEnabled(true);
            DiscoverButton.IsEnabled = true;
            var version = _session.AdapterVersion;
            var capabilities = _session.AdapterCapabilities;
            var versionText = version is null
                ? (string.IsNullOrWhiteSpace(_session.AdapterVersionStatus) ? "version unavailable" : _session.AdapterVersionStatus)
                : $"API {version.Api} · DLL {version.Dll} · FW {version.Firmware}";
            var capabilityText = capabilities is null
                ? string.Empty
                : $" · HW filters {(capabilities.HardwareCanFilters ? "available" : "unavailable")} · version query {(capabilities.VersionQuery ? "available" : "unavailable")}";
            SetStatus("Connected · J2534 · CAN · " + versionText + capabilityText);
        }
        catch (Exception ex)
        {
            if (!_closing)
                SetStatus("Connection failed: " + FormatSafeException(ex, "unexpected adapter error."));
        }
        finally
        {
            _connectTask = null;
            if (!_closing)
                ConnectButton.IsEnabled = true;
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    { if (_cts is null) StartMonitor(); else StopMonitor(); }

    private void StartMonitor()
    {
        if (!_connected) return;

        // A cancelled monitor may still be unwinding from a native adapter call.
        // Never start a second reader until the previous task has actually ended.
        if (_monitorTask is { IsCompleted: false })
        {
            SetStatus("Waiting for the previous CAN monitor to stop.");
            return;
        }

        _monitorTask = null;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var subscription = _session.Bus.Subscribe();
        _monitorSubscription = subscription;
        DroppedFrameText.Text = "0 dropped";

        StartButton.Content = "Stop monitor";
        SetStatus("Monitoring CAN...");
        _rate.Restart();
        _rateFrames = 0;

        _monitorTask = Task.Run(() =>
        {
            try { ReadLoop(subscription, token); }
            finally
            {
                subscription.Dispose();
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (ReferenceEquals(_monitorSubscription, subscription))
                    {
                        DroppedFrameText.Text = $"{subscription.DroppedFrameCount:N0} dropped";
                        _monitorSubscription = null;
                    }
                });
            }
        });
    }

    private void StopMonitor()
    {
        var cts = _cts;
        _cts = null;
        cts?.Cancel();
        StartButton.Content = "Start monitor";

        var task = _monitorTask;

        // Never block the UI waiting for a worker that may be inside a native
        // J2534 read. Disconnect awaits this task explicitly; other callers
        // leave cleanup to the continuation below.
        if (task is null || task.IsCompleted)
        {
            _monitorTask = null;
            cts?.Dispose();
        }
        else
        {
            // Keep the task reference so StartMonitor cannot create a second
            // reader while the old one is still unwinding.
            _ = task.ContinueWith(
                completed =>
                {
                    cts?.Dispose();
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (ReferenceEquals(_monitorTask, completed))
                            _monitorTask = null;
                    });
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        if (_connected) SetStatus("Connected");
    }

    private void ReadLoop(CanRxDispatcher.Subscription subscription, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (!subscription.TryRead(TimeSpan.FromMilliseconds(250), token, out var canFrame))
                continue;

            var frame = new Frame
            {
                Timestamp = canFrame.Timestamp,
                Id = canFrame.IdHex,
                Type = canFrame.IsExtended ? "29-bit" : "11-bit",
                Dlc = canFrame.Dlc,
                Data = canFrame.DataHex
            };

            DispatcherQueue.TryEnqueue(() => AddFrame(frame));
        }
    }

    private void AddFrame(Frame frame)
    {
        DroppedFrameText.Text = $"{(_monitorSubscription?.DroppedFrameCount ?? 0):N0} dropped";
        if (!Matches(frame)) return;
        _frames.Add(frame); _total++; _rateFrames++;
        if (_frames.Count > 5000) _frames.RemoveAt(0);
        FrameCountText.Text = $"{_total:N0} frames";

        if (_rate.ElapsedMilliseconds >= 1000)
        {
            FrameRateText.Text = $"{_rateFrames} frames/s";
            _rateFrames = 0; _rate.Restart();
        }
    }

    private bool Matches(Frame frame) => MatchesCanIdFilter(IdFilter.Text, frame.Id);

    internal static bool MatchesCanIdFilter(string query, string frameId)
    {
        query = query.Trim();
        if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            query = query[2..];
        if (query.Length == 0)
            return true;

        // Parse numerically rather than trimming leading zeroes: "0" is a
        // real CAN-ID filter, not an empty filter that matches every frame.
        return uint.TryParse(query, System.Globalization.NumberStyles.HexNumber,
                   System.Globalization.CultureInfo.InvariantCulture, out var wanted) &&
               uint.TryParse(frameId, System.Globalization.NumberStyles.HexNumber,
                   System.Globalization.CultureInfo.InvariantCulture, out var actual) &&
               wanted == actual;
    }

    private void HardwareFilter_Click(object sender, RoutedEventArgs e)
    {
        if (!_connected)
        {
            SetStatus("Connect to J2534 before applying a hardware filter.");
            return;
        }

        var bus = _session.Bus;
        if (_activeHardwareFilterId is uint activeFilterId)
        {
            if (!bus.StopCanFilter(activeFilterId, out var stopError))
            {
                SetStatus("Could not remove hardware filter: " + stopError);
                return;
            }

            _activeHardwareFilterId = null;
            HardwareFilterButton.Content = "Apply HW filter";
            SetStatus("J2534 hardware filter removed.");
            return;
        }

        if (CanIdFormat.SelectedIndex is not (0 or 1))
        {
            SetStatus("Select 11-bit or 29-bit CAN ID format before applying a hardware filter.");
            return;
        }

        var extended = CanIdFormat.SelectedIndex == 1;
        if (!TryParseCanId(IdFilter.Text, extended, out var id, out var parseError))
        {
            SetStatus(parseError);
            return;
        }

        if (!bus.StartCanIdFilter(id, extended, out var filterId, out var startError))
        {
            SetStatus("J2534 hardware filter could not be applied: " + startError);
            return;
        }

        _activeHardwareFilterId = filterId;
        HardwareFilterButton.Content = "Remove HW filter";
        SetStatus($"J2534 hardware filter active for 0x{id:X} ({(extended ? "29-bit" : "11-bit")}).");
    }

    private void Filter_Changed(object sender, TextChangedEventArgs e) { }

    private void Clear_Click(object sender, RoutedEventArgs e)
    { _frames.Clear(); _total = 0; FrameCountText.Text = "0 frames"; }

    private async void Csv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.SuggestedFileName = "can-log";
            picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });

            var file = await picker.PickSaveFileAsync();
            if (file is null) return;

            var lines = new List<string> { "timestamp,id,type,dlc,data" };
            lines.AddRange(_frames.Select(frame =>
                $"{frame.Timestamp},{frame.Id},{frame.Type},{frame.Dlc},\"{frame.Data}\""));

            await File.WriteAllLinesAsync(file.Path, lines);
            SetStatus($"CSV saved: {file.Name}.");
        }
        catch (Exception)
        {
            // Picker and filesystem failures are expected user-environment errors.
            // Surface them in the status bar instead of escaping an async-void UI handler.
            SetStatus("CSV export failed. Check destination permissions and available disk space.");
        }
    }
}
