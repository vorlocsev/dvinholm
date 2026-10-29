using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Dvinholm.App;

public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<CanMonitorRow> _frames = new();
    private readonly CanMonitorHistory _monitorHistory = new();
    private readonly CanMonitorRxDropTracker _rxDropTracker = new();
    private bool _canRxSummaryBusy;
    private readonly ObservableCollection<J2534Device> _j2534Devices = new();
    private readonly J2534Session _session = new();
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private CanRxDispatcher.Subscription? _monitorSubscription;
    private bool _connected;
#if DVINHOLM_DEVELOPMENT_FEATURES
    private DispatcherQueueTimer? _demoTimer;
    private uint _demoTick;
    private bool _demoDataPresent;
    private Button _demoMonitorButton = null!;
    private TextBlock _demoStatusText = null!;
#endif
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
    private bool _snapshotInspectionBusy;
    private bool _loadingProfiles;
    private int _profileFilterGeneration;
    private int _rateFrames;
    private uint? _activeHardwareFilterId;
    public MainWindow()
    {
        InitializeComponent();
#if DVINHOLM_DEVELOPMENT_FEATURES
        InitializeDevelopmentControls();
#endif
        LoadSavedBaudRate();
        BaudRate.SelectionChanged += BaudRate_SelectionChanged;
        DllPath.Text = LoadSavedDllPath();
        DllPath.TextChanged += DllPath_TextChanged;
        Title = LatestBuildUpdater.CurrentWindowTitle;
        // Updates and dependency management belong to the standalone Launcher.
        // Legacy update handlers remain temporarily compiled for migration safety,
        // but the diagnostic window exposes no update controls.
        // The EXE also embeds this icon; SetIcon applies it to the WinUI title bar and taskbar.
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Dvinholm.ico");
        if (System.IO.File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);
        LoadSavedProfileCatalog();
        FramesView.ItemsSource = _frames;
        EcusView.ItemsSource = _ecus;
        J2534Devices.ItemsSource = _j2534Devices;
        RefreshJ2534Devices(updateStatus: false);
        // The standalone launcher owns updates and all Lite runtime management.
        if (DvinholmUserDataDirectory.IsAcceptanceLaunch)
            ConnectButton.IsEnabled = false;
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
#if DVINHOLM_DEVELOPMENT_FEATURES
            StopDemoMonitor();
#endif

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

    private static string ProfileCatalogPath => Path.Combine(
        DvinholmUserDataDirectory.Current,
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
        RefreshSnapshotCaptureAvailability();

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
            var (catalog, recoveredFromBackup) =
                new EcuProfileCatalogStore(path).LoadWithRecovery();
            SetProfileCatalog(catalog);
            SetStatus(recoveredFromBackup
                ? $"Recovered {catalog.All.Count} ECU profile(s) from the local backup. " +
                  "The damaged original has not been overwritten; the next successful save archives it separately."
                : $"Loaded {catalog.All.Count} saved ECU profile(s).");
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
                // The coordinator only returns a new catalog after the
                // complete on-disk save succeeds; never publish an unsaved edit.
                var result = EcuProfileEditor.SaveAndPersist(
                    _profileCatalog, draft, SaveProfileCatalog, editing?.Id);
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

#if DVINHOLM_DEVELOPMENT_FEATURES
    private void InitializeDevelopmentControls()
    {
        var addTestProfiles = new Button { Content = "Add test profiles" };
        addTestProfiles.Click += AddTestProfiles_Click;
        ProfileActionsPanel.Children.Insert(2, addTestProfiles);

        _demoMonitorButton = new Button { Content = "Start demo" };
        _demoMonitorButton.Click += DemoMonitor_Click;
        Grid.SetColumn(_demoMonitorButton, 0);
        DeveloperMonitorHost.Children.Add(_demoMonitorButton);

        _demoStatusText = new TextBlock
        {
            Text = "DEMO — synthetic data, no J2534 connection",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        Grid.SetColumn(_demoStatusText, 1);
        DeveloperMonitorHost.Children.Add(_demoStatusText);
        DeveloperMonitorHost.Visibility = Visibility.Visible;

        var offlinePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        var previewUds = new Button { Content = "Preview UDS example (offline)" };
        previewUds.Click += PreviewUdsSample_Click;
        offlinePanel.Children.Add(previewUds);
        var exportSynthetic = new Button { Content = "Export synthetic UDS TXT" };
        exportSynthetic.Click += ExportOfflineUdsTxt_Click;
        offlinePanel.Children.Add(exportSynthetic);
        StatusPanel.Children.Insert(0, offlinePanel);
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

#endif
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
            // Validate the entire JSON and persist it before touching the
            // profile selector or any of the currently applied UDS CAN IDs.
            var imported = EcuProfileCatalogImporter.ImportAndPersist(
                json, SaveProfileCatalog);
            SetProfileCatalog(imported);
            SetStatus($"Imported and saved {imported.All.Count} ECU profile(s) from {file.Name}. Select a profile to apply its CAN IDs.");
        }
        catch (InvalidDataException)
        {
            SetStatus("Profile import not applied: invalid ECU profile JSON. Existing profiles remain unchanged.");
        }
        catch (ArgumentException)
        {
            SetStatus("Profile import not applied: invalid ECU profile data. Existing profiles remain unchanged.");
        }
        catch (IOException)
        {
            SetStatus("Profile import not applied: could not read or save the local catalog. Existing profiles remain unchanged.");
        }
        catch (UnauthorizedAccessException)
        {
            SetStatus("Profile import not applied: access to the selected or local catalog file was denied. Existing profiles remain unchanged.");
        }
        catch (Exception)
        {
            SetStatus("Profile import failed without applying the imported catalog. Existing profiles remain unchanged.");
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

    private async void P2PinReview_Click(object sender, RoutedEventArgs e)
    {
        // A cancellable *explanatory* checklist, not a live PIN workflow.
        // No J2534, CAN/UDS, credentials, configuration or vehicle input.
        if (_snapshotInspectionBusy || _closing)
            return;

        var review = new VolvoP2PinAuthorizationReview();
        _snapshotInspectionBusy = true;
        P2PinReviewButton.IsEnabled = false;
        P2ConfigCatalogButton.IsEnabled = false;
        InspectSnapshotButton.IsEnabled = false;
        CompareSnapshotsButton.IsEnabled = false;
        try
        {
            var description = new TextBlock
            {
                Text = review.DescribeCurrent() + Environment.NewLine +
                    VolvoP2CemVendorPinRouteResearch.BuildOfflineOverview(),
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            };
            // Compare only user-claimed year/engine/tool against the
            // source-limited vendor statements. No secrets or vehicle input,
            // saved state, adapter access, VIN, PIN or service operations.
            var routeYear = new TextBox
            {
                Header = "Claimed XC90 model year (2005-2014)",
                MaxLength = 4,
                PlaceholderText = "e.g. 2007"
            };
            var routeYearChecked = new CheckBox
            {
                Content = "I checked the model year separately (self-report, NOT authenticated)"
            };
            var routeEngine = new ComboBox
            {
                Header = "Claimed engine variant"
            };
            foreach (var name in new[]
            {
                "Unknown / not checked",
                "Other five-cylinder",
                "D5 147 kW",
                "D5 120 kW",
                "3.2 inline six",
                "4.4 V8",
                "Other / unclassified"
            })
                routeEngine.Items.Add(new ComboBoxItem { Content = name });
            routeEngine.SelectedIndex = 0;

            var routeTool = new ComboBox
            {
                Header = "Vendor product claim (not a Dvinholm action)"
            };
            routeTool.Items.Add(new ComboBoxItem { Content = "VDASH" });
            routeTool.Items.Add(new ComboBoxItem { Content = "VDD" });
            routeTool.SelectedIndex = 0;

            var routeResult = new TextBlock
            {
                Text = "No variant comparison yet. This is offline vendor research only.",
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            };
            var routePreview = new Button
            {
                Content = "Compare vendor claims (offline only)"
            };
            routePreview.Click += (_, _) =>
            {
                if (_closing)
                    return;
                var engine = routeEngine.SelectedIndex is >= 0 and <= 6
                    ? (VolvoP2CemClaimedEngine)routeEngine.SelectedIndex
                    : VolvoP2CemClaimedEngine.Unknown;
                var tool = routeTool.SelectedIndex == 1
                    ? VolvoP2CemVendorPinTool.Vdd
                    : VolvoP2CemVendorPinTool.Vdash;
                routeResult.Text = VolvoP2CemVendorPinRouteDisplay.Build(
                    routeYear.Text, routeYearChecked.IsChecked == true,
                    engine, tool);
            };

            void InvalidateRoutePreview() => routeResult.Text =
                "Selection changed. Compare again; no vehicle check was performed.";
            routeYear.TextChanged += (_, _) => InvalidateRoutePreview();
            routeYearChecked.Checked += (_, _) => InvalidateRoutePreview();
            routeYearChecked.Unchecked += (_, _) => InvalidateRoutePreview();
            routeEngine.SelectionChanged += (_, _) => InvalidateRoutePreview();
            routeTool.SelectionChanged += (_, _) => InvalidateRoutePreview();

            var content = new StackPanel { Spacing = 9 };
            content.Children.Add(description);
            content.Children.Add(routeYear);
            content.Children.Add(routeYearChecked);
            content.Children.Add(routeEngine);
            content.Children.Add(routeTool);
            content.Children.Add(routePreview);
            content.Children.Add(routeResult);

            var dialog = new ContentDialog
            {
                XamlRoot = RootLayout.XamlRoot,
                Title = "XC90 P2 CEM PIN readiness (offline only)",
                Content = new ScrollViewer { MaxHeight = 430, Content = content },
                PrimaryButtonText = "Next review step",
                CloseButtonText = "Cancel review",
                DefaultButton = ContentDialogButton.Close
            };
            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (review.Advance())
                {
                    // Keep the dialog open until all four evidence requirements
                    // have been displayed. This is NOT PIN-search progress.
                    args.Cancel = true;
                    description.Text = review.DescribeCurrent() + Environment.NewLine +
                        VolvoP2CemVendorPinRouteResearch.BuildOfflineOverview();
                    if (review.StepNumber == review.TotalSteps)
                        dialog.PrimaryButtonText = "Finish offline review";
                }
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                review.Cancel();
            if (!_closing)
                SetStatus(review.State == VolvoP2PinPreviewState.Cancelled
                    ? "Offline P2 PIN readiness review cancelled. No operation started."
                    : "Offline P2 PIN readiness review finished: unsupported. " +
                      "No PIN, credentials, vehicle requests or configuration changes.");
        }
        catch (Exception)
        {
            review.Cancel();
            if (!_closing)
                SetStatus("Could not display offline P2 PIN readiness review. " +
                          "No vehicle or security operation was attempted.");
        }
        finally
        {
            _snapshotInspectionBusy = false;
            if (!_closing)
            {
                P2PinReviewButton.IsEnabled = true;
                P2ConfigCatalogButton.IsEnabled = true;
                InspectSnapshotButton.IsEnabled = true;
                CompareSnapshotsButton.IsEnabled = true;
            }
        }
    }

    private async void P2ConfigCatalog_Click(object sender, RoutedEventArgs e)
    {
        // This is static research text only. Do not touch ECU selection,
        // diagnostic state, CAN/J2534, saved snapshots, or service access.
        if (_snapshotInspectionBusy || _closing)
            return;

        _snapshotInspectionBusy = true;
        P2PinReviewButton.IsEnabled = false;
        P2ConfigCatalogButton.IsEnabled = false;
        InspectSnapshotButton.IsEnabled = false;
        CompareSnapshotsButton.IsEnabled = false;
        try
        {
            // Ephemeral user-proposed value review sits WITHIN the existing
            // offline catalog dialog. It cannot open a vehicle session,
            // change an ECU profile or persist a draft.
            var draftYear = new TextBox
            {
                Header = "Claimed XC90 year (2003-2014; customer values 2003-2006)",
                MaxLength = 4,
                PlaceholderText = "e.g. 2005"
            };
            var draftYearCheck = new CheckBox
            {
                Content = "I separately checked the vehicle's model year (self-report only)"
            };
            var draftKind = new ComboBox
            {
                Header = "Historical customer setting"
            };
            foreach (var name in new[]
            {
                "Heated seat LOW/HIGH (Celsius)",
                "Approach lighting (seconds)",
                "Post-drying (ON/OFF)",
                "Daytime lamps (Flex mode)"
            })
                draftKind.Items.Add(new ComboBoxItem { Content = name });
            draftKind.SelectedIndex = 0;

            var draftValue = new TextBox
            {
                Header = "Proposed historical value (LOW for heated seats)",
                MaxLength = 32,
                PlaceholderText = "e.g. 37 or 30 or ON or FLEX_STANDARD"
            };
            var draftHigh = new TextBox
            {
                Header = "Proposed HIGH seat temperature (Celsius)",
                MaxLength = 3,
                PlaceholderText = "e.g. 41"
            };
            var manualNoteValue = new TextBox
            {
                Header = "Optional MANUAL reference notation (NOT an ECU read)",
                MaxLength = 32,
                PlaceholderText = "Leave empty to preview just the proposal"
            };
            var manualNoteHigh = new TextBox
            {
                Header = "Optional MANUAL reference HIGH (heated seats only)",
                MaxLength = 3,
                PlaceholderText = "e.g. 41"
            };
            draftKind.SelectionChanged += (_, _) =>
            {
                // Never carry a manual note into another parameter category.
                draftValue.Text = string.Empty;
                draftHigh.Text = string.Empty;
                manualNoteValue.Text = string.Empty;
                manualNoteHigh.Text = string.Empty;
                draftHigh.Visibility = draftKind.SelectedIndex == 0
                    ? Visibility.Visible : Visibility.Collapsed;
                manualNoteHigh.Visibility = draftHigh.Visibility;
            };

            var draftResult = new TextBlock
            {
                Text = "No draft preview yet. Values are not current vehicle readings.",
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            };
            var draftPreview = new Button
            {
                Content = "Preview / compare manual notes (offline, no save)"
            };
            // A prior review is no longer valid once any field is edited.
            foreach (var entry in new[]
                { draftYear, draftValue, draftHigh, manualNoteValue, manualNoteHigh })
                entry.TextChanged += (_, _) => draftResult.Text =
                    "Draft changed; review again. Nothing saved or read from the car.";
            draftYearCheck.Checked += (_, _) => draftResult.Text =
                "Year claim changed; review again. No vehicle verification.";
            draftYearCheck.Unchecked += (_, _) => draftResult.Text =
                "Year claim changed; review again. No vehicle verification.";
            draftKind.SelectionChanged += (_, _) => draftResult.Text =
                "Parameter changed; review again. No ECU values are available.";

            draftPreview.Click += (_, _) =>
            {
                if (_closing)
                    return;
                var kind = (VolvoP2CemCustomerParameterKind)draftKind.SelectedIndex;
                var seat = kind == VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius;
                if (!string.IsNullOrEmpty(manualNoteValue.Text) ||
                    !string.IsNullOrEmpty(manualNoteHigh.Text))
                {
                    draftResult.Text = VolvoP2CemManualValueComparison.BuildReadOnlyReport(
                        draftYear.Text, draftYearCheck.IsChecked == true,
                        kind, manualNoteValue.Text,
                        seat ? manualNoteHigh.Text : null,
                        draftValue.Text, seat ? draftHigh.Text : null);
                }
                else
                {
                    draftResult.Text = VolvoP2CemCustomerValueDraftPreview.Build(
                        draftYear.Text, draftYearCheck.IsChecked == true,
                        kind, draftValue.Text, seat ? draftHigh.Text : null);
                }
            };

            // This secondary, offline-only check joins the EXISTING
            // XC90 HS-CAN advisory and Volvo VIDA tool limitation.
            // It never uses selected adapter state or changes CAN speed.
            var transportNetwork = new ComboBox
            {
                Header = "Claimed network (research only)"
            };
            foreach (var name in new[]
            {
                "Not identified", "HS-CAN", "LS-CAN", "Other / K-line"
            })
                transportNetwork.Items.Add(new ComboBoxItem { Content = name });
            transportNetwork.SelectedIndex = 0;

            var transportAdapter = new ComboBox
            {
                Header = "Claimed interface type (NOT detected)"
            };
            foreach (var name in new[]
            {
                "Unspecified", "DiCE", "J2534-1", "J2534-2", "Other"
            })
                transportAdapter.Items.Add(new ComboBoxItem { Content = name });
            transportAdapter.SelectedIndex = 0;

            var transportRate = new ComboBox
            {
                Header = "Proposed rate (NOT applied to adapter)"
            };
            foreach (var name in new[]
            {
                "Unspecified", "125 kbit/s", "250 kbit/s", "500 kbit/s"
            })
                transportRate.Items.Add(new ComboBoxItem { Content = name });
            transportRate.SelectedIndex = 0;

            var transportReport = new TextBlock
            {
                Text = "Transport not checked; no physical interface was detected.",
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            };
            var transportReview = new Button
            {
                Content = "Review legacy transport risks (offline)"
            };
            // Recheck after every change; old results must never be mistaken
            // for a live adapter setting or a validated vehicle identity.
            void InvalidateTransport() => transportReport.Text =
                "Research inputs changed; transport has NOT been verified.";
            draftYear.TextChanged += (_, _) => InvalidateTransport();
            draftYearCheck.Checked += (_, _) => InvalidateTransport();
            draftYearCheck.Unchecked += (_, _) => InvalidateTransport();
            transportNetwork.SelectionChanged += (_, _) => InvalidateTransport();
            transportAdapter.SelectionChanged += (_, _) => InvalidateTransport();
            transportRate.SelectionChanged += (_, _) => InvalidateTransport();
            transportReview.Click += (_, _) =>
            {
                if (_closing)
                    return;
                var yearClaim = draftYear.Text;
                int? year = yearClaim.Length == 4 &&
                    int.TryParse(yearClaim, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsed) &&
                    yearClaim == parsed.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    ? parsed : null;
                uint? nominalRate = transportRate.SelectedIndex switch
                {
                    1 => 125_000u,
                    2 => 250_000u,
                    3 => 500_000u,
                    _ => null
                };
                var claimedNetwork = (VolvoP2NetworkKind)transportNetwork.SelectedIndex;
                var claimedInterface = (VolvoP2CemDiagnosticInterface)transportAdapter.SelectedIndex;
                // Only user-selected offline context. Never read connected adapter
                // state or convert workshop logical node labels into CAN IDs.
                transportReport.Text = VolvoP2CemTransportResearchPreflight.BuildReadOnlyReport(
                    year, draftYearCheck.IsChecked == true,
                    claimedNetwork, nominalRate, claimedInterface)
                    + Environment.NewLine + Environment.NewLine
                    + VolvoP2CemReadinessMatrix.BuildOfflineReport(
                        year, draftYearCheck.IsChecked == true,
                        claimedNetwork, nominalRate, claimedInterface);
            };

            var catalogContent = new StackPanel { Spacing = 8 };
            foreach (var control in new UIElement[]
            {
                new TextBlock
                {
                    Text = "UNVERIFIED LOCAL RESEARCH ONLY: proposed historical values, " +
                           "never live ECU values or write instructions. No VIN/PIN/secret input.",
                    TextWrapping = TextWrapping.Wrap
                },
                draftYear, draftYearCheck, draftKind, draftValue,
                draftHigh, manualNoteValue, manualNoteHigh, draftPreview, draftResult,
                new TextBlock
                {
                    Text = "XC90 P2 CEM LEGACY TRANSPORT RESEARCH (NO CONNECTION)",
                    TextWrapping = TextWrapping.Wrap
                },
                transportNetwork, transportAdapter, transportRate,
                transportReview, transportReport,
                new TextBlock
                {
                    Text = VolvoP2ConfigCandidateDisplay.BuildOverview(),
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                }
            })
                catalogContent.Children.Add(control);

            // User-triggered additional research comparison. The parent
            // dialog is hidden before opening either Windows file picker.
            var revisionCrossCheckButton = new Button
            {
                Content = "Cross-check CEM revision source JSON (offline)",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            catalogContent.Children.Add(revisionCrossCheckButton);
            var revisionCrossCheckRequested = false;

            var dialog = new ContentDialog
            {
                XamlRoot = RootLayout.XamlRoot,
                Title = "XC90 P2 configuration candidates (offline only)",
                Content = new ScrollViewer
                {
                    MaxHeight = 380,
                    Content = catalogContent
                },
                PrimaryButtonText = "Enter CEM identity (offline)",
                SecondaryButtonText = "Open CEM research JSON...",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close
            };
            revisionCrossCheckButton.Click += (_, _) =>
            {
                revisionCrossCheckRequested = true;
                dialog.Hide();
            };
            var choice = await dialog.ShowAsync();
            if (_closing)
                return;
            if (revisionCrossCheckRequested)
                await ShowP2CemRevisionSourceComparisonAsync();
            else if (choice == ContentDialogResult.Primary)
                await ShowP2CemVidaIdentityWorksheetAsync();
            else if (choice == ContentDialogResult.Secondary)
                await ShowP2CemIdentityResearchFileAsync();
            else
                SetStatus("Viewed offline P2 research categories: vendor claims and " +
                    "Volvo-documented service menu names, neither a vehicle configuration. " +
                    "No vehicle data read, PIN requested or ECU data changed.");
        }
        catch (Exception)
        {
            if (!_closing)
                SetStatus("Could not display the offline P2 research catalog. " +
                    "No vehicle or configuration operation was attempted.");
        }
        finally
        {
            _snapshotInspectionBusy = false;
            if (!_closing)
            {
                P2PinReviewButton.IsEnabled = true;
                P2ConfigCatalogButton.IsEnabled = true;
                InspectSnapshotButton.IsEnabled = true;
                CompareSnapshotsButton.IsEnabled = true;
            }
        }
    }


    /// <summary>
    /// Compare two explicitly selected, locally inspected unverified
    /// research files. No profile, CAN/J2534, CEM, PIN or write action.
    /// </summary>
    private async Task ShowP2CemRevisionSourceComparisonAsync()
    {
        try
        {
            var manualPicker = new FileOpenPicker();
            InitializeWithWindow.Initialize(manualPicker,
                WindowNative.GetWindowHandle(this));
            manualPicker.FileTypeFilter.Add(".json");
            var manualFile = await manualPicker.PickSingleFileAsync();
            if (manualFile is null || _closing)
                return;

            var manual = await VolvoP2CemIdentityResearchFileInspection
                .InspectFileAsync(manualFile.Path);
            if (_closing)
                return;

            var sourcePicker = new FileOpenPicker();
            InitializeWithWindow.Initialize(sourcePicker,
                WindowNative.GetWindowHandle(this));
            sourcePicker.FileTypeFilter.Add(".json");
            // One or more independent, user-selected research files. A
            // duplicate source reference is NOT independent confirmation.
            var sourceFiles = await sourcePicker.PickMultipleFilesAsync();
            if (sourceFiles is null || sourceFiles.Count == 0 || _closing)
                return;
            if (sourceFiles.Count > VolvoP2CemRevisionSourceSetReview.MaximumSources)
                throw new InvalidDataException("Too many CEM revision source files.");
            var claims = new List<VolvoP2CemRevisionSourceClaim>(sourceFiles.Count);
            foreach (var sourceFile in sourceFiles)
            {
                if (_closing)
                    return;
                claims.Add(await VolvoP2CemRevisionSourceComparison
                    .InspectFileAsync(sourceFile.Path));
            }
            if (_closing)
                return;

            var result = new ContentDialog
            {
                XamlRoot = RootLayout.XamlRoot,
                Title = "XC90 P2 CEM source-set review (UNVERIFIED)",
                Content = new ScrollViewer
                {
                    MaxHeight = 350,
                    Content = new TextBlock
                    {
                        Text = VolvoP2CemRevisionSourceSetReview
                            .BuildReadOnlyReport(manual, claims)
                            + Environment.NewLine
                            + VolvoP2CemConfigurationReadPreflight
                            .BuildReadOnlyReport(manual, claims)
                            + Environment.NewLine
                            + VolvoP2CemReadSurfaceEvidence
                            .BuildOfflineReport(manual.ClaimedModelYear),
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true
                    }
                },
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close
            };
            await result.ShowAsync();
            if (!_closing)
                SetStatus("Reviewed unverified local CEM revision source set. " +
                    "No ECU, PIN, adapter or configuration operation.");
        }
        catch (InvalidDataException)
        {
            if (!_closing)
                SetStatus("Rejected invalid CEM revision source research JSON. " +
                    "No file, profile or vehicle changed.");
        }
        catch (Exception)
        {
            if (!_closing)
                SetStatus("Could not inspect local CEM revision source research JSON. " +
                    "No vehicle connection or configuration operation.");
        }
    }

    /// <summary>
    /// Read-only review of a user-selected, untrusted local JSON research
    /// card. Never applies identifiers to profiles, CAN, J2534 or CEM.
    /// </summary>
    private async Task ShowP2CemIdentityResearchFileAsync()
    {
        try
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker,
                WindowNative.GetWindowHandle(this));
            picker.FileTypeFilter.Add(".json");
            var file = await picker.PickSingleFileAsync();
            if (file is null || _closing)
                return;

            var record = await VolvoP2CemIdentityResearchFileInspection
                .InspectFileAsync(file.Path);
            if (_closing)
                return;

            var preview = new ContentDialog
            {
                XamlRoot = RootLayout.XamlRoot,
                Title = "Saved CEM identity research (UNVERIFIED, offline)",
                Content = new ScrollViewer
                {
                    MaxHeight = 350,
                    Content = new TextBlock
                    {
                        Text = VolvoP2CemIdentityResearchFileInspection
                            .BuildReadOnlyReview(record),
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true
                    }
                },
                PrimaryButtonText = "Compare another JSON...",
                SecondaryButtonText = "Pair with DID snapshot...",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close
            };
            var choice = await preview.ShowAsync();
            if (_closing)
                return;
            if (choice == ContentDialogResult.Primary)
            {
                await ShowP2CemIdentityResearchComparisonAsync(record);
                return;
            }
            if (choice == ContentDialogResult.Secondary)
            {
                await ShowP2CemPairedResearchIntakeAsync(record);
                return;
            }

            SetStatus("Reviewed saved unverified CEM research JSON offline. " +
                "No profile changed and no ECU/PIN/configuration command sent.");
        }
        catch (InvalidDataException)
        {
            if (!_closing)
                SetStatus("Rejected CEM research JSON: invalid, unsafe or " +
                    "unsupported file. No ECU operation or profile change.");
        }
        catch (Exception)
        {
            if (!_closing)
                SetStatus("Could not open CEM research JSON. Check local " +
                    "file permissions and format. No ECU operation occurred.");
        }
    }

    /// <summary>
    /// Pair a user-selected CEM research card with a separately chosen
    /// historical diagnostic snapshot, without asserting that they belong
    /// to the same module or using any ECU communication.
    /// </summary>
    private async Task ShowP2CemPairedResearchIntakeAsync(
        VolvoP2CemIdentityResearchDocument manualClaim)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker,
            WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".json");
        var selected = await picker.PickSingleFileAsync();
        if (selected is null || _closing)
            return;

        var snapshot = await EcuConfigurationSnapshotInspection
            .InspectFileAsync(selected.Path);
        if (_closing)
            return;

        var report = VolvoP2CemPairedResearchIntakeDisplay
            .Build(manualClaim, snapshot);
        // Third-party scanner labels for a claimed year never establish
        // authenticated CEM revision or permissions. Show exact open gaps.
        report += Environment.NewLine +
            VolvoP2CemRevisionEvidencePreflight
                .BuildReadOnlyReport(manualClaim, snapshot);
        // A year/part-number research card and checksum-protected historic
        // DID export do NOT establish a physical CEM. The production
        // revision/DID evidence catalog is intentionally empty; show the
        // explicit revision-scoped read-only gap, without any live request.
        report += Environment.NewLine +
            VolvoP2CemHistoricalDidEvidence.BuildReadOnlyReport(
                manualClaim, snapshot, VolvoP2CemSupportMatrix.ProductionEntries);
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Paired P2/CEM research (UNVERIFIED, offline)",
            Content = new ScrollViewer
            {
                MaxHeight = 350,
                Content = new TextBlock
                {
                    Text = report,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                }
            },
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close
        };
        await dialog.ShowAsync();
        if (!_closing)
            SetStatus("Reviewed separate CEM identity claim and historical DID metadata " +
                "offline. Neither ECU identity nor configuration compatibility was verified.");
    }

    /// <summary>
    /// A second explicit file choice compares manual research claims only.
    /// The first record remains in memory; neither file is imported, saved,
    /// converted to an ECU identity signature or sent to J2534.
    /// </summary>
    private async Task ShowP2CemIdentityResearchComparisonAsync(
        VolvoP2CemIdentityResearchDocument first)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker,
            WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".json");
        var file = await picker.PickSingleFileAsync();
        if (file is null || _closing)
            return;

        var second = await VolvoP2CemIdentityResearchFileInspection
            .InspectFileAsync(file.Path);
        if (_closing)
            return;

        var report = VolvoP2CemIdentityResearchComparison
            .BuildReadOnlyDiff(first, second);
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Compare CEM research claims (UNVERIFIED, offline)",
            Content = new ScrollViewer
            {
                MaxHeight = 350,
                Content = new TextBlock
                {
                    Text = report,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                }
            },
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close
        };
        await dialog.ShowAsync();
        if (!_closing)
            SetStatus("Compared two offline CEM identity research cards. " +
                "No ECU identity was verified and no profile or vehicle was changed.");
    }

    /// <summary>
    /// Manual, offline VIDA identification notes. Never opens a J2534 session,
    /// imports raw DIDs, saves files or requests sensitive serial/VIN/PIN data.
    /// </summary>
    private async Task ShowP2CemVidaIdentityWorksheetAsync()
    {
        var year = new TextBox { Header = "XC90 model year (claimed)", MaxLength = 4 };
        var hardware = new TextBox { Header = "CEM hardware P/N", MaxLength = 40 };
        var software = new TextBox { Header = "CEM software P/N", MaxLength = 40 };
        var diagnostics = new TextBox
        {
            Header = "CEM diagnostic software P/N",
            MaxLength = 40
        };
        var fromVida = new CheckBox
        {
            Content = "I transcribed these three part numbers from VIDA"
        };
        var fields = new StackPanel { Spacing = 8 };
        fields.Children.Add(new TextBlock
        {
            Text = "Offline research notes only. Do NOT enter VIN, hardware serial, " +
                   "PIN, credentials or ECU response bytes. Source remains unverified.",
            TextWrapping = TextWrapping.Wrap
        });
        fields.Children.Add(year);
        fields.Children.Add(hardware);
        fields.Children.Add(software);
        fields.Children.Add(diagnostics);
        fields.Children.Add(fromVida);

        var enter = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "XC90 P2 CEM identity worksheet (offline)",
            Content = new ScrollViewer { MaxHeight = 380, Content = fields },
            PrimaryButtonText = "Review notes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await enter.ShowAsync() != ContentDialogResult.Primary || _closing)
            return;

        var result = VolvoP2CemVidaIdentityWorksheet.Assess(
            year.Text, hardware.Text, software.Text, diagnostics.Text,
            fromVida.IsChecked == true);

        var canCopy = result.Kind == VolvoP2CemVidaWorksheetKind.ManualClaimUnverified;
        var preview = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Manual CEM identity: unverified research only",
            Content = new ScrollViewer
            {
                MaxHeight = 350,
                Content = new TextBlock
                {
                    Text = result.Summary,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                }
            },
            PrimaryButtonText = canCopy ? "Copy worksheet" : "",
            SecondaryButtonText = canCopy ? "Save research JSON..." : "",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close
        };
        if (canCopy)
        {
            preview.PrimaryButtonClick += (_, args) =>
            {
                try
                {
                    var clipboardContent = new DataPackage();
                    clipboardContent.SetText(result.Summary);
                    Clipboard.SetContent(clipboardContent);
                }
                catch (Exception)
                {
                    args.Cancel = true;
                    if (!_closing)
                        SetStatus("Could not copy offline CEM identity notes.");
                }
            };
        }
        var choice = await preview.ShowAsync();
        if (_closing)
            return;

        if (choice == ContentDialogResult.Secondary && canCopy)
        {
            // The WinRT save picker creates a zero-byte placeholder before
            // authorization/path checks. Use the existing path-only picker
            // and atomic writer instead. No file on Cancel.
            try
            {
                var destinationPath = ReportSavePathPicker.PickPath(
                    WindowNative.GetWindowHandle(this),
                    "cem-identity-research", ".json", "JSON");
                if (destinationPath is null || _closing)
                {
                    if (!_closing)
                        SetStatus("CEM identity JSON export cancelled. No file saved.");
                    return;
                }

                await VolvoP2CemIdentityResearchExport.SaveToUserSelectedPathAsync(
                    year.Text, hardware.Text, software.Text, diagnostics.Text,
                    fromVida.IsChecked == true, destinationPath,
                    AppContext.BaseDirectory);
                if (!_closing)
                    SetStatus("Saved unverified manual CEM identity research JSON. " +
                        "Not a verified ECU or a restorable configuration backup.");
            }
            catch (IOException)
            {
                if (!_closing)
                    SetStatus("Could not save CEM research JSON. " +
                        "Choose a writable folder outside the application directory.");
            }
            catch (Exception)
            {
                if (!_closing)
                    SetStatus("CEM research JSON export failed. No ECU operation occurred.");
            }
            return;
        }

        SetStatus(choice == ContentDialogResult.Primary && canCopy
            ? "User-copied claimed CEM part numbers to clipboard. " +
              "No ECU identity or compatibility was verified."
            : "Offline CEM identity worksheet reviewed. " +
              "No diagnostic request, PIN, configuration edit or file write.");
    }

    private async void CompareSnapshots_Click(object sender, RoutedEventArgs e)
    {
        // Both file selections and the comparison are local-only. Never
        // populate any UDS controls, connect to J2534, or transmit CAN.
        if (_snapshotInspectionBusy || _closing)
            return;

        _snapshotInspectionBusy = true;
        P2PinReviewButton.IsEnabled = false;
        P2ConfigCatalogButton.IsEnabled = false;
        InspectSnapshotButton.IsEnabled = false;
        CompareSnapshotsButton.IsEnabled = false;
        try
        {
            SetStatus("Choose the first historical diagnostic JSON snapshot.");
            var firstPicker = new FileOpenPicker();
            InitializeWithWindow.Initialize(firstPicker, WindowNative.GetWindowHandle(this));
            firstPicker.FileTypeFilter.Add(".json");
            var first = await firstPicker.PickSingleFileAsync();
            if (first is null || _closing)
                return;

            var before = await EcuConfigurationSnapshotInspection.InspectFileAsync(first.Path);
            if (_closing)
                return;

            SetStatus("Choose the second historical diagnostic JSON snapshot.");
            var secondPicker = new FileOpenPicker();
            InitializeWithWindow.Initialize(secondPicker, WindowNative.GetWindowHandle(this));
            secondPicker.FileTypeFilter.Add(".json");
            var second = await secondPicker.PickSingleFileAsync();
            if (second is null || _closing)
                return;

            var after = await EcuConfigurationSnapshotInspection.InspectFileAsync(second.Path);
            if (_closing)
                return;

            var report = EcuHistoricalSnapshotComparer.Compare(before, after);
            // Analyze *two historical observations*, not an ECU identity.
            // Do not make the existing snapshot comparison depend on
            // chronology: equal timestamps still permit a normal DID diff.
            var repeatedSummary = before.CapturedAtUtc == after.CapturedAtUtc
                ? "Repeated-DID observation requires distinct UTC capture times; " +
                  "ordinary historical diff is still available."
                : EcuRepeatedDidEvidence.Compare(
                    before.CapturedAtUtc < after.CapturedAtUtc ? before : after,
                    before.CapturedAtUtc < after.CapturedAtUtc ? after : before).Summary;
            var dialog = new ContentDialog
            {
                XamlRoot = RootLayout.XamlRoot,
                Title = "Historical ECU DID comparison (offline)",
                Content = new ScrollViewer
                {
                    MaxHeight = 320,
                    Content = new TextBlock
                    {
                        Text = report.Summary + Environment.NewLine + Environment.NewLine +
                            repeatedSummary,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true
                    }
                },
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close
            };
            await dialog.ShowAsync();
            if (!_closing)
                SetStatus($"Compared {report.ComparedCount} historical DID(s); " +
                    $"{report.ChangedCount} DID(s) changed. Values hidden. " +
                    "No ECU request or configuration write was performed.");
        }
        catch (InvalidDataException)
        {
            SetStatus("Historical DID comparison rejected: invalid file, checksum, " +
                "CAN addressing, profile or DID set. No ECU operation was performed.");
        }
        catch (Exception)
        {
            // Never leak raw data, private paths or parser exception content.
            SetStatus("Historical DID comparison failed. Check local JSON files " +
                "and access permissions. No ECU operation was performed.");
        }
        finally
        {
            _snapshotInspectionBusy = false;
            if (!_closing)
            {
                P2PinReviewButton.IsEnabled = true;
                P2ConfigCatalogButton.IsEnabled = true;
                InspectSnapshotButton.IsEnabled = true;
                CompareSnapshotsButton.IsEnabled = true;
            }
        }
    }

    private async void InspectSnapshot_Click(object sender, RoutedEventArgs e)
    {
        // This action is fully offline, including when J2534 is disconnected.
        // Never apply historical CAN IDs, DIDs or profile metadata to the
        // current session, and never transmit a diagnostic request.
        if (_snapshotInspectionBusy || _closing)
            return;

        _snapshotInspectionBusy = true;
        P2PinReviewButton.IsEnabled = false;
        P2ConfigCatalogButton.IsEnabled = false;
        InspectSnapshotButton.IsEnabled = false;
        CompareSnapshotsButton.IsEnabled = false;
        try
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.FileTypeFilter.Add(".json");
            var selectedFile = await picker.PickSingleFileAsync();
            if (selectedFile is null || _closing)
                return;

            var inspected = await EcuConfigurationSnapshotInspection.InspectFileAsync(
                selectedFile.Path);
            if (_closing)
                return;

            var summary = EcuSnapshotInspectionDisplay.Build(inspected);
            // This second text is a deliberately redacted research *intake*,
            // never a supported CEM identification or decoded option map.
            var researchSummary = EcuCemResearchIntakeDisplay.Build(inspected);
            bool copiedResearchSummary = false;
            var dialog = new ContentDialog
            {
                XamlRoot = RootLayout.XamlRoot,
                Title = "Historical ECU DID snapshot (read-only)",
                Content = new ScrollViewer
                {
                    MaxHeight = 320,
                    Content = new TextBlock
                    {
                        Text = summary + Environment.NewLine + Environment.NewLine +
                            "Optional P2/CEM research metadata (select Copy to share):" +
                            Environment.NewLine + researchSummary,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true
                    }
                },
                PrimaryButtonText = "Copy sanitized research metadata",
                SecondaryButtonText = "Save redacted DID inventory TXT",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close
            };
            dialog.PrimaryButtonClick += (_, args) =>
            {
                // Copy only on explicit user action. Clipboard may outlive the
                // app, and even CAN addresses/DID lengths can fingerprint a car.
                try
                {
                    var content = new DataPackage();
                    content.SetText(researchSummary);
                    Clipboard.SetContent(content);
                    copiedResearchSummary = true;
                }
                catch (Exception)
                {
                    args.Cancel = true;
                    if (!_closing)
                        SetStatus("Could not copy historical research metadata. " +
                            "No file or vehicle data was changed.");
                }
            };

            var choice = await dialog.ShowAsync();
            if (_closing)
                return;

            if (choice == ContentDialogResult.Secondary)
            {
                // Path-only Save As and guarded atomic writer. No empty file
                // on Cancel, no automatic persistence, no ECU request.
                var destination = ReportSavePathPicker.PickPath(
                    WindowNative.GetWindowHandle(this),
                    "did-inventory-redacted", ".txt", "Text");
                if (destination is null || _closing)
                {
                    if (!_closing)
                        SetStatus("Redacted DID report cancelled. No file was created.");
                    return;
                }

                await EcuSanitizedDidReport.SaveAsync(
                    inspected, destination, AppContext.BaseDirectory);
                if (!_closing)
                    SetStatus("Saved a redacted historical DID inventory. " +
                        "No raw DID bytes, profile metadata, CAN addresses or VIN were exported. " +
                        "Review DID identifiers and lengths before sharing.");
                return;
            }

            if (!_closing)
                SetStatus(copiedResearchSummary
                    ? "Copied historical CEM research metadata (CAN and DID lengths). " +
                      "Review before sharing; no raw DID bytes or profile names copied."
                    : "Local diagnostic snapshot verified against its SHA-256. " +
                      "No vehicle data was imported, restored or transmitted.");
        }
        catch (InvalidDataException)
        {
            SetStatus("Local diagnostic snapshot rejected: invalid format, data or SHA-256. " +
                "No vehicle data was imported.");
        }
        catch (Exception)
        {
            // Do not echo untrusted paths, raw DID values or exception content
            // into the application's global status line.
            SetStatus("Could not inspect or save the selected diagnostic snapshot report. " +
                "Check the file, access permissions and disk. No ECU operation occurred.");
        }
        finally
        {
            _snapshotInspectionBusy = false;
            if (!_closing)
            {
                P2PinReviewButton.IsEnabled = true;
                P2ConfigCatalogButton.IsEnabled = true;
                InspectSnapshotButton.IsEnabled = true;
                CompareSnapshotsButton.IsEnabled = true;
            }
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
            RefreshSnapshotCaptureAvailability();
            return;
        }

        UdsRequestId.Text = profile.RequestId.ToString(profile.IsExtended ? "X8" : "X3");
        UdsResponseId.Text = profile.ResponseId.ToString(profile.IsExtended ? "X8" : "X3");
        CanIdFormat.SelectedIndex = profile.IsExtended ? 1 : 0;
        UdsDid.Text = profile.ReadableDataIdentifiers.Count > 0
            ? profile.ReadableDataIdentifiers.Order().First().ToString("X4")
            : string.Empty;

        EcuInfoText.Text = EcuProfileCatalogSearch.Describe(profile);
        RefreshSnapshotCaptureAvailability();
        SetStatus($"Applied profile '{profile.Name}' (request {UdsRequestId.Text}, response {UdsResponseId.Text}). No diagnostic request was sent.");
    }


    private static string CanBaudRateSettingsFile => Path.Combine(
        DvinholmUserDataDirectory.Current,
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
        DvinholmUserDataDirectory.Current,
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
            ConnectButton.IsEnabled = true;
            _udsGate.Release();
            SetUdsEnabled(_connected);
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
        RefreshSnapshotCaptureAvailability();
    }

    private void SnapshotCanIdTextChanged(object sender, TextChangedEventArgs e) =>
        RefreshSnapshotCaptureAvailability();

    private void SnapshotCanIdFormatChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshSnapshotCaptureAvailability();

    private bool CanCaptureSelectedSnapshot(EcuProfile? profile, bool busy)
    {
        if (CanIdFormat is null || UdsRequestId is null || UdsResponseId is null)
            return false;

        bool? extended = CanIdFormat.SelectedIndex switch
        {
            0 => false,
            1 => true,
            _ => null
        };

        uint? requestId = null;
        uint? responseId = null;
        if (extended is bool format &&
            TryParseCanId(UdsRequestId.Text, format, out var request, out _) &&
            TryParseCanId(UdsResponseId.Text, format, out var response, out _))
        {
            requestId = request;
            responseId = response;
        }

        return EcuSnapshotCaptureUiGate.CanCapture(
            profile, _connected && !_closing,
            DvinholmUserDataDirectory.IsAcceptanceLaunch, busy,
            requestId, responseId, extended);
    }

    private void RefreshSnapshotCaptureAvailability()
    {
        // XAML may fire its initial TextChanged/SelectionChanged before all
        // named controls exist. Never enable an action on partial UI state.
        if (CaptureSnapshotButton is null || ProfileSelector is null)
            return;

        CaptureSnapshotButton.IsEnabled = CanCaptureSelectedSnapshot(
            ProfileSelector.SelectedItem as EcuProfile,
            _udsGate.CurrentCount == 0 || _discoveryCts is not null);
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

#if DVINHOLM_DEVELOPMENT_FEATURES
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

#endif
    private async void CaptureSnapshot_Click(object sender, RoutedEventArgs e)
    {
        var profile = ProfileSelector.SelectedItem as EcuProfile;
        if (!CanCaptureSelectedSnapshot(profile,
                _udsGate.CurrentCount == 0 || _discoveryCts is not null))
        {
            SetStatus("Select a profile with DIDs, connect to J2534 and restore its exact CAN IDs before reading a snapshot.");
            return;
        }

        // Keep all UDS work exclusive, including the consent and Save As
        // dialogs. No diagnostic request is transmitted before confirmation.
        if (!await _udsGate.WaitAsync(0))
        {
            SetStatus("Another ECU operation is running.");
            return;
        }

        using var cts = new CancellationTokenSource();
        _udsCts = cts;
        try
        {
            SetUdsEnabled(false);
            DiscoverButton.IsEnabled = false;
            ConnectButton.IsEnabled = false;
            CancelUdsButton.IsEnabled = true;

            var consent = new ContentDialog
            {
                XamlRoot = RootLayout.XamlRoot,
                Title = "Read and export ECU DID values?",
                Content = new TextBlock
                {
                    Text = "Dvinholm will request the selected profile's DIDs using read-only UDS 0x22. " +
                           "The JSON export can contain a VIN or other sensitive vehicle data. " +
                           "It is a diagnostic snapshot, NOT a restorable ECU backup. " +
                           "No configuration or PIN operation will be performed. " +
                           "Read the data and choose a local file?",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = "Read and choose Save As",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            if (await consent.ShowAsync() != ContentDialogResult.Primary)
            {
                SetStatus("ECU snapshot cancelled. No diagnostic request was sent.");
                return;
            }

            cts.Token.ThrowIfCancellationRequested();
            if (_closing || !_connected ||
                !ReferenceEquals(ProfileSelector.SelectedItem, profile) ||
                !CanCaptureSelectedSnapshot(profile, busy: false))
            {
                SetStatus("ECU snapshot cancelled: session, profile or CAN addressing changed.");
                return;
            }

            if (!TryGetUdsClient(out var client, out var error) || client is null)
            {
                SetStatus(error);
                return;
            }

            using var udsClient = client;
            SetStatus("Reading selected ECU DIDs (read-only)...");
            var snapshot = await Task.Run(() =>
                EcuConfigurationReadback.Capture(profile!, udsClient, cts.Token));

            cts.Token.ThrowIfCancellationRequested();
            if (_closing || !_connected)
            {
                SetStatus("ECU snapshot discarded because the session closed.");
                return;
            }

            // Path-only Save As: cancelling never creates a placeholder file.
            var destinationPath = ReportSavePathPicker.PickPath(
                WindowNative.GetWindowHandle(this),
                "ecu-did-snapshot", ".json", "JSON");
            if (destinationPath is null)
            {
                SetStatus("ECU DID snapshot read, but export was cancelled. No file was saved.");
                return;
            }

            cts.Token.ThrowIfCancellationRequested();
            if (_closing || !_connected)
            {
                SetStatus("ECU snapshot export cancelled because the session closed.");
                return;
            }

            await EcuConfigurationSnapshotExport.SaveToUserSelectedPathAsync(
                snapshot, destinationPath, AppContext.BaseDirectory,
                userApprovedSensitiveExport: true);
            SetStatus($"Read {snapshot.Values.Count} ECU DID(s) and saved a sensitive diagnostic snapshot to {Path.GetFileName(destinationPath)}. Not a restorable backup.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("ECU DID snapshot cancelled. No file was saved.");
        }
        catch (UdsNegativeResponseException ex)
        {
            SetStatus("ECU DID snapshot rejected: " + ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus("ECU snapshot read/export failed: " +
                FormatSafeException(ex, "check the selected profile, adapter and destination."));
        }
        finally
        {
            _udsCts = null;
            CancelUdsButton.IsEnabled = false;
            DiscoverButton.IsEnabled = _connected && !_closing;
            ConnectButton.IsEnabled = !_closing;
            _udsGate.Release();
            SetUdsEnabled(_connected && !_closing);
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
            DiscoverButton.IsEnabled = _connected;
            ConnectButton.IsEnabled = true;
            _udsGate.Release();
            SetUdsEnabled(_connected);
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
        if (DvinholmUserDataDirectory.IsAcceptanceLaunch)
        {
            SetStatus("Offline acceptance mode: J2534 connections are disabled.");
            return;
        }
#if DVINHOLM_DEVELOPMENT_FEATURES
        // Never connect a real J2534 session while a synthetic demo is active.
        if (_demoTimer is not null)
        {
            SetStatus("Stop the offline CAN demo before connecting a J2534 adapter.");
            return;
        }
#endif

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
#if DVINHOLM_DEVELOPMENT_FEATURES
                _demoMonitorButton.IsEnabled = !_connected && !_closing;
#endif
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

#if DVINHOLM_DEVELOPMENT_FEATURES
        // Never combine previously generated demo rows with real CAN capture.
        if (_demoDataPresent)
            ClearDemoData();
        _demoMonitorButton.IsEnabled = false;
#endif

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
            {
                ConnectButton.IsEnabled = true;
#if DVINHOLM_DEVELOPMENT_FEATURES
                _demoMonitorButton.IsEnabled = !_connected;
#endif
            }
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    { if (_cts is null) StartMonitor(); else StopMonitor(); }

    private void StartMonitor()
    {
        if (!_connected) return;
#if DVINHOLM_DEVELOPMENT_FEATURES
        if (_demoTimer is not null) return;
#endif

        // A cancelled monitor may still be unwinding from a native adapter call.
        // Never start a second reader until the previous task has actually ended.
        if (_monitorTask is { IsCompleted: false })
        {
            SetStatus("Waiting for the previous CAN monitor to stop.");
            return;
        }

        _monitorTask = null;
        // The prior worker can finish before its UI cleanup callback runs.
        // Finalize its counter now so a restart does not lose known drops.
        if (_monitorSubscription is { } previous)
        {
            _rxDropTracker.Complete(previous.DroppedFrameCount);
            _monitorSubscription = null;
        }
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var subscription = _session.Bus.Subscribe();
        _monitorSubscription = subscription;
        _rxDropTracker.Start(subscription.DroppedFrameCount);
        UpdateRxDropCountText();

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
                        _rxDropTracker.Complete(subscription.DroppedFrameCount);
                        _monitorSubscription = null;
                        UpdateRxDropCountText();
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

        UpdateRxDropCountText();
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

    private void UpdateRxDropCountText()
    {
        var drops = _rxDropTracker.Snapshot(_monitorSubscription?.DroppedFrameCount);
        DroppedFrameText.Text = drops is long count
            ? $"{count:N0} dropped since clear"
            : "drops unknown";
    }

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

        // A multi-ID query is a display filter only. J2534 hardware filters
        // are deliberately restricted to one exact arbitration identifier.
        if (IdFilter.Text.Contains(','))
        {
            SetStatus("Hardware filter requires one CAN ID; comma-separated IDs are software-only.");
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
        // Clear the history and its matching drop-count epoch together.
        _rxDropTracker.Clear(_monitorSubscription?.DroppedFrameCount);
        _monitorHistory.Clear();
        _frames.Clear();
        _total = 0;
        _rateFrames = 0;
        _rate.Restart();
        FrameRateText.Text = "0 frames/s";
        UpdateFrameCountText();
        UpdateRxDropCountText();
    }

#if DVINHOLM_DEVELOPMENT_FEATURES
    private void ClearDemoData()
    {
        // A stopped demo keeps its data for CSV until explicitly cleared or
        // the user begins a genuine J2534 connection.
        ResetMonitorView();
        _demoDataPresent = false;
        _demoStatusText.Visibility = Visibility.Collapsed;
    }

#endif
    private void Clear_Click(object sender, RoutedEventArgs e)
    {
#if DVINHOLM_DEVELOPMENT_FEATURES
        if (_demoTimer is null && _demoDataPresent)
        {
            ClearDemoData();
            return;
        }
#endif
        ResetMonitorView();
    }

#if DVINHOLM_DEVELOPMENT_FEATURES
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
        _demoStatusText.Text = "DEMO ACTIVE — synthetic CAN frames only; no adapter or vehicle.";
        _demoStatusText.Visibility = Visibility.Visible;
        _demoMonitorButton.Content = "Stop demo";
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
        _demoMonitorButton.Content = "Start demo";
        if (_demoDataPresent)
            _demoStatusText.Text = "DEMO DATA — synthetic frames retained for filtering/CSV.";
        if (!_closing && !_connected && _connectTask is not { IsCompleted: false })
            ConnectButton.IsEnabled = true;
    }

#endif
    /// <summary>
    /// Inspect existing bounded CAN RX history without transmitting,
    /// opening a file or copying anything until explicit user approval.
    /// Even CAN IDs/DLC counts can fingerprint a vehicle.
    /// </summary>
    private async void CanRxSummary_Click(object sender, RoutedEventArgs e)
    {
        if (_closing || _canRxSummaryBusy)
            return;

        _canRxSummaryBusy = true;
        try
        {
#if DVINHOLM_DEVELOPMENT_FEATURES
            var synthetic = _demoDataPresent;
#else
            const bool synthetic = false;
#endif
            // Filter("") reads the retained RX history regardless of the
            // current software view filter. This method never starts CAN.
            var snapshotRows = _monitorHistory.Filter("");
            var queueDrops = _rxDropTracker.Snapshot(_monitorSubscription?.DroppedFrameCount);
            var report = CanMonitorRxResearchSummary.Build(
                snapshotRows, _monitorHistory.TotalReceived, queueDrops, synthetic);
            // Same UI-thread snapshot, with the same bounded validation and no
            // CAN payloads, timestamps, file writes or device access.
            var jsonMetadata = CanMonitorRxMetadataJson.Build(
                snapshotRows, _monitorHistory.TotalReceived, queueDrops, synthetic);

            var copied = false;
            var preview = new ContentDialog
            {
                XamlRoot = RootLayout.XamlRoot,
                Title = "CAN RX research summary (payload hidden)",
                Content = new ScrollViewer
                {
                    MaxHeight = 380,
                    Content = new TextBlock
                    {
                        Text = report,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true
                    }
                },
                PrimaryButtonText = "Copy RX metadata",
                SecondaryButtonText = "Copy JSON metadata",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close
            };
            preview.PrimaryButtonClick += (_, args) =>
            {
                try
                {
                    var package = new DataPackage();
                    package.SetText(report);
                    Clipboard.SetContent(package);
                    copied = true;
                }
                catch (Exception)
                {
                    args.Cancel = true;
                    if (!_closing)
                        SetStatus("CAN RX metadata could not be copied. No data was exported.");
                }
            };

            preview.SecondaryButtonClick += (_, args) =>
            {
                try
                {
                    var package = new DataPackage();
                    package.SetText(jsonMetadata);
                    Clipboard.SetContent(package);
                    copied = true;
                }
                catch (Exception)
                {
                    args.Cancel = true;
                    if (!_closing)
                        SetStatus("CAN RX JSON could not be copied. No data was exported.");
                }
            };

            await preview.ShowAsync();
            if (!_closing)
                SetStatus(copied
                    ? "Copied CAN RX ID/DLC metadata after approval. Review before sharing."
                    : "Reviewed buffered CAN RX metadata without copying or exporting.");
        }
        catch (InvalidDataException)
        {
            if (!_closing)
                SetStatus("Invalid CAN RX history metadata; research summary not created.");
        }
        catch (Exception)
        {
            if (!_closing)
                SetStatus("Could not display CAN RX metadata. No diagnostic request or file write.");
        }
        finally
        {
            _canRxSummaryBusy = false;
        }
    }

    private async void Csv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
#if DVINHOLM_DEVELOPMENT_FEATURES
            var name = _demoDataPresent ? "can-demo-SYNTHETIC" : "can-log";
            var synthetic = _demoDataPresent;
#else
            const string name = "can-log";
            const bool synthetic = false;
#endif
            var destinationPath = ReportSavePathPicker.PickPath(
                WindowNative.GetWindowHandle(this),
                name, ".csv", "CSV");
            if (destinationPath is null) return;

            // Exported CAN logs also belong outside the application folder.
            // Guard exported CAN captures in both production and Development.
            if (ReportExportDestinationGuard.IsInsideApplicationDirectory(
                    destinationPath, AppContext.BaseDirectory))
            {
                SetStatus(ReportExportDestinationGuard.BlockedMessage);
                return;
            }

            var lines = CanMonitorCsv.BuildLines(_frames, synthetic);
            await ReportExportFileWriter.WriteLinesAsync(destinationPath, lines);
#if DVINHOLM_DEVELOPMENT_FEATURES
            SetStatus(synthetic
                ? $"SYNTHETIC DEMO CSV saved: {Path.GetFileName(destinationPath)}."
                : $"CSV saved: {Path.GetFileName(destinationPath)}.");
#else
            SetStatus($"CSV saved: {Path.GetFileName(destinationPath)}.");
#endif
        }
        catch (Exception)
        {
            // Picker and filesystem failures are expected user-environment errors.
            // Surface them in the status bar instead of escaping an async-void UI handler.
            SetStatus("CSV export failed. Check destination permissions and available disk space.");
        }
    }
}
