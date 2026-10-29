using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.IO.Compression;

public class PatchFile {
    public string path,original,patched,raw,delta,deltaHash,group;
    public long originalSize,patchedSize,rawSize;
    public bool unreal;
}
public class PatchManifest { public string version,toolHash; public List<PatchFile> files; }
public class PatchState { public string version,status; public List<PatchFile> files; }
public sealed class PatchEngine : IDisposable {
    public const string StateName="Dishonored-AZ-Patch";
    public readonly string Payload;
    public readonly PatchManifest Manifest;
    public Action<string,int> Progress=delegate {};
    public int FailAfter=-1; // Fault-injection test hook; disabled in normal use.
    static readonly JavaScriptSerializer Json=new JavaScriptSerializer { MaxJsonLength=16*1024*1024 };
    public static string Hash(string p) { using(var f=File.OpenRead(p)) using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant(); }
    public static string HashBytes(byte[] b) { using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant(); }
    public static T Read<T>(string p) { return Json.Deserialize<T>(File.ReadAllText(p,Encoding.UTF8)); }
    public static void Write(string p,object obj) {
        string tmp=p+".new";
        using(var f=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)) {
            byte[] b=Encoding.UTF8.GetBytes(Json.Serialize(obj)); f.Write(b,0,b.Length); f.Flush(true);
        }
        if(File.Exists(p)) File.Replace(tmp,p,null); else File.Move(tmp,p);
    }
    static void Require(bool b,string message) { if(!b) throw new InvalidOperationException(message); }
    public static string Safe(string root,string rel) {
        Require(!String.IsNullOrWhiteSpace(rel) && !Path.IsPathRooted(rel),"Unsafe patch path.");
        string normalized=rel.Replace('/',Path.DirectorySeparatorChar);
        Require(!normalized.Split(Path.DirectorySeparatorChar).Any(s=>s==".." || s=="." || s.Contains(":")),"Unsafe patch path.");
        string p=Path.GetFullPath(Path.Combine(root,normalized)),r=Path.GetFullPath(root).TrimEnd('\\')+"\\";
        Require(p.StartsWith(r,StringComparison.OrdinalIgnoreCase),"Patch path escapes game folder.");
        for(string a=p;a!=null && a.Length>=r.Length-1;a=Path.GetDirectoryName(a))
            if(File.Exists(a)||Directory.Exists(a)) Require((File.GetAttributes(a)&FileAttributes.ReparsePoint)==0,"Linked folders/files are unsupported: "+a);
        return p;
    }
    public PatchEngine() {
        Payload=Path.Combine(Path.GetTempPath(),"DishonoredAZ-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Payload);
        try {
            using(var s=typeof(PatchEngine).Assembly.GetManifestResourceStream("payload.zip")) {
                Require(s!=null,"Installer payload is missing.");
                using(var z=new ZipArchive(s,ZipArchiveMode.Read)) foreach(var e in z.Entries) {
                    string p=Safe(Payload,e.FullName); Require(Path.GetDirectoryName(p)==Payload,"Invalid payload entry.");
                    e.ExtractToFile(p);
                }
            }
            Manifest=Read<PatchManifest>(Path.Combine(Payload,"manifest.json"));
            Require(Manifest.version=="0.1.0-beta.1" && Manifest.files.Count==746,"Unexpected patch manifest.");
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var f in Manifest.files) {
                Require(seen.Add(f.path),"Duplicate patch path."); Safe(Payload,f.delta);
                Require(f.path.StartsWith("DishonoredGame/",StringComparison.Ordinal) && (f.path.EndsWith(".int",StringComparison.OrdinalIgnoreCase)||f.path.EndsWith(".upk",StringComparison.OrdinalIgnoreCase)),"Unsupported patch target.");
                Require(Hash(Safe(Payload,f.delta))==f.deltaHash,"Damaged installer payload: "+f.delta);
            }
            Require(Hash(Path.Combine(Payload,"xdelta3.exe"))==Manifest.toolHash,"Damaged patch tool.");
        } catch { Dispose(); throw; }
    }
    static string Root(string root) {
        Require(!String.IsNullOrWhiteSpace(root),"Select your Dishonored game folder."); root=Path.GetFullPath(root).TrimEnd('\\');
        Require(File.Exists(Safe(root,"Binaries/Win32/Dishonored.exe")),"Select the game root containing Binaries\\Win32\\Dishonored.exe.");
        foreach(var p in Process.GetProcessesByName("Dishonored")) { p.Dispose(); throw new InvalidOperationException("Close Dishonored before installing or restoring the patch."); }
        return root;
    }
    List<PatchFile> Select(string root) {
        var selected=new List<PatchFile>();
        foreach(var g in Manifest.files.GroupBy(f=>f.group)) {
            int found=g.Count(f=>File.Exists(Safe(root,f.path)));
            if(g.Key!="Base" && found==0) continue;
            Require(found==g.Count(),"Missing files for "+g.Key+". This game edition is incomplete or incompatible. No game files were changed.");
            selected.AddRange(g);
        }
        return selected;
    }
    static string StateDir(string root) { return Safe(root,StateName); }
    void VerifyState(PatchState state) {
        Require(state.version==Manifest.version && state.files!=null,"Backup belongs to a different patch version.");
        var known=Manifest.files.ToDictionary(f=>f.path,StringComparer.OrdinalIgnoreCase);
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var f in state.files) {
            PatchFile k;
            Require(seen.Add(f.path) && known.TryGetValue(f.path,out k),"Invalid backup manifest.");
            k=known[f.path]; Require(k.original==f.original && k.patched==f.patched,"Invalid backup hashes.");
        }
        Require(state.files.Count>0,"Empty backup manifest.");
    }
    void Decode(string source,PatchFile f,string output,string scratch) {
        string raw=Path.Combine(scratch,"source.raw");
        try {
            if(f.unreal) File.WriteAllBytes(raw,UnrealPackage.Decode(source)); else File.Copy(source,raw,true);
            Require(Hash(raw)==f.raw,"Decompressed source verification failed: "+f.path);
            var start=new ProcessStartInfo(Path.Combine(Payload,"xdelta3.exe"),"-d -f -D -R -s "+Quote(raw)+" "+Quote(Safe(Payload,f.delta))+" "+Quote(output)) {
                UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true
            };
            start.EnvironmentVariables.Remove("XDELTA");
            using(var p=Process.Start(start)) {
                string errors=p.StandardError.ReadToEnd(); p.WaitForExit();
                Require(p.ExitCode==0,"Cannot apply patch for "+f.path+": "+errors);
            }
            Require(Hash(output)==f.patched,"Patched file verification failed: "+f.path);
        } finally { if(File.Exists(raw)) File.Delete(raw); }
    }
    public static string Quote(string s) { Require(!s.Contains("\""),"Invalid path."); return "\""+s+"\""; }
    static void DeleteDirectory(string p) {
        if(!Directory.Exists(p)) return;
        Require((File.GetAttributes(p)&FileAttributes.ReparsePoint)==0,"Refusing to remove a linked directory.");
        foreach(var f in Directory.GetFiles(p)) { Require((File.GetAttributes(f)&FileAttributes.ReparsePoint)==0,"Linked backup file."); File.Delete(f); }
        foreach(var d in Directory.GetDirectories(p)) DeleteDirectory(d);
        Directory.Delete(p);
    }
    public string Install(string input) {
        string root=Root(input),dir=StateDir(root),statePath=Safe(dir,"state.json");
        CleanRestored(root);
        var files=Select(root);
        bool managed=File.Exists(statePath);
        if(!managed && files.All(f=>Hash(Safe(root,f.path))==f.patched))
            return "This Azerbaijani build is already installed. No files were changed. Use its existing restore tool to uninstall.";
        if(managed) {
            var prior=Read<PatchState>(statePath); VerifyState(prior);
            if(prior.status=="installed") {
                Require(prior.files.All(f=>Hash(Safe(root,f.path))==f.patched),"Installed files changed after patching. Restore or repair before reinstalling.");
                Require(prior.files.All(f=>File.Exists(Safe(dir,"backup/"+f.path)) && Hash(Safe(dir,"backup/"+f.path))==f.original),"A backup is missing or damaged. Keep the backup folder and repair it before making changes.");
                Require(prior.files.Count==files.Count,"DLC selection changed. Restore this patch before adding DLC.");
                return "This Azerbaijani build is already installed and verified.";
            }
            Restore(root); // Recover a previous interrupted transaction before beginning anew.
            CleanRestored(root);
        }
        Require(!Directory.Exists(dir),"An unrecognized patch backup folder exists. Keep it safe and resolve it before installing.");
        for(int i=0;i<files.Count;i++) {
            var f=files[i]; Progress("Checking compatibility: "+f.path,i*20/files.Count);
            Require(Hash(Safe(root,f.path))==f.original,"Unsupported or modified game file: "+f.path+". Restore the original English files first. No game files were changed.");
            Require((File.GetAttributes(Safe(root,f.path))&FileAttributes.ReadOnly)==0,"Read-only game file: "+f.path);
        }
        long need=files.Sum(f=>f.patchedSize)+files.Max(f=>f.rawSize)+128L*1024*1024;
        var drive=new DriveInfo(Path.GetPathRoot(root));
        Require(drive.AvailableFreeSpace>need,"Not enough free space. Free at least "+Math.Ceiling(need/1073741824.0)+" GB on the game drive.");
        Directory.CreateDirectory(dir);
        using(var locked=new FileStream(Safe(dir,"transaction.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
            var state=new PatchState { version=Manifest.version,status="preparing",files=files }; Write(statePath,state);
            try {
                for(int i=0;i<files.Count;i++) {
                    var f=files[i]; Progress("Preparing: "+f.path,20+i*55/files.Count);
                    string dst=Safe(dir,"staged/"+f.path); Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    Decode(Safe(root,f.path),f,dst,dir);
                }
                // Detect edits made while staging; no original has been replaced yet.
                foreach(var f in files) Require(Hash(Safe(root,f.path))==f.original,"A game file changed during preparation. Close other tools and retry.");
                state.status="committing"; Write(statePath,state);
                for(int i=0;i<files.Count;i++) {
                    if(FailAfter==i) throw new IOException("Simulated write failure for rollback testing.");
                    var f=files[i]; Progress("Installing: "+f.path,75+i*20/files.Count);
                    string dst=Safe(root,f.path),backup=Safe(dir,"backup/"+f.path);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    File.Replace(Safe(dir,"staged/"+f.path),dst,backup);
                    Require(Hash(dst)==f.patched && Hash(backup)==f.original,"Installed file or backup verification failed: "+f.path);
                }
                string restore=Safe(dir,"Restore-English.exe"); File.Copy(typeof(PatchEngine).Assembly.Location,restore,true);
                File.WriteAllText(Safe(dir,"README.txt"),"Close Dishonored and run Restore-English.exe, choose this game folder, then click Restore English.\r\nKeep this folder: it contains original game files for rollback.\r\nTest release 0.1.0-beta.1; artwork unchanged. Full linguistic and DLC QA pending.\r\n",Encoding.UTF8);
                state.status="installed"; Write(statePath,state);
                DeleteDirectory(Safe(dir,"staged")); Progress("Installation verified. Launch Dishonored normally.",100);
                return "Azerbaijani patch installed and verified ("+files.Count+" files). Launch Dishonored normally. Keep the Dishonored-AZ-Patch folder for restoring English.";
            } catch(Exception initial) {
                try { Rollback(root,dir,state); }
                catch(Exception recovery) { throw new IOException("Installation stopped. Automatic rollback needs attention. Keep "+dir+" and run Restore English. Error: "+recovery.Message,initial); }
                throw new IOException("Installation stopped; original files were restored. "+initial.Message,initial);
            }
        }
    }
    void Rollback(string root,string dir,PatchState state) {
        VerifyState(state);
        foreach(var f in state.files) {
            string backup=Safe(dir,"backup/"+f.path),dst=Safe(root,f.path);
            if(File.Exists(backup)) {
                Require(Hash(backup)==f.original,"Damaged original backup: "+f.path);
                if(File.Exists(dst)) Require(Hash(dst)==f.patched || Hash(dst)==f.original,"Game file was changed by another program: "+f.path);
            } else Require(File.Exists(dst) && Hash(dst)==f.original,"Original file is missing: "+f.path);
        }
        state.status="restoring"; Write(Safe(dir,"state.json"),state);
        for(int i=0;i<state.files.Count;i++) {
            var f=state.files[i]; string backup=Safe(dir,"backup/"+f.path),dst=Safe(root,f.path);
            if(File.Exists(backup)) {
                string tmp=dst+".az-restore-"+Guid.NewGuid().ToString("N");
                try {
                    File.Copy(backup,tmp); Require(Hash(tmp)==f.original,"Restore verification failed.");
                    if(File.Exists(dst)) File.Replace(tmp,dst,null); else File.Move(tmp,dst);
                } finally { if(File.Exists(tmp)) File.Delete(tmp); }
                Require(Hash(dst)==f.original,"Restore verification failed: "+f.path);
            }
            Progress("Restoring English: "+f.path,i*100/state.files.Count);
        }
        state.status="restored"; Write(Safe(dir,"state.json"),state);
    }
    public string Restore(string input) {
        string root=Root(input),dir=StateDir(root),sp=Safe(dir,"state.json");
        Require(File.Exists(sp),"No backup from this installer was found. Use the restore tool supplied with your previous patch.");
        using(var locked=new FileStream(Safe(dir,"transaction.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
            var state=Read<PatchState>(sp); VerifyState(state); Rollback(root,dir,state);
        }
        // Running Restore-English.exe cannot delete itself. Leave only the harmless launcher;
        // a restored state is kept until a subsequent installer run cleans it up.
        DeleteDirectory(Safe(dir,"backup")); DeleteDirectory(Safe(dir,"staged"));
        if(File.Exists(Safe(dir,"source.raw"))) File.Delete(Safe(dir,"source.raw"));
        Progress("Original English files restored and verified.",100);
        return "Original English files restored and verified. Your saves were not changed.";
    }
    public void CleanRestored(string input) {
        string root=Root(input),dir=StateDir(root),sp=Safe(dir,"state.json");
        if(!File.Exists(sp)) return;
        var state=Read<PatchState>(sp); VerifyState(state);
        if(state.status=="restored") {
            foreach(var group in state.files.GroupBy(f=>f.group)) {
                if(group.Key!="Base" && group.All(f=>!File.Exists(Safe(root,f.path)))) continue;
                Require(group.All(f=>File.Exists(Safe(root,f.path)) && Hash(Safe(root,f.path))==f.original),"Game files changed since restoration.");
            }
            Require(!typeof(PatchEngine).Assembly.Location.StartsWith(dir+"\\",StringComparison.OrdinalIgnoreCase),"Run the downloaded installer outside the game backup folder to reinstall.");
            DeleteDirectory(dir);
        }
    }
    public void Dispose() { if(Payload!=null) DeleteDirectory(Payload); }
}
