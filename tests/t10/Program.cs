using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Dk.PhpCompatibilityChecker;
using Dk.PhpCompatibilityChecker.Models;
using Dk.PhpCompatibilityChecker.Services;

internal static class Program
{
    static readonly List<string> Passed = [];
    static readonly List<object> Failures = [];
    static readonly List<object> Runs = [];
    static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    static string Repo = "", Packs = "", Output = "", Active = "", Subject = "";
    static MainWindow? Window;
    static readonly List<MainWindow> Windows = [];
    static readonly Dictionary<string, string> BaselineResults = [];
    static string Hash(string p) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant();
    static SortedDictionary<string,string> Snapshot(string root) => new(Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(p=>Path.GetRelativePath(root,p),Hash));
    static void Check(bool value, string text) { if (!value) throw new InvalidOperationException(text); }
    static void Pump() { var f=new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>f.Continue=false)); Dispatcher.PushFrame(f); }
    static void Until(Func<bool> ready) { var end=DateTime.UtcNow.AddSeconds(60); while(!ready()&&DateTime.UtcNow<end){Pump();Thread.Sleep(10);} Check(ready(),"Timed out");Pump(); }
    static void Await(Task task) { Until(()=>task.IsCompleted);task.GetAwaiter().GetResult();Pump(); }
    static T Await<T>(Task<T> task){Until(()=>task.IsCompleted);return task.GetAwaiter().GetResult();}
    static void Case(string name, Action action){try{action();Passed.Add(name);Console.WriteLine("PASS "+name);}catch(Exception e){Failures.Add(new{name,error=e.ToString()});Console.WriteLine("FAIL "+name+": "+e.Message);}}
    static void Copy(string from,string to){Directory.CreateDirectory(to);foreach(var p in Directory.GetFiles(from,"*",SearchOption.AllDirectories)){var d=Path.Combine(to,Path.GetRelativePath(from,p));Directory.CreateDirectory(Path.GetDirectoryName(d)!);File.Copy(p,d);}}
    static void Close(){if(Window is null)return;Window.Close();Until(()=>!Window.IsVisible);Window=null;}
    static void Activate(string label){Close();if(Directory.Exists(Active))Directory.Delete(Active,true);Copy(Path.Combine(Packs,label),Active);}
    static MainWindow Open(){var w=new MainWindow(new ScanPresentation());Windows.Add(w);w.Title="T10 development pack exchange verification";w.Show();Await(w.LoadPackAsync(Active));Check(new WindowInteropHelper(w).Handle!=IntPtr.Zero&&w.IsVisible,"No shown WPF HWND");Window=w;return w;}
    static void Targets(params string[] expected){var w=Open();Check(w.Presentation.Targets.Select(t=>t.Version).SequenceEqual(expected),"WPF targets differ after restart");}
    static void GuiScan(string target){var w=Window!;var p=w.Presentation;p.SourceDirectory=Path.Combine(Subject,"compatibility");p.SelectedTarget=p.Targets.Single(t=>t.Version==target);var b=(Button)w.FindName("ScanButton");Check(b.IsEnabled,"Scan button unavailable");b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Until(()=>p.Status!=ScanStatus.Running);Check(p.Status==ScanStatus.Completed,"GUI scan failed");Runs.Add(new{origin="actual WPF button and PHPCompatibility",target,status=p.Status.ToString(),errors=p.ErrorCountText,warnings=p.WarningCountText});}
    static void Regression(bool restore){
        using var cases=JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo,"tests/fixtures/cases.json")));
        var pack=Await(new EnginePackLoader().LoadAsync(Active));
        foreach(var c in cases.RootElement.GetProperty("cases").EnumerateArray())foreach(var t in c.GetProperty("targets").EnumerateObject()){
            if(!pack.Targets.Any(x=>x.Version==t.Name))continue;
            string file=c.GetProperty("file").GetString()!, key=file+"@"+t.Name;
            Await(Scan(file,t.Name,t.Value,key,restore));
        }
    }
    static async Task Scan(string file,string target,JsonElement expectation,string key,bool restore){
        var temp=Path.Combine(Output,"single-case");if(Directory.Exists(temp))Directory.Delete(temp,true);Directory.CreateDirectory(temp);File.Copy(Path.Combine(Subject,file),Path.Combine(temp,Path.GetFileName(file)));
        await using var service=new ScanService(Active);
        var r=await service.ScanAsync(new ScanRequest(temp,target,true));
        Check(r.Status==ScanStatus.Completed&&r.ProcessedFileCount==1,"Actual fixture scan failed "+key);
        var kind=expectation.GetProperty("expectation").GetString();
        if(kind=="finding")Check(r.Issues.Any(x=>x.Source.StartsWith(expectation.GetProperty("source_prefix").GetString()!,StringComparison.Ordinal)&&x.Line==expectation.GetProperty("line").GetInt32()&&(!expectation.TryGetProperty("type",out var type)||x.Type==type.GetString())),"Expected finding absent "+key);
        if(kind=="no_findings")Check(r.Issues.Count==0&&r.EngineExitCode==0,"Unexpected finding "+key);
        var normalized=JsonSerializer.Serialize(new{r.Status,r.ProcessedFileCount,r.ErrorCount,r.WarningCount,r.EngineExitCode,issues=r.Issues.Select(x=>new{file=Path.GetFileName(x.FilePath),x.Line,x.Column,x.Type,x.Severity,x.Source,x.Message,x.Fixable})});
        if(restore)Check(BaselineResults[key]==normalized,"Restored fixture result differs "+key);else if(BaselineResults.TryGetValue(key,out var previous))Check(previous==normalized,"Common-target result changed after exchange "+key);else BaselineResults[key]=normalized;
        Runs.Add(new{origin="actual fixed engine fixture",key,restore,result=JsonDocument.Parse(normalized).RootElement.Clone()});
    }
    static void ChildProbe(string label,string target,params string[] expected){
        var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Repo};
        if(Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet",StringComparison.OrdinalIgnoreCase))info.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach(var arg in new[]{"--child",Repo,Active,Subject,Output,target,string.Join(",",expected),label})info.ArgumentList.Add(arg);
        using var process=Process.Start(info)??throw new InvalidOperationException("Could not start independent verification child");
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(60000)){process.Kill(true);throw new InvalidOperationException("Independent WPF process timed out");}
        Check(process.ExitCode==0,"Independent WPF process failed: "+stderr.GetAwaiter().GetResult()+stdout.GetAwaiter().GetResult());
        using var result=JsonDocument.Parse(File.ReadAllText(Path.Combine(Output,"child-"+label+".json")));
        Check(result.RootElement.GetProperty("productAssemblySha256").GetString()==Hash(typeof(MainWindow).Assembly.Location),"Child used a different product assembly");
        Runs.Add(new{origin="independent verification OS process and actual WPF product assembly",label,pid=process.Id,output="child-"+label+".json",result=result.RootElement.Clone()});
    }
    static int Child(string[] args){
        Repo=Path.GetFullPath(args[1]);Active=Path.GetFullPath(args[2]);Subject=Path.GetFullPath(args[3]);Output=Path.GetFullPath(args[4]);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try{Targets(args[6].Split(','));GuiScan(args[5]);Close();File.WriteAllText(Path.Combine(Output,"child-"+args[7]+".json"),JsonSerializer.Serialize(new{scopePassed=true,pid=Environment.ProcessId,targets=args[6].Split(','),productAssemblySha256=Hash(typeof(MainWindow).Assembly.Location),runs=Runs},Pretty),new UTF8Encoding(false));return 0;}
        finally{foreach(var w in Windows)if(w.IsVisible){w.Close();Until(()=>!w.IsVisible);}app.Shutdown();}
    }
    static void Bad(string name,Action<JsonNode> patch,bool corrupt=false,bool mix=false){
        Activate("full");var path=Path.Combine(Active,"manifest.json");var m=JsonNode.Parse(File.ReadAllText(path))!;patch(m);File.WriteAllText(path,m.ToJsonString(),new UTF8Encoding(false));
        if(corrupt)File.AppendAllText(Path.Combine(Active,"engine/ruleset.xml"),"corruption");
        if(mix)File.Copy(Path.Combine(Packs,"reduced/manifest.json"),path,true);
        var w=Open();Check(w.Presentation.Targets.Count==0&&!((Button)w.FindName("ScanButton")).IsEnabled,"Invalid pack exposed targets/scan: "+name);
        bool rejected=false;try{Await(new EnginePackLoader().LoadAsync(Active));}catch(InvalidDataException){rejected=true;}Check(rejected,"Loader accepted "+name);Close();
    }
    [STAThread] static int Main(string[] args){
        if(args.Length==8&&args[0]=="--child"&&OperatingSystem.IsWindows())return Child(args);
        if(args.Length!=2||!OperatingSystem.IsWindows()){Console.Error.WriteLine("Actual Windows required: repo derivedPacks");return 2;}
        Repo=Path.GetFullPath(args[0]);Packs=Path.GetFullPath(args[1]);Output=Path.Combine(Repo,"artifacts/t10");Active=Path.Combine(Output,"active-pack");Subject=Path.Combine(Output,"subject");Directory.CreateDirectory(Output);
        if(Directory.Exists(Subject))throw new InvalidOperationException("Fresh T10 output required");Copy(Path.Combine(Repo,"tests/fixtures"),Subject);
        var subjectBefore=Snapshot(Subject);var packsBefore=Snapshot(Packs);string assembly=typeof(MainWindow).Assembly.Location, assemblyHash=Hash(assembly);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try{
            var fullTargets = Await(new EnginePackLoader().LoadAsync(Path.Combine(Packs, "full")))
                .Targets.Select(t => t.Version).ToArray();
            string expandedTarget = fullTargets[^1];
            Case("reduced pack restart displays only verified 8.2/8.3",()=>{Activate("reduced");ChildProbe("reduced","8.3","8.2","8.3");Targets("8.2","8.3");GuiScan("8.3");});
            Case("reduced pack actual fixture/target regressions",()=>Regression(false));
            Case("closed-window full pack exchange adds verified " + expandedTarget,()=>{Activate("full");ChildProbe("full",expandedTarget,fullTargets);Targets(fullTargets);GuiScan(expandedTarget);});
            Case("full pack actual fixture/target regressions",()=>Regression(false));
            Case("unverified target refused",()=>Bad("unverified target",m=>m["targets"]![0]!["version"]="99.0"));
            Case("runtime/engine file hash corruption refused",()=>Bad("hash corruption",_=>{},corrupt:true));
            Case("unsupported manifest schema refused",()=>Bad("schema",m=>m["schemaVersion"]=999));
            Case("unsupported report profile refused",()=>Bad("profile",m=>m["reportProfile"]="unsupported"));
            Case("mixed subset/full partial composition refused",()=>Bad("partial mix",_=>{},mix:true));
            Case("closed-window previous pack restore reproduces targets and GUI",()=>{Activate("reduced");ChildProbe("restored","8.3","8.2","8.3");Targets("8.2","8.3");GuiScan("8.3");});
            Case("restored pack actual regressions match prior results",()=>Regression(true));
            Case("same application assembly and source/derived-pack bytes unchanged",()=>{Check(assemblyHash==Hash(assembly),"Product app assembly changed");Check(subjectBefore.SequenceEqual(Snapshot(Subject)),"Subject source changed");Check(packsBefore.SequenceEqual(Snapshot(Packs)),"Derived input packs changed");});
        }finally{foreach(var w in Windows)if(w.IsVisible){w.Close();Until(()=>!w.IsVisible);}app.Shutdown();}
        var report=new{environment=new{os=Environment.OSVersion.VersionString,runId=Environment.GetEnvironmentVariable("GITHUB_RUN_ID"),sourceSha=Environment.GetEnvironmentVariable("GITHUB_SHA")},passed=Passed,failures=Failures,runs=Runs,applicationAssemblySha256=assemblyHash,scopePassed=Failures.Count==0,scope="Development-only derived verified targets, actual WPF restart/manual directory exchange and restore, same assembly; not license or release approval",limitations=new[]{"T08 redistribution gate remains open","Independent verification OS processes load the unchanged product assembly; default packaged product EXE startup belongs to deployment validation","No future/unverified PHP version introduced","Windows Server 2022, not clean Windows 11","Firewall rule installed by workflow; connectivity positive-control is from base pack preparation"}};
        File.WriteAllText(Path.Combine(Output,"summary.json"),JsonSerializer.Serialize(report,Pretty),new UTF8Encoding(false));Console.WriteLine("T10_SUMMARY_JSON="+JsonSerializer.Serialize(report));return Failures.Count==0?0:1;
    }
}
