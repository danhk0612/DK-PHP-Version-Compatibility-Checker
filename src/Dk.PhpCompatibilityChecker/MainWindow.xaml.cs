using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Dk.PhpCompatibilityChecker.Models;
using Dk.PhpCompatibilityChecker.Services;
using Microsoft.Win32;

namespace Dk.PhpCompatibilityChecker;

public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _windowLifetime = new();
    private ScanService? _scanService;
    private Task? _packLoadTask;
    private Task? _scanTask;
    private bool _loadingPack;
    private bool _closing;
    private bool _allowClose;

    public ScanPresentation Presentation { get; }

    public MainWindow() : this(new ScanPresentation())
    {
        Loaded += OnDefaultLoaded;
    }

    // The injected presentation lets the Windows verification harness display its
    // own clearly identified fixtures without adding a product demo/test mode.
    public MainWindow(ScanPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        Presentation = presentation;
        InitializeComponent();
        DataContext = Presentation;
        Presentation.PropertyChanged += OnPresentationChanged;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    public Task LoadPackAsync(string packDirectory)
    {
        VerifyAccess();
        if (_closing || _allowClose)
            return Task.CompletedTask;
        if (_loadingPack || _scanTask is { IsCompleted: false })
            throw new InvalidOperationException("팩 준비 또는 검사가 진행 중입니다.");
        _loadingPack = true;
        UpdateScanButton();
        _packLoadTask = LoadPackCoreAsync(packDirectory);
        return _packLoadTask;
    }

    private async Task LoadPackCoreAsync(string packDirectory)
    {
        var previousService = _scanService;
        try
        {
            if (previousService is not null)
                await previousService.DisposeAsync();
            _scanService = null;
            var pack = await new EnginePackLoader().LoadAsync(packDirectory, _windowLifetime.Token);
            if (_closing)
                return;
            _scanService = new ScanService(pack.RootDirectory);
            Presentation.SetPack(pack);
        }
        catch (OperationCanceledException) when (_closing) { }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            if (!_closing)
                Presentation.SetPackFailure(ex.Message);
        }
        finally
        {
            _loadingPack = false;
            UpdateScanButton();
        }
    }

    private async void OnDefaultLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnDefaultLoaded;
        await LoadPackAsync(Path.Combine(AppContext.BaseDirectory, "tools", "runtime"));
    }

    private void OnPresentationChanged(object? sender, PropertyChangedEventArgs e) => UpdateScanButton();

    private void UpdateScanButton() => ScanButton.IsEnabled = !_closing && !_loadingPack
        && _scanService is not null && Presentation.CanPrepareScan;

    private async void OnScan(object sender, RoutedEventArgs e)
    {
        // Check again in the handler: queued or programmatic clicks must not
        // bypass the disabled control or start a second engine invocation.
        if (_closing || _loadingPack || _scanService is null || !Presentation.CanPrepareScan
            || _scanTask is { IsCompleted: false })
            return;
        var request = new ScanRequest(Presentation.SourceDirectory, Presentation.SelectedTarget!.Version,
            Presentation.IncludeSubdirectories);
        _scanTask = RunScanAsync(_scanService, request);
        await _scanTask;
    }

    private async Task RunScanAsync(ScanService service, ScanRequest request)
    {
        Presentation.ShowRunning();
        try
        {
            var result = await service.ScanAsync(request);
            if (!_closing)
                Presentation.ApplyResult(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or OperationCanceledException)
        {
            if (!_closing)
                Presentation.ApplyResult(new ScanResult(request, ScanStatus.Failed, 0, null, 0, 0,
                    Array.Empty<ScanIssue>(), null, ex.Message));
        }
        finally { UpdateScanButton(); }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;
        e.Cancel = true;
        if (_closing)
            return;
        _closing = true;
        IsEnabled = false;
        _windowLifetime.Cancel();
        UpdateScanButton();
        // Let the original Closing event finish before calling Close again,
        // including the idle case where disposal completes synchronously.
        await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.Background);
        try
        {
            if (_packLoadTask is not null)
                await _packLoadTask;
            if (_scanService is not null)
                await _scanService.DisposeAsync();
            if (_scanTask is not null)
                await _scanTask;
            _allowClose = true;
            Close();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            // Keep the window alive if ownership cleanup failed; never silently
            // claim that a child process was stopped or leave an async-void fault.
            Presentation.SetPackFailure("종료 정리에 실패했습니다: " + ex.Message);
            _closing = false;
            IsEnabled = true;
            UpdateScanButton();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Presentation.PropertyChanged -= OnPresentationChanged;
        Loaded -= OnDefaultLoaded;
        _windowLifetime.Dispose();
    }

    private void OnFolderBrowse(object sender, RoutedEventArgs e)
    {
        if (_closing || !Presentation.CanEditInputs)
            return;
        var dialog = new OpenFolderDialog
        {
            Title = "검사할 PHP 소스 폴더 선택",
            Multiselect = false,
            AddToRecent = false
        };
        if (Directory.Exists(Presentation.SourceDirectory))
            dialog.InitialDirectory = Presentation.SourceDirectory;
        if (dialog.ShowDialog(this) == true && !_closing)
            Presentation.SourceDirectory = dialog.FolderName;
    }
}
