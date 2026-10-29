using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Principal;
using System.Diagnostics;

static class Program {
    [STAThread] static int Main(string[] args) {
        if(args.Contains("--headless")) {
            string report=Arg(args,"--report");
            try {
                using(var engine=new PatchEngine()) {
                    int fault; if(Int32.TryParse(Arg(args,"--fail-after"),out fault)) engine.FailAfter=fault;
                    string root=Arg(args,"--root"),result;
                    using(var gate=LockRoot(root)) {
                        result=args.Contains("--restore")?engine.Restore(root):engine.Install(root);
                    }
                    if(report!=null) PatchEngine.Write(report,new { success=true,message=result });
                }
                return 0;
            } catch(Exception e) { if(report!=null) PatchEngine.Write(report,new { success=false,message=e.Message }); return 1; }
        }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm(Arg(args,"--root"))); return 0;
    }
    public static string Arg(string[] a,string key) { int i=Array.IndexOf(a,key); return i>=0 && i<a.Length-1?a[i+1]:null; }
    public static IDisposable LockRoot(string root) {
        string key=PatchEngine.HashBytes(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(root).TrimEnd('\\').ToUpperInvariant()));
        var m=new Mutex(false,"Local\\DishonoredAZ-"+key);
        try { if(!m.WaitOne(0)) throw new InvalidOperationException("Another installer is working on this game folder."); }
        catch(AbandonedMutexException) { }
        catch { m.Dispose(); throw; }
        return new MutexScope(m);
    }
    class MutexScope:IDisposable { Mutex m; public MutexScope(Mutex m) { this.m=m; } public void Dispose() { m.ReleaseMutex(); m.Dispose(); } }
}
class InstallerForm:Form {
    TextBox path=new TextBox(),details=new TextBox();
    Button browse=new Button(),install=new Button(),restore=new Button(),admin=new Button();
    ProgressBar progress=new ProgressBar(); Label status=new Label(); bool busy;
    public InstallerForm(string initial) {
        Text="Dishonored — Azərbaycan dili yaması"; ClientSize=new Size(730,495); MinimumSize=new Size(746,534);
        Font=new Font("Segoe UI",10); StartPosition=FormStartPosition.CenterScreen; AutoScaleMode=AutoScaleMode.Dpi;
        var title=new Label { Text="Dishonored · Azərbaycan dili",Location=new Point(24,21),Size=new Size(680,38),Font=new Font("Segoe UI",20,FontStyle.Bold) };
        var intro=new Label { Text="Azerbaijani language patch 0.1.0-beta.1\nText, dialogue and fonts · Base game + installed DLC · Artwork unchanged",Location=new Point(26,70),Size=new Size(675,49) };
        var label=new Label { Text="Game folder / Oyun qovluğu",Location=new Point(26,132),AutoSize=true };
        path.SetBounds(26,158,558,28); path.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;
        browse.Text="Browse…"; browse.SetBounds(598,155,105,33); browse.Anchor=AnchorStyles.Right|AnchorStyles.Top;
        browse.Click+=delegate { using(var d=new FolderBrowserDialog { Description="Select the folder containing Binaries and DishonoredGame",SelectedPath=path.Text,ShowNewFolderButton=false }) if(d.ShowDialog(this)==DialogResult.OK) path.Text=d.SelectedPath; };
        var note=new Label { Text="Exact file checks protect your game. Unsupported files are rejected before installation.\nOriginal files are backed up locally. Saves are untouched. Language and DLC QA are ongoing.",Location=new Point(26,201),Size=new Size(677,49) };
        install.Text="Install / Quraşdır"; install.SetBounds(26,263,185,39);
        restore.Text="Restore English"; restore.SetBounds(223,263,172,39);
        admin.Text="Run as administrator"; admin.SetBounds(460,263,243,39); admin.Anchor=AnchorStyles.Top|AnchorStyles.Right;
        admin.Click+=delegate { try { Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--root "+PatchEngine.Quote(path.Text)) { UseShellExecute=true,Verb="runas" }); Close(); } catch(Exception e) { status.Text=e.Message; } };
        if(new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) admin.Visible=false;
        progress.SetBounds(26,318,677,20); progress.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;
        status.Text="Choose your game folder to begin."; status.SetBounds(26,346,677,29); status.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;
        details.SetBounds(26,379,677,90); details.Multiline=true; details.ReadOnly=true; details.ScrollBars=ScrollBars.Vertical; details.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom|AnchorStyles.Top;
        details.Text="Keep the Dishonored-AZ-Patch folder after installing. It contains your original files and Restore-English.exe.\r\nThis installer works offline and does not modify saves or the game executable.";
        Controls.AddRange(new Control[] {title,intro,label,path,browse,note,install,restore,admin,progress,status,details});
        string parent=Path.GetDirectoryName(Application.ExecutablePath);
        path.Text=initial??(Path.GetFileName(parent)==PatchEngine.StateName?Path.GetDirectoryName(parent):Detect());
        install.Click+=async delegate { await Run(false); }; restore.Click+=async delegate { await Run(true); };
        FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(busy) { e.Cancel=true; status.Text="Please wait for verification or rollback to finish."; } };
    }
    static string Detect() {
        foreach(var prefix in new[] {Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"C:\Games",@"D:\SteamLibrary"})
            foreach(var suffix in new[] {@"Steam\steamapps\common\Dishonored",@"steamapps\common\Dishonored","Dishonored"}) {
                string p=Path.Combine(prefix,suffix); if(File.Exists(Path.Combine(p,@"Binaries\Win32\Dishonored.exe"))) return p;
            }
        return "";
    }
    async Task Run(bool undo) {
        if(busy) return;
        if(undo && MessageBox.Show(this,"Restore the original English files from this installer's backup?","Restore English",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
        busy=true; foreach(var c in new Control[]{path,browse,install,restore,admin}) c.Enabled=false; progress.Value=0;
        string root=path.Text;
        try {
            string result=await Task.Run(()=> {
                using(var gate=Program.LockRoot(root)) using(var engine=new PatchEngine()) {
                    engine.Progress=(message,percent)=> BeginInvoke((Action)(()=> {status.Text=message; progress.Value=Math.Max(0,Math.Min(100,percent));}));
                    return undo?engine.Restore(root):engine.Install(root);
                }
            });
            progress.Value=100; status.Text=undo?"English restored.":"Done."; details.Text=result;
            MessageBox.Show(this,result,"Dishonored Azerbaijani Patch",MessageBoxButtons.OK,MessageBoxIcon.Information);
        } catch(Exception e) { status.Text="Operation stopped safely. See details below."; details.Text=e.Message; MessageBox.Show(this,e.Message,"Patch could not continue",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        finally { busy=false; foreach(var c in new Control[]{path,browse,install,restore,admin}) c.Enabled=true; }
    }
}
