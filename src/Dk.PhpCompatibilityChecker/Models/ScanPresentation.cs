using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace Dk.PhpCompatibilityChecker.Models;

/// <summary>Bindable input and result text for the single scan window; never executes a scan.</summary>
public sealed class ScanPresentation : INotifyPropertyChanged
{
    private const string StaticAnalysisLimit =
        "정적 분석 결과이며 실제 PHP 실행, DB·외부 라이브러리 및 서비스의 정상 동작을 보장하지 않습니다.";

    private string _sourceDirectory = "";
    private PhpTarget? _selectedTarget;
    private bool _includeSubdirectories = true;
    private IReadOnlyList<PhpTarget> _targets = Array.Empty<PhpTarget>();
    private bool _hasPack;
    private string _packFailure = "";
    private ScanStatus _status = ScanStatus.Idle;
    private ScanResult? _result;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SourceDirectory
    {
        get => _sourceDirectory;
        set
        {
            if (!CanEditInputs || _sourceDirectory == (value ?? ""))
                return;
            _sourceDirectory = value ?? "";
            ResetResult();
            NotifyAll();
        }
    }

    public PhpTarget? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (!CanEditInputs)
                return;
            // Only instances supplied by this verified pack may become selected.
            var selected = value is null ? null : _targets.FirstOrDefault(t => ReferenceEquals(t, value));
            if (ReferenceEquals(_selectedTarget, selected))
                return;
            _selectedTarget = selected;
            ResetResult();
            NotifyAll();
        }
    }

    public bool IncludeSubdirectories
    {
        get => _includeSubdirectories;
        set
        {
            if (!CanEditInputs || _includeSubdirectories == value)
                return;
            _includeSubdirectories = value;
            ResetResult();
            NotifyAll();
        }
    }

    public IReadOnlyList<PhpTarget> Targets => _targets;
    public IReadOnlyList<ScanIssue> Issues => _result?.Issues ?? Array.Empty<ScanIssue>();
    public IReadOnlyList<string> ExcludedPaths => _result?.ExcludedPaths ?? Array.Empty<string>();
    public ScanStatus Status => _status;
    public bool CanEditInputs => _status != ScanStatus.Running;
    public bool CanPrepareScan => CanEditInputs && _hasPack && _selectedTarget is not null
        && _targets.Any(t => ReferenceEquals(t, _selectedTarget)) && Directory.Exists(_sourceDirectory);

    public string StatusText => _status switch
    {
        ScanStatus.Running => "검사 중",
        ScanStatus.Completed => "완료",
        ScanStatus.NoFiles => "검사 대상 없음",
        ScanStatus.Failed => "검사 실패",
        _ => "대기"
    };

    public string SummaryText => _status switch
    {
        ScanStatus.Running => "검사 중입니다. 결과가 확인될 때까지 기다려 주세요.",
        ScanStatus.Completed when Issues.Count == 0 && _result is { ErrorCount: 0, WarningCount: 0 } =>
            "정적 검사에서 문제를 발견하지 못했습니다.",
        ScanStatus.Completed => "정적 검사 결과를 확인하세요.",
        ScanStatus.NoFiles => "검사할 .php/.inc 파일이 없습니다. 호환성 통과를 뜻하지 않습니다.",
        ScanStatus.Failed when Issues.Count > 0 =>
            "검사가 완료되지 않았습니다. 아래 문제는 확인된 일부 결과입니다.",
        ScanStatus.Failed => "검사를 완료하지 못했습니다. 실패 이유를 확인하세요.",
        _ => "검사할 폴더와 목표 PHP 버전을 선택하세요."
    };

    public string FailureText => _result?.FailureMessage ?? _packFailure;
    public string SelectedFileCountText => _result is null ? UnavailableCount : Count(_result.SelectedFileCount);
    public string ProcessedFileCountText => _result is null ? UnavailableCount
        : _result.ProcessedFileCount is int count ? Count(count) : "미확인";
    public string ErrorCountText => _result is null ? UnavailableCount : Count(_result.ErrorCount);
    public string WarningCountText => _result is null ? UnavailableCount : Count(_result.WarningCount);

    public string LimitationsText
    {
        get
        {
            var limitations = _selectedTarget?.Limitations ?? Array.Empty<string>();
            return string.Join(Environment.NewLine, new[] { StaticAnalysisLimit }
                .Concat(limitations).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal));
        }
    }

    public string InputMessage
    {
        get
        {
            if (!string.IsNullOrEmpty(_packFailure))
                return _packFailure;
            if (!_hasPack)
                return "검증된 엔진 팩이 준비되지 않았습니다.";
            if (_targets.Count == 0)
                return "엔진 팩에 검증된 목표 PHP 버전이 없습니다.";
            if (string.IsNullOrWhiteSpace(_sourceDirectory))
                return "검사할 소스 폴더를 선택하세요.";
            if (!Directory.Exists(_sourceDirectory))
                return "존재하는 소스 폴더를 선택하세요.";
            if (_selectedTarget is null)
                return "목표 PHP 버전을 선택하세요.";
            return "";
        }
    }

    public void SetPack(EnginePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var previousVersion = _selectedTarget?.Version;
        _targets = Array.AsReadOnly(pack.Targets.ToArray());
        _selectedTarget = previousVersion is null ? null : _targets.FirstOrDefault(t => t.Version == previousVersion);
        _hasPack = true;
        _packFailure = "";
        _result = null;
        _status = ScanStatus.Idle;
        NotifyAll();
    }

    public void SetPackFailure(string message)
    {
        _targets = Array.Empty<PhpTarget>();
        _selectedTarget = null;
        _hasPack = false;
        _packFailure = message ?? "";
        _result = null;
        _status = ScanStatus.Failed;
        NotifyAll();
    }

    public void ShowRunning()
    {
        _packFailure = "";
        _result = null;
        _status = ScanStatus.Running;
        NotifyAll();
    }

    public void ApplyResult(ScanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        // Keep the displayed inputs tied to these results, even after a prior input edit.
        _sourceDirectory = result.Request.SourceDirectory;
        _includeSubdirectories = result.Request.IncludeSubdirectories;
        _selectedTarget = _targets.FirstOrDefault(t => t.Version == result.Request.TargetPhpVersion);
        _packFailure = "";
        _result = result with
        {
            Issues = Array.AsReadOnly(result.Issues.ToArray()),
            ExcludedPaths = Array.AsReadOnly(result.ExcludedPaths.ToArray())
        };
        _status = result.Status;
        NotifyAll();
    }

    private string UnavailableCount => _status == ScanStatus.Idle ? "—" : "미확인";
    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    private void ResetResult()
    {
        _result = null;
        _status = string.IsNullOrEmpty(_packFailure) ? ScanStatus.Idle : ScanStatus.Failed;
    }

    private void NotifyAll()
    {
        foreach (var name in new[]
        {
            nameof(SourceDirectory), nameof(SelectedTarget), nameof(IncludeSubdirectories), nameof(Targets),
            nameof(Issues), nameof(ExcludedPaths), nameof(Status), nameof(StatusText), nameof(SummaryText),
            nameof(FailureText), nameof(SelectedFileCountText), nameof(ProcessedFileCountText),
            nameof(ErrorCountText), nameof(WarningCountText), nameof(LimitationsText), nameof(InputMessage),
            nameof(CanEditInputs), nameof(CanPrepareScan)
        })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
