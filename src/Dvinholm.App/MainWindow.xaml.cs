using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Dvinholm.App;

public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<CanMonitorRow> _frames = new();
    private readonly CanMonitorHistory _monitorHistory = new();
    private readonly ObservableCollection<J2534Device> _j2534Devices = new();
    private readonly J2534Session _session = new();
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private CanRxDispatcher.Subscription? _monitorSubscription;
    private bool _connected;
    private DispatcherQueueTimer? _demoTimer;
    private uint _demoTick;
    private bool _demoDataPresent;
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
    private int _profileFilterGeneration;
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
        // The EXE also embeds this icon; SetIcon applies it to the WinUI title bar and taskbar.
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Dvinholm.ico");
        if (System.IO.File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);
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
            StopDemoMonitor();

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

    private bool UseStableUpdateSource => UpdateSource.SelectedIndex == 1;

    private void UpdateSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Source changes invalidate *both* a pending update offer and its download
        // URL. Never install a CI archive after switching the UI to Stable (or vice versa).
        if (UpdateButton is null || UpdateStatusText is null || _updaterBusy)
            return;

        _latestBuild = null;
        _updateAvailable = false;
        UpdateButton.Content = "Check update";
        UpdateStatusText.Text = $"Current: {LatestBuildUpdater.CurrentDisplayVersion} · " +
            $"source: {(UseStableUpdateSource ? "Stable" : "Development")}; select Check update.";
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updaterBusy)
            return;

        if (_updateAvailable && _latestBuild is { } build)
        {
            // A Stable release can be older than the currently installed
            // Development build. Do not assume direction from unequal SHAs:
            // require explicit confirmation for any Stable replacement.
            if (LatestBuildUpdater.RequiresStableReplacementConfirmation(
                UseStableUpdateSource, LatestBuildUpdater.CurrentCommitSha, build))
            {
                _updaterBusy = true;
                UpdateButton.IsEnabled = false;
                UpdateSource.IsEnabled = false;
                try
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Switch to Stable?",
                        XamlRoot = RootLayout.XamlRoot,
                        Content = new TextBlock
                        {
                            Text = $"You are about to replace the installed build with Stable " +
                                $"{build.DisplayVersion}. This release may be older than your " +
                                "current Development build, so newer application features may " +
                                "be lost. Saved ECU profiles are stored separately. " +
                                "The application files will be replaced and Dvinholm restarted. " +
                                "Continue?",
                            TextWrapping = TextWrapping.Wrap
                        },
                        PrimaryButtonText = "Replace files and restart",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Close
                    };

                    if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                    {
                        // Do not keep an actionable cached Stable offer after Cancel.
                        _latestBuild = null;
                        _updateAvailable = false;
                        UpdateButton.Content = "Check update";
                        UpdateStatusText.Text =
                            $"Current: {LatestBuildUpdater.CurrentDisplayVersion} · " +
                            "Stable replacement cancelled. Select Check update to check again.";
                        return;
                    }
                }
                finally
                {
                    _updaterBusy = false;
                    UpdateButton.IsEnabled = true;
                    UpdateSource.IsEnabled = true;
                }
            }

            await DownloadAndLaunchLatestBuildAsync(build);
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
        UpdateSource.IsEnabled = false;
        UpdateButton.Content = "Checking…";
        var stable = UseStableUpdateSource;
        UpdateStatusText.Text = $"Current: {LatestBuildUpdater.CurrentDisplayVersion} · " +
            (stable ? "checking latest published stable release…" : "checking latest successful main-branch CI build…");

        try
        {
            var latest = stable
                ? await LatestBuildUpdater.GetLatestStableReleaseAsync()
                : await LatestBuildUpdater.GetLatestSuccessfulBuildAsync();
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
            UpdateStatusText.Text = $"Current: {LatestBuildUpdater.CurrentDisplayVersion} · " +
                (stable ? "Latest stable release: unavailable" : "Latest CI build: unavailable");
            UpdateButton.Content = "Retry update check";
        }
        finally
        {
            _updaterBusy = false;
            UpdateButton.IsEnabled = true;
            UpdateSource.IsEnabled = true;
        }
    }

    private async Task DownloadAndLaunchLatestBuildAsync(LatestBuildInfo build)
    {
        _updaterBusy = true;
        UpdateButton.IsEnabled = false;
        UpdateSource.IsEnabled = false;
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
            UpdateSource.IsEnabled = true;
        }
    }


    private static string ProfileCatalogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Dvinholm",
        "ecu-profiles.json");

    private void SetProfileCatalog(EcuProfileCatalog catalog)
    {
        _profileCatalog = catalog;
        _profileFilterGeneration++;
        _loadingProfiles = true;
        try
        {
            ProfileSelector.ItemsSource = null;
            ProfileSelector.SelectedIndex = -1;
            ClearProfileInputs();
        }
        finally
        {
            _loadingProfiles = false;
        }

        RefreshProfileFilter();
    }

    private void ProfileSearch_TextChanged(object sender, TextChangedEventArgs e) =>
        RefreshProfileFilter();

    private void MarkedVerifiedOnly_Changed(object sender, RoutedEventArgs e) =>
        RefreshProfileFilter();

    private void RefreshProfileFilter()
    {
        // WinUI may fire TextChanged/Checked during InitializeComponent.
        if (ProfileSelector is null || EcuInfoText is null ||
            ProfileSearch is null || MarkedVerifiedOnly is null || _loadingProfiles)
            return;

        var previousId = (ProfileSelector.SelectedItem as EcuProfile)?.Id;
        var filtered = EcuProfileCatalogSearch.Filter(
            _profileCatalog, ProfileSearch.Text, MarkedVerifiedOnly.IsChecked == true);
        var retained = previousId is null
            ? null
            : filtered.FirstOrDefault(profile =>
                string.Equals(profile.Id, previousId, StringComparison.OrdinalIgnoreCase));

        // Suppress ComboBox selection changes caused by replacing its ItemsSource;
        // this should not overwrite manually edited UDS fields for a retained
        // profile or trigger a vehicle diagnostic request.
        _profileFilterGeneration++;
        _loadingProfiles = true;
        try
        {
            ProfileSelector.ItemsSource = filtered;
            ProfileSelector.SelectedItem = retained;
        }
        finally
        {
            _loadingProfiles = false;
        }

        if (previousId is not null && retained is null)
        {
            ClearProfileInputs();
            SetStatus("Previously selected ECU profile is hidden by the filter. No diagnostic request was sent.");
        }

        EditProfileButton.IsEnabled = retained is not null;

        EcuInfoText.Text = retained is null
            ? $"ECU profiles: {filtered.Count} shown of {_profileCatalog.All.Count}. " +
              "Choose a profile to view its source and candidate DIDs. " +
              "The 'marked verified' flag is supplied by the catalog author, not checked against a vehicle."
            : EcuProfileCatalogSearch.Describe(retained);
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

    private async void CreateProfile_Click(object sender, RoutedEventArgs e) =>
        await ShowProfileEditorAsync(null);

    private async void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileSelector.SelectedItem is not EcuProfile profile)
        {
            SetStatus("Select an ECU profile before editing.");
            return;
        }

        await ShowProfileEditorAsync(profile);
    }

    private async Task ShowProfileEditorAsync(EcuProfile? editing)
    {
        // Editing and creating are LOCAL operations; nothing is sent to CAN.
        // Stable profile IDs cannot be renamed. Author-provided verification
        // is reset for EVERY edit to avoid carrying an obsolete assertion.
        var idBox = new TextBox
        {
            Header = "Profile ID (unique; immutable on edit)",
            Text = editing?.Id ?? string.Empty,
            IsReadOnly = editing is not null,
            PlaceholderText = "e.g. my.ecu.draft"
        };
        var nameBox = new TextBox
        {
            Header = "ECU name",
            Text = editing?.Name ?? string.Empty,
            PlaceholderText = "e.g. My test ECU"
        };
        var requestBox = new TextBox
        {
            Header = "Request CAN ID (hex)",
            Text = editing?.RequestId.ToString(editing.IsExtended ? "X8" : "X3") ?? string.Empty,
            PlaceholderText = "e.g. 7E0"
        };
        var responseBox = new TextBox
        {
            Header = "Response CAN ID (hex)",
            Text = editing?.ResponseId.ToString(editing.IsExtended ? "X8" : "X3") ?? string.Empty,
            PlaceholderText = "e.g. 7E8"
        };
        var formatBox = new ComboBox { Header = "CAN ID format" };
        formatBox.Items.Add(new ComboBoxItem { Content = "11-bit" });
        formatBox.Items.Add(new ComboBoxItem { Content = "29-bit" });
        formatBox.SelectedIndex = editing?.IsExtended == true ? 1 : 0;
        var didBox = new TextBox
        {
            Header = "Candidate readable DIDs (hex; comma or space separated)",
            Text = editing is null
                ? string.Empty
                : string.Join(", ", editing.ReadableDataIdentifiers.Order().Select(did => did.ToString("X4"))),
            PlaceholderText = "e.g. F190, F187 (optional)"
        };
        var sourceBox = new TextBox
        {
            Header = "Source / verification note (required)",
            Text = editing?.Source ?? string.Empty,
            PlaceholderText = "Describe where the addresses and DIDs came from",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 76
        };
        var warning = new TextBlock
        {
            Text = "Saved profiles are marked UNVERIFIED. Any edit clears a previous author verification flag. " +
                   "Do not use unconfirmed CAN IDs or DIDs on a real vehicle.",
            TextWrapping = TextWrapping.Wrap
        };
        var errorBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.IndianRed);
        var errorText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
            Foreground = errorBrush
        };
        // Restore each field's normal border as soon as the user edits it.
        foreach (var box in new[] { idBox, nameBox, requestBox, responseBox, didBox, sourceBox })
        {
            var normalBorder = box.BorderBrush;
            box.TextChanged += (_, _) => box.BorderBrush = normalBorder;
        }
        var fields = new StackPanel { Spacing = 9 };
        foreach (var control in new UIElement[]
                 { idBox, nameBox, formatBox, requestBox, responseBox, didBox, sourceBox, warning })
            fields.Children.Add(control);

        var viewer = new ScrollViewer
        {
            Content = fields,
            MaxHeight = 480,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var dialog = new ContentDialog
        {
            Title = editing is null ? "New ECU profile" : "Edit ECU profile",
            XamlRoot = RootLayout.XamlRoot,
            Content = new StackPanel
            {
                Spacing = 10,
                Children = { errorText, viewer }
            },
            PrimaryButtonText = editing is null ? "Create and save" : "Save changes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        EcuProfileCatalog? savedCatalog = null;
        EcuProfile? savedProfile = null;
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try
            {
                var draft = new EcuProfileDraft(
                    idBox.Text, nameBox.Text, requestBox.Text, responseBox.Text,
                    formatBox.SelectedIndex == 1, didBox.Text, sourceBox.Text);
                var result = EcuProfileEditor.Save(_profileCatalog, draft, editing?.Id);
                // Persist the replacement catalog FIRST. A failed disk write
                // must leave the current in-memory profile list untouched.
                SaveProfileCatalog(result.Catalog);
                savedCatalog = result.Catalog;
                savedProfile = result.Profile;
                errorText.Text = string.Empty;
                errorText.Visibility = Visibility.Collapsed;
            }
            catch (ArgumentException ex)
            {
                args.Cancel = true;
                var invalidField = ex.ParamName switch
                {
                    nameof(EcuProfileDraft.Id) => idBox,
                    nameof(EcuProfileDraft.Name) => nameBox,
                    nameof(EcuProfileDraft.RequestId) => requestBox,
                    nameof(EcuProfileDraft.ResponseId) => responseBox,
                    nameof(EcuProfileDraft.CandidateDids) => didBox,
                    nameof(EcuProfileDraft.Source) => sourceBox,
                    _ => null
                };
                if (invalidField is not null)
                {
                    invalidField.BorderBrush = errorBrush;
                    invalidField.Focus(FocusState.Programmatic);
                }
                // ArgumentException appends the parameter name to Message;
                // keep the user-facing validation wording free of that suffix.
                var message = ex.Message;
                if (ex.ParamName is not null)
                {
                    var suffix = $" (Parameter '{ex.ParamName}')";
                    if (message.EndsWith(suffix, StringComparison.Ordinal))
                        message = message[..^suffix.Length];
                }
                errorText.Text = "Profile not saved: " + message;
                errorText.Visibility = Visibility.Visible;
            }
            catch (IOException)
            {
                args.Cancel = true;
                errorText.Text = "Profile not saved: check local storage and free disk space.";
                errorText.Visibility = Visibility.Visible;
            }
            catch (UnauthorizedAccessException)
            {
                args.Cancel = true;
                errorText.Text = "Profile not saved: access to local storage was denied.";
                errorText.Visibility = Visibility.Visible;
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary ||
            savedCatalog is null || savedProfile is null)
            return;

        // A newly created or edited profile should be immediately visible,
        // even if the old search/verification filter was hiding it.
        _loadingProfiles = true;
        try
        {
            ProfileSearch.Text = string.Empty;
            MarkedVerifiedOnly.IsChecked = false;
        }
        finally
        {
            _loadingProfiles = false;
        }
        SetProfileCatalog(savedCatalog);
        var selected = ((IEnumerable<EcuProfile>)ProfileSelector.ItemsSource)
            .First(p => string.Equals(p.Id, savedProfile.Id, StringComparison.OrdinalIgnoreCase));
        ProfileSelector.SelectedItem = selected;
        ApplySelectedProfile(selected);
        SetStatus($"Saved ECU profile '{savedProfile.Name}' as unverified. No diagnostic request was sent.");
    }

    private void AddTestProfiles_Click(object sender, RoutedEventArgs e)
    {
        const string standardId = "dvinholm-test-standard-11bit";
        const string extendedId = "dvinholm-test-extended-29bit";

        var profiles = _profileCatalog.All.ToList();
        var added = 0;

        if (!_profileCatalog.TryGet(standardId, out _))
        {
            profiles.Add(new EcuProfile(
                standardId,
                "TEST — Standard CAN (11-bit)",
                0x7E0,
                0x7E8,
                isExtended: false,
                readableDataIdentifiers: new ushort[] { 0xF190 },
                source: "Synthetic Dvinholm UI test profile only; not verified for any vehicle."));
            added++;
        }

        if (!_profileCatalog.TryGet(extendedId, out _))
        {
            profiles.Add(new EcuProfile(
                extendedId,
                "TEST — Extended CAN (29-bit)",
                0x18DA10F1,
                0x18DAF110,
                isExtended: true,
                readableDataIdentifiers: new ushort[] { 0x1234 },
                source: "Synthetic Dvinholm UI test profile only; not verified for any vehicle."));
            added++;
        }

        if (added == 0)
        {
            SetStatus("Both test ECU profiles are already present.");
            return;
        }

        var updated = new EcuProfileCatalog(profiles);
        try
        {
            SaveProfileCatalog(updated);
            SetProfileCatalog(updated);
            SetStatus($"Added and saved {added} synthetic test ECU profile(s). These profiles are for UI testing only, not vehicle use.");
        }
        catch (Exception)
        {
            SetStatus("Test profiles were not saved. Check the local Dvinholm folder permissions and available disk space.");
        }
    }

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
            // WinRT FileSavePicker creates an empty StorageFile before the
            // destination can be checked, so use the path-only Win32 dialog.
            var destinationPath = ReportSavePathPicker.PickPath(
                WindowNative.GetWindowHandle(this), "ecu-profiles", ".json", "JSON");
            if (destinationPath is null)
                return; // Cancel must not create or replace anything.

            await EcuProfileJsonExport.SaveAsync(
                _profileCatalog, destinationPath, AppContext.BaseDirectory);
            SetStatus($"Exported {_profileCatalog.All.Count} ECU profile(s) to {Path.GetFileName(destinationPath)}.");
        }
        catch (InvalidOperationException ex)
        {
            // Empty catalogs and app-directory locations have clear, specific
            // no-write errors. Other I/O failures use the generic safe text.
            SetStatus(ex.Message);
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

        EditProfileButton.IsEnabled = ProfileSelector.SelectedItem is EcuProfile;

        // WinUI can raise selection notifications while the ComboBox is still
        // reconciling its selected item. Apply the final selection on the UI
        // queue so returning to a profile always restores its saved values,
        // rather than leaving a manually edited ID in the TextBox.
        var generation = _profileFilterGeneration;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_loadingProfiles || generation != _profileFilterGeneration)
                return;

            ApplySelectedProfile(ProfileSelector.SelectedItem as EcuProfile);
        });
    }

    private void ApplySelectedProfile(EcuProfile? profile)
    {
        EditProfileButton.IsEnabled = profile is not null;
        if (profile is null)
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

        EcuInfoText.Text = EcuProfileCatalogSearch.Describe(profile);
        SetStatus($"Applied profile '{profile.Name}' (request {UdsRequestId.Text}, response {UdsResponseId.Text}). No diagnostic request was sent.");
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
            if (ex.DroppedFrameCount > 0)
                DiscoveryStatus.Text += $" · {ex.DroppedFrameCount} frame(s) dropped";
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

    private bool CanUseOfflineUdsSample() =>
        !_closing && !_connected && _demoTimer is null && _udsGate.CurrentCount > 0;

    private void PreviewUdsSample_Click(object sender, RoutedEventArgs e)
    {
        // A preview is independent of the live J2534/UDS stack. Keep it
        // distinct from real diagnostic output and from the CAN demo feed.
        if (!CanUseOfflineUdsSample())
        {
            SetStatus("OFFLINE UDS preview requires a disconnected, idle session. " +
                "Stop the CAN demo and any active diagnostic operation first.");
            return;
        }

        SetStatus(UdsOfflineSample.Format());
    }

    private async void ExportOfflineUdsTxt_Click(object sender, RoutedEventArgs e)
    {
        // Export only the *fixed synthetic fixture*. Do not export StatusText:
        // it can contain actual VIN, DID or DTC results in a live session.
        if (!CanUseOfflineUdsSample())
        {
            SetStatus("OFFLINE UDS TXT export requires a disconnected, idle session. " +
                "Stop the CAN demo and any active diagnostic operation first.");
            return;
        }

        try
        {
            // The WinRT FileSavePicker creates an empty placeholder on Save,
            // even when our destination guard rejects the selected folder.
            // Pick ONLY a path; do not create a file until checks have passed.
            var destinationPath = ReportSavePathPicker.PickPath(
                WindowNative.GetWindowHandle(this),
                UdsOfflineTextReport.SuggestedFileName, ".txt", "Text");
            if (destinationPath is null)
                return; // Cancel leaves the filesystem completely untouched.

            // UI may have changed while the picker was open. In all cases the
            // exported text comes from the immutable synthetic fixture.
            if (!CanUseOfflineUdsSample())
            {
                SetStatus("OFFLINE UDS TXT export cancelled: the session is no longer idle.");
                return;
            }

            // Files inside the app's installation tree can be removed by the
            // in-place updater. Never write report contents to that tree.
            if (ReportExportDestinationGuard.IsInsideApplicationDirectory(
                    destinationPath, AppContext.BaseDirectory))
            {
                SetStatus(ReportExportDestinationGuard.BlockedMessage);
                return;
            }

            await ReportExportFileWriter.WriteTextAsync(
                destinationPath, UdsOfflineTextReport.Build());
            SetStatus($"SYNTHETIC UDS TXT saved: {Path.GetFileName(destinationPath)}. " +
                "No vehicle data was exported.");
        }
        catch (Exception)
        {
            SetStatus("Synthetic UDS TXT export failed. " +
                "Check destination permissions and available disk space.");
        }
    }

    private async void ReadDid_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseDid(out var did)) return;

        await RunUdsOperationAsync("Read DID", (client, token) =>
            UdsDidDisplayFormatter.Format(did, client.ReadDataByIdentifier(did, token)));
    }

    private async void ReadVin_Click(object sender, RoutedEventArgs e)
    {
        await RunUdsOperationAsync("Read VIN", (client, token) => client.ReadVin(token));
    }

    private async void ReadDtc_Click(object sender, RoutedEventArgs e)
    {
        const byte statusMask = 0xFF;
        await RunUdsOperationAsync("Read DTC", (client, token) =>
        {
            var records = client.ReadDtcByStatusMask(statusMask, token);
            return UdsDtcDisplayFormatter.Format(records, statusMask);
        });
    }

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
        // Demo never touches the real adapter. Do not allow either path to
        // transition into the other while its frame source is running.
        if (_demoTimer is not null)
        {
            SetStatus("Stop the offline CAN demo before connecting a J2534 adapter.");
            return;
        }

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
                DemoMonitorButton.IsEnabled = !_connected && !_closing;
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

        // Never combine previously generated demo rows with real CAN capture.
        if (_demoDataPresent)
            ClearDemoData();

        var dllPath = DllPath.Text.Trim();
        DemoMonitorButton.IsEnabled = false;
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
            {
                ConnectButton.IsEnabled = true;
                DemoMonitorButton.IsEnabled = !_connected;
            }
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    { if (_cts is null) StartMonitor(); else StopMonitor(); }

    private void StartMonitor()
    {
        if (!_connected || _demoTimer is not null) return;

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

            var frame = CanMonitorRow.FromFrame(canFrame);
            DispatcherQueue.TryEnqueue(() => AddFrame(frame));
        }
    }

    private void AddFrame(CanMonitorRow frame)
    {
        // Preserve all received frames, even those currently hidden by the
        // software filter. The view is a projection of the last 5000 RX frames.
        var evicted = _monitorHistory.Append(frame);
        if (evicted is not null && _frames.Count > 0 &&
            ReferenceEquals(_frames[0], evicted))
            _frames.RemoveAt(0);

        DroppedFrameText.Text = $"{(_monitorSubscription?.DroppedFrameCount ?? 0):N0} dropped";
        if (CanMonitorHistory.MatchesCanIdFilter(IdFilter.Text, frame.Id))
            _frames.Add(frame);
        _total++;
        _rateFrames++;
        UpdateFrameCountText();

        if (_rate.ElapsedMilliseconds >= 1000)
        {
            FrameRateText.Text = $"{_rateFrames} frames/s";
            _rateFrames = 0;
            _rate.Restart();
        }
    }

    private void UpdateFrameCountText() =>
        FrameCountText.Text = $"{_frames.Count:N0} shown / {_total:N0} rx";

    private void RefreshMonitorFilter()
    {
        var visible = _monitorHistory.Filter(IdFilter.Text);
        _frames.Clear();
        foreach (var frame in visible)
            _frames.Add(frame);
        UpdateFrameCountText();
    }

    internal static bool MatchesCanIdFilter(string query, string frameId) =>
        CanMonitorHistory.MatchesCanIdFilter(query, frameId);

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

    private void Filter_Changed(object sender, TextChangedEventArgs e) =>
        RefreshMonitorFilter();

    private void ResetMonitorView()
    {
        _monitorHistory.Clear();
        _frames.Clear();
        _total = 0;
        _rateFrames = 0;
        _rate.Restart();
        FrameRateText.Text = "0 frames/s";
        UpdateFrameCountText();
    }

    private void ClearDemoData()
    {
        // A stopped demo keeps its data for CSV until explicitly cleared or
        // the user begins a genuine J2534 connection.
        ResetMonitorView();
        _demoDataPresent = false;
        DemoStatusText.Visibility = Visibility.Collapsed;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_demoTimer is null && _demoDataPresent)
            ClearDemoData();
        else
            ResetMonitorView();
    }

    private void DemoMonitor_Click(object sender, RoutedEventArgs e)
    {
        if (_demoTimer is not null)
        {
            StopDemoMonitor();
            SetStatus("DEMO stopped. Synthetic frames remain available for filtering and CSV.");
            return;
        }

        if (_closing || _connected || _cts is not null ||
            _monitorTask is { IsCompleted: false } ||
            _connectTask is { IsCompleted: false } ||
            _disconnectTask is { IsCompleted: false })
        {
            SetStatus("Demo requires a disconnected and idle J2534 session.");
            return;
        }

        // All demo rows come from an in-process generator. No adapter is
        // opened, and no J2534 / ISO-TP / UDS operation is issued.
        ResetMonitorView();
        IdFilter.Text = string.Empty;
        _demoDataPresent = true;
        _demoTick = 0;
        DemoStatusText.Text = "DEMO ACTIVE — synthetic CAN frames only; no adapter or vehicle.";
        DemoStatusText.Visibility = Visibility.Visible;
        DemoMonitorButton.Content = "Stop demo";
        ConnectButton.IsEnabled = false;
        DroppedFrameText.Text = "0 dropped";

        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(CanDemoFrameGenerator.IntervalMilliseconds);
        timer.IsRepeating = true;
        timer.Tick += DemoTimer_Tick;
        _demoTimer = timer;
        timer.Start();
        EmitDemoFrames(); // Make the first batch visible immediately.
        SetStatus("DEMO ONLY: synthetic CAN frames. J2534 and diagnostics are not connected.");
    }

    private void DemoTimer_Tick(DispatcherQueueTimer sender, object args) =>
        EmitDemoFrames();

    private void EmitDemoFrames()
    {
        if (_demoTimer is null || _closing)
            return;

        foreach (var row in CanDemoFrameGenerator.CreateBatch(_demoTick++))
            AddFrame(row);
    }

    private void StopDemoMonitor()
    {
        var timer = _demoTimer;
        _demoTimer = null;
        if (timer is null)
            return;

        timer.Stop();
        timer.Tick -= DemoTimer_Tick;
        DemoMonitorButton.Content = "Start demo";
        if (_demoDataPresent)
            DemoStatusText.Text = "DEMO DATA — synthetic frames retained for filtering/CSV.";
        if (!_closing && !_connected && _connectTask is not { IsCompleted: false })
            ConnectButton.IsEnabled = true;
    }

    private async void Csv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var destinationPath = ReportSavePathPicker.PickPath(
                WindowNative.GetWindowHandle(this),
                _demoDataPresent ? "can-demo-SYNTHETIC" : "can-log",
                ".csv", "CSV");
            if (destinationPath is null) return;

            // Exported CAN logs also belong outside the application folder.
            // This applies to both synthetic demo and ordinary CSV captures.
            if (ReportExportDestinationGuard.IsInsideApplicationDirectory(
                    destinationPath, AppContext.BaseDirectory))
            {
                SetStatus(ReportExportDestinationGuard.BlockedMessage);
                return;
            }

            var lines = CanMonitorCsv.BuildLines(_frames, _demoDataPresent);
            await ReportExportFileWriter.WriteLinesAsync(destinationPath, lines);
            SetStatus(_demoDataPresent
                ? $"SYNTHETIC DEMO CSV saved: {Path.GetFileName(destinationPath)}."
                : $"CSV saved: {Path.GetFileName(destinationPath)}.");
        }
        catch (Exception)
        {
            // Picker and filesystem failures are expected user-environment errors.
            // Surface them in the status bar instead of escaping an async-void UI handler.
            SetStatus("CSV export failed. Check destination permissions and available disk space.");
        }
    }
}
