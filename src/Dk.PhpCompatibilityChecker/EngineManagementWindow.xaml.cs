using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using Dk.PhpCompatibilityChecker.Models;
using Dk.PhpCompatibilityChecker.Services;

namespace Dk.PhpCompatibilityChecker;

/// <summary>Manual engine management; the active scanner is not changed until the next app start.</summary>
public partial class EngineManagementWindow : Window
{
    private readonly EnginePack _activePack;
    private readonly EnginePackInstaller? _installer;
    private readonly string _sourceDirectory;
    private readonly string? _bundledRoot;
    private readonly Func<EnginePackOffer, bool>? _confirmationForVerification;
    private CancellationTokenSource? _operation;
    private bool _busy;
    private bool _restartRequired;

    public bool RestartRequired => _restartRequired;

    public EngineManagementWindow(EnginePack activePack, EnginePackInstaller? installer,
        string sourceDirectory, string? bundledRoot, bool restartRequired = false,
        Func<EnginePackOffer, bool>? confirmationForVerification = null)
    {
        _activePack = activePack ?? throw new ArgumentNullException(nameof(activePack));
        _installer = installer;
        _sourceDirectory = sourceDirectory;
        _bundledRoot = bundledRoot;
        _confirmationForVerification = confirmationForVerification;
        _restartRequired = restartRequired;
        InitializeComponent();

        var info = activePack.EngineInfo;
        EngineDetailsTextBox.Text =
            "엔진 팩: " + activePack.PackId + Environment.NewLine
            + "실행 PHP CLI: " + info.PhpRuntimeVersion + Environment.NewLine
            + "PHPCompatibility: " + info.PhpCompatibilityVersion + Environment.NewLine
            + "PHP_CodeSniffer: " + info.PhpCodeSnifferVersion + Environment.NewLine
            + "PHPCSUtils: " + info.PhpcsUtilsVersion;
        EngineTargetsTextBox.Text = string.Join(", ", activePack.Targets.Select(t => "PHP " + t.Version));
        EngineLimitationsTextBox.Text = string.Join(Environment.NewLine,
            activePack.Targets.SelectMany(t => t.Limitations).Distinct(StringComparer.Ordinal));
        EngineUpdateStatusTextBlock.Text = installer is null
            ? "온라인 엔진 팩 채널이 아직 연결되지 않았습니다. 기본 오프라인 검사는 계속 사용할 수 있습니다."
            : "업데이트 확인을 누를 때만 온라인 목록을 조회합니다.";
        Closing += OnClosing;
        UpdateControls();
    }

    private void UpdateControls()
    {
        CheckEngineUpdateButton.IsEnabled = !_busy && !_restartRequired && _installer is not null;
        CancelEngineUpdateButton.IsEnabled = _busy && _operation is { IsCancellationRequested: false };
        RestorePreviousEngineButton.IsEnabled = RestoreBundledEngineButton.IsEnabled =
            !_busy && !_restartRequired && _installer is not null;
        CloseEngineManagementButton.IsEnabled = !_busy;
        EngineRestartNotice.Text = _restartRequired ? "엔진 변경이 준비됐습니다. 프로그램을 다시 실행하면 적용됩니다." : "";
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_busy)
        {
            e.Cancel = true;
            _operation?.Cancel();
        }
    }

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        if (_installer is null || _busy || _restartRequired || !CheckEngineUpdateButton.IsEnabled)
            return;
        _operation = new CancellationTokenSource();
        var token = _operation.Token;
        _busy = true;
        UpdateControls();
        try
        {
            EngineOfferTextBlock.Text = "";
            EngineUpdateStatusTextBlock.Text = "검증된 엔진 팩 목록을 확인하는 중입니다.";
            var offer = await _installer.FindUpdateAsync(_activePack, token);
            if (offer is null)
            {
                EngineUpdateStatusTextBlock.Text = "현재 사용 중인 엔진보다 새로운 호환 팩이 없습니다.";
                return;
            }

            var added = offer.Targets.Select(t => t.Version).Except(_activePack.Targets.Select(t => t.Version)).ToArray();
            EngineOfferTextBlock.Text =
                "새 엔진 팩: " + offer.PackId + Environment.NewLine
                + "지원 PHP: " + string.Join(", ", offer.Targets.Select(t => t.Version)) + Environment.NewLine
                + "추가된 목표: " + (added.Length == 0 ? "없음 (엔진 구성 갱신)" : string.Join(", ", added)) + Environment.NewLine
                + "실행 PHP: " + offer.PhpRuntimeVersion + Environment.NewLine
                + "PHPCompatibility: " + VersionOf(offer, "phpcompatibility/php-compatibility") + Environment.NewLine
                + "PHP_CodeSniffer: " + VersionOf(offer, "squizlabs/php_codesniffer") + Environment.NewLine
                + "다운로드: " + (offer.ArchiveBytes / 1048576d).ToString("F1") + " MiB";
            // Production always shows the native confirmation; the Windows GUI
            // verifier injects consent to avoid unreliable cross-thread dialog clicks.
            var approved = _confirmationForVerification?.Invoke(offer) ??
                (MessageBox.Show(this, EngineOfferTextBlock.Text + Environment.NewLine
                    + Environment.NewLine + "검증된 엔진 팩을 다운로드하여 다음 실행부터 사용하시겠습니까?",
                    "엔진 팩 업데이트", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
                    == MessageBoxResult.Yes);
            if (!approved)
            {
                EngineUpdateStatusTextBlock.Text = "다운로드를 시작하지 않았습니다.";
                return;
            }

            token.ThrowIfCancellationRequested();
            EngineUpdateStatusTextBlock.Text = "엔진 팩 다운로드 중입니다.";
            EngineDownloadProgress.Value = 0;
            EngineDownloadProgress.Visibility = Visibility.Visible;
            var progress = new Progress<long>(bytes =>
            {
                if (!_busy || token.IsCancellationRequested)
                    return;
                var percentage = Math.Clamp(bytes * 100d / offer.ArchiveBytes, 0, 100);
                EngineDownloadProgress.Value = percentage;
                EngineUpdateStatusTextBlock.Text = "엔진 팩 다운로드: " + percentage.ToString("F0") + "% (완료 후 구성과 해시를 검증합니다.)";
            });
            await _installer.InstallAsync(offer, _sourceDirectory, token, progress);
            _restartRequired = true;
            EngineUpdateStatusTextBlock.Text = "엔진 팩을 설치했습니다. 프로그램을 다시 실행하면 적용됩니다.";
        }
        catch (OperationCanceledException)
        {
            EngineUpdateStatusTextBlock.Text = "엔진 팩 다운로드 또는 확인을 중단했습니다.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            or HttpRequestException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            EngineUpdateStatusTextBlock.Text = "엔진 팩을 적용하지 못했습니다: " + ex.Message;
        }
        finally
        {
            EngineDownloadProgress.Visibility = Visibility.Collapsed;
            _busy = false;
            _operation?.Dispose();
            _operation = null;
            UpdateControls();
        }
    }

    private static string VersionOf(EnginePackOffer offer, string name) =>
        offer.EngineVersions.TryGetValue(name, out var version) ? version : "확인 불가";

    private async void OnRestorePrevious(object sender, RoutedEventArgs e) =>
        await RestoreAsync(previous: true);

    private async void OnRestoreBundled(object sender, RoutedEventArgs e) =>
        await RestoreAsync(previous: false);

    private async Task RestoreAsync(bool previous)
    {
        if (_installer is null || _busy || _restartRequired)
            return;
        if (!previous && _bundledRoot is not null
            && Path.TrimEndingDirectorySeparator(Path.GetFullPath(_activePack.RootDirectory))
                .Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(_bundledRoot)), StringComparison.OrdinalIgnoreCase))
        {
            EngineUpdateStatusTextBlock.Text = "이미 기본 내장 엔진을 사용하고 있습니다.";
            return;
        }
        if (MessageBox.Show(this, (previous ? "이전 검증 엔진" : "기본 내장 엔진")
            + "을 다음 실행부터 사용하시겠습니까?", "엔진 복원", MessageBoxButton.YesNo,
            MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;
        _operation = new CancellationTokenSource();
        _busy = true;
        UpdateControls();
        try
        {
            EngineUpdateStatusTextBlock.Text = "검증된 엔진 팩을 확인하고 복원하는 중입니다.";
            if (previous)
                await _installer.RestorePreviousAsync(_sourceDirectory, _operation.Token);
            else
                await _installer.RestoreBundledAsync(_sourceDirectory, _operation.Token);
            _restartRequired = true;
            EngineUpdateStatusTextBlock.Text = "복원할 엔진을 선택했습니다. 프로그램을 다시 실행하면 적용됩니다.";
        }
        catch (OperationCanceledException)
        {
            EngineUpdateStatusTextBlock.Text = "엔진 복원을 중단했습니다.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            EngineUpdateStatusTextBlock.Text = "엔진을 복원하지 못했습니다: " + ex.Message;
        }
        finally
        {
            _busy = false;
            _operation?.Dispose();
            _operation = null;
            UpdateControls();
        }
    }

    private void OnCancelUpdate(object sender, RoutedEventArgs e) => _operation?.Cancel();

    private void OnClose(object sender, RoutedEventArgs e)
    {
        if (!_busy)
            Close();
    }
}
