using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace VolvoJ2534.App;

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
    private bool _connected;
    private Task? _disconnectTask;
    private Task<bool>? _connectTask;
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

    public MainWindow()
    {
        InitializeComponent();
        FramesView.ItemsSource = _frames;
        EcusView.ItemsSource = _ecus;
        J2534Devices.ItemsSource = _j2534Devices;
        RefreshJ2534Devices(updateStatus: false);
        _session.ReadError += ex => DispatcherQueue.TryEnqueue(() => SetStatus("Read error: " + ex.Message));
        Closed += (_, _) =>
        {
            // Ask background operations to stop before touching the native J2534
            // session. Do not dispose it while a worker may still be using it.
            _discoveryCts?.Cancel();
            _udsCts?.Cancel();

            var discoveryTask = _discoveryTask;
            var monitorTask = _monitorTask;
            var disconnectTask = _disconnectTask;
            var connectTask = _connectTask;
            StopMonitor();

            try { discoveryTask?.Wait(TimeSpan.FromSeconds(2)); }
            catch (AggregateException) { }
            try { monitorTask?.Wait(TimeSpan.FromSeconds(2)); }
            catch (AggregateException) { }
            try { disconnectTask?.Wait(TimeSpan.FromSeconds(2)); }
            catch (AggregateException) { }
            try { connectTask?.Wait(TimeSpan.FromSeconds(2)); }
            catch (AggregateException) { }

            var udsStopped = _udsGate.Wait(TimeSpan.FromSeconds(2));
            if (udsStopped)
                _udsGate.Release();

            var discoveryStopped = discoveryTask is null || discoveryTask.IsCompleted;
            var monitorStopped = monitorTask is null || monitorTask.IsCompleted;
            var disconnectStopped = disconnectTask is null || disconnectTask.IsCompleted;
            var connectStopped = connectTask is null || connectTask.IsCompleted;
            if (discoveryStopped && monitorStopped && udsStopped && disconnectStopped && connectStopped)
                _session.Dispose();
            // If a native adapter call is still blocked, avoid racing Dispose
            // against that call. The process is closing and will reclaim handles.
        };
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private async void ImportProfiles_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".json");
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            var json = await File.ReadAllTextAsync(file.Path);
            var imported = EcuProfileCatalog.FromJson(json);
            _profileCatalog = imported;

            _loadingProfiles = true;
            ProfileSelector.ItemsSource = _profileCatalog.All;
            ProfileSelector.DisplayMemberPath = nameof(EcuProfile.Name);
            ProfileSelector.SelectedIndex = -1;
            _loadingProfiles = false;

            SetStatus($"Imported {_profileCatalog.All.Count} ECU profile(s) from {file.Name}. Select a profile to apply its CAN IDs.");
        }
        catch (Exception ex)
        {
            _loadingProfiles = false;
            SetStatus("Profile import failed: " + ex.Message);
        }
    }

    private async void ExportProfiles_Click(object sender, RoutedEventArgs e)
    {
        if (_profileCatalog.All.Count == 0)
        {
            SetStatus("There are no ECU profiles to export. Import a JSON catalog first.");
            return;
        }

        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedFileName = "ecu-profiles";
        picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        try
        {
            await File.WriteAllTextAsync(file.Path, _profileCatalog.ToJson());
            SetStatus($"Exported {_profileCatalog.All.Count} ECU profile(s) to {file.Path}");
        }
        catch (Exception ex)
        {
            SetStatus("Profile export failed: " + ex.Message);
        }
    }

    private void ProfileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingProfiles || ProfileSelector.SelectedItem is not EcuProfile profile)
            return;

        UdsRequestId.Text = profile.RequestId.ToString(profile.IsExtended ? "X8" : "X3");
        UdsResponseId.Text = profile.ResponseId.ToString(profile.IsExtended ? "X8" : "X3");
        CanIdFormat.SelectedIndex = profile.IsExtended ? 1 : 0;
        if (profile.ReadableDataIdentifiers.Count > 0)
            UdsDid.Text = profile.ReadableDataIdentifiers.Order().First().ToString("X4");

        EcuInfoText.Text = $"Profile: {profile.Name} ({profile.Id}) · {(profile.IsVerified ? "verified" : "unverified draft")}" +
            Environment.NewLine + $"Source: {profile.Source}" +
            Environment.NewLine + $"Candidate readable DIDs: {(profile.ReadableDataIdentifiers.Count == 0 ? "none listed" : string.Join(", ", profile.ReadableDataIdentifiers.Order().Select(did => did.ToString("X4"))))}" +
            Environment.NewLine + "Profile metadata does not guarantee that the ECU supports a listed DID.";
        SetStatus($"Applied profile '{profile.Name}'. No diagnostic request was sent.");
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

            DiscoveryStatus.Text = found.Count == 0
                ? "No UDS responses observed"
                : $"{found.Count} ECU(s) found";

            SetStatus("ECU discovery complete.");
        }
        catch (OperationCanceledException)
        {
            DiscoveryStatus.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            DiscoveryStatus.Text = "Failed";
            SetStatus("ECU discovery failed: " + ex.Message);
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
        if (row.Candidate.RequestId is uint requestId)
            UdsRequestId.Text = requestId.ToString(row.Candidate.IsExtended ? "X8" : "X3");
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
            error = ex.Message;
            return false;
        }
    }

    internal static bool TryParseCanId(string text, bool extended, out uint id, out string error)
    {
        error = string.Empty;
        id = 0;

        var value = text.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase);
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
        : string.Join(", ", dtcs.Select(d => $"0x{d.Code:X6}/status=0x{d.Status:X2}"));
});
    }

    private static string Hex(byte[] data) => BitConverter.ToString(data).Replace('-', ' ');

    private bool TryParseDid(out ushort did)
    {
        did = 0;
        var value = UdsDid.Text.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase);

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
            SetStatus(operation + " failed: " + ex.Message);
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
            StopMonitor();
            ConnectButton.IsEnabled = false;
            HardwareFilterButton.IsEnabled = false;
            StartButton.IsEnabled = false;
            SetUdsEnabled(false);
            DiscoverButton.IsEnabled = false;
            SetStatus("Disconnecting J2534...");
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
                SetStatus("Disconnect failed: " + ex.Message);
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
        _connectTask = connectTask.ContinueWith(
            completed => completed.Status == TaskStatus.RanToCompletion && completed.Result.connected,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        try
        {
            var result = await connectTask;
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
            SetStatus("Connection failed: " + ex.Message);
        }
        finally
        {
            _connectTask = null;
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

        StartButton.Content = "Stop monitor";
        SetStatus("Monitoring CAN...");
        _rate.Restart();
        _rateFrames = 0;

        _monitorTask = Task.Run(() =>
        {
            try { ReadLoop(subscription, token); }
            finally { subscription.Dispose(); }
        });
    }

    private void StopMonitor()
    {
        var cts = _cts;
        _cts = null;
        cts?.Cancel();
        StartButton.Content = "Start monitor";

        var task = _monitorTask;

        if (task is not null && !task.IsCompleted)
        {
            try { task.Wait(TimeSpan.FromSeconds(1)); }
            catch (AggregateException) { }
        }

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
        SetStatus("CSV saved: " + file.Path);
    }
}
