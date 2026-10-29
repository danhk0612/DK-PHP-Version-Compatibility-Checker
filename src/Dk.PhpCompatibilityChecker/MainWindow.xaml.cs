using System.IO;
using System.Windows;
using Dk.PhpCompatibilityChecker.Models;
using Dk.PhpCompatibilityChecker.Services;
using Microsoft.Win32;

namespace Dk.PhpCompatibilityChecker;

public partial class MainWindow : Window
{
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
    }

    public async Task LoadPackAsync(string packDirectory)
    {
        try
        {
            var pack = await new EnginePackLoader().LoadAsync(packDirectory);
            Presentation.SetPack(pack);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            Presentation.SetPackFailure(ex.Message);
        }
    }

    private async void OnDefaultLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnDefaultLoaded;
        await LoadPackAsync(Path.Combine(AppContext.BaseDirectory, "tools", "runtime"));
    }

    private void OnFolderBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "검사할 PHP 소스 폴더 선택",
            Multiselect = false,
            AddToRecent = false
        };
        if (Directory.Exists(Presentation.SourceDirectory))
            dialog.InitialDirectory = Presentation.SourceDirectory;
        if (dialog.ShowDialog(this) == true)
            Presentation.SourceDirectory = dialog.FolderName;
    }
}
