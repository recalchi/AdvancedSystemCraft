using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualBasic.FileIO;

internal sealed class Finding {
    public string Path, Category, Target;
    public string BrowserRoot; internal string BrowserProcess;
    public long Size;
    public DateTime Modified;
}
internal sealed class Report {
    public readonly List<Finding> Items = new List<Finding>();
    public int Examined, Skipped;
    public readonly List<string> Notices = new List<string>();
}
internal static class Engine {
    public static readonly string Temp = System.IO.Path.GetTempPath();
    public static readonly string Desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    static bool IsLink(FileSystemInfo file) { return (file.Attributes & FileAttributes.ReparsePoint) != 0; }
    static bool LocalPath(string path) {
        return !String.IsNullOrEmpty(path) && path.Length >= 3 && Char.IsLetter(path[0]) &&
            path[1] == ':' && path[2] == '\\' && new DriveInfo(path.Substring(0,3)).DriveType == DriveType.Fixed;
    }
    static bool Missing(string path) {
        if (!LocalPath(path)) return false;
        try { File.GetAttributes(path); return false; }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return true; }
    }
    static string ShortcutTarget(string path) {
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        try {
            dynamic link = shell.CreateShortcut(path);
            try { return (string)link.TargetPath; }
            finally { Marshal.FinalReleaseComObject(link); }
        } finally { Marshal.FinalReleaseComObject(shell); }
    }
    static Finding Inspect(FileInfo file, bool shortcut) {
        if (IsLink(file)) return null;
        string target = null;
        if (shortcut) {
            if (!file.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)) return null;
            target = ShortcutTarget(file.FullName);
            if (!Missing(target)) return null;
        } else if (!file.Extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase) ||
                   file.LastWriteTimeUtc >= DateTime.UtcNow.AddDays(-7)) return null;
        return new Finding { Path=file.FullName, Category=shortcut ? "Atalho inválido" : "Temporário antigo",
            Size=file.Length, Modified=file.LastWriteTimeUtc, Target=target };
    }
    static bool Expected(Exception ex) {
        return ex is IOException || ex is UnauthorizedAccessException || ex is COMException ||
               ex is System.Security.SecurityException || ex is ArgumentException || ex is NotSupportedException;
    }
    public static Report Scan(string temp, string desktop, bool files, bool links, CancellationToken token) {
        var report = new Report();
        Action<string,bool> scan = (root, shortcut) => {
            try {
                var dir = new DirectoryInfo(root);
                if (!LocalPath(dir.FullName) || IsLink(dir)) { report.Skipped++; return; }
                foreach (var file in dir.EnumerateFiles(shortcut ? "*.lnk" : "*.tmp")) {
                    token.ThrowIfCancellationRequested();
                    report.Examined++;
                    try { var item = Inspect(file,shortcut); if (item != null) report.Items.Add(item); }
                    catch (Exception ex) { if (!Expected(ex)) throw; report.Skipped++; }
                }
            } catch (Exception ex) { if (!Expected(ex)) throw; report.Skipped++; }
        };
        token.ThrowIfCancellationRequested();
        if (files) scan(temp,false);
        if (links) scan(desktop,true);
        return report;
    }
    static bool BrowserOpen(string name) {
        var processes=Process.GetProcessesByName(name);
        try { return processes.Length!=0; }
        finally { foreach(var process in processes)process.Dispose(); }
    }
    // Chromium simple-cache entry streams. Index and browser profile databases are excluded.
    static bool CacheEntry(string name) {
        return Regex.IsMatch(name,@"\A[0-9a-fA-F]{16}_[01]\z");
    }
    static bool CacheLocation(FileInfo file, string root) {
        var stream=file.Directory;
        if(stream==null || (stream.Name!="js" && stream.Name!="wasm"))return false;
        var cache=stream.Parent;
        if(cache==null || cache.Name!="Code Cache" || cache.Parent==null)return false;
        var profile=cache.Parent;
        if(profile.Name!="Default" && !Regex.IsMatch(profile.Name,@"\AProfile [0-9]+\z"))return false;
        return profile.Parent!=null && String.Equals(profile.Parent.FullName,root,StringComparison.OrdinalIgnoreCase) &&
            !IsLink(profile.Parent) && !IsLink(profile) && !IsLink(cache) && !IsLink(stream) && !IsLink(file);
    }
    internal static void ScanCache(Report report,string root,string browser,CancellationToken token) {
        if(BrowserOpen(browser)){report.Notices.Add(browser+" aberto — cache ignorado");return;}
        try {
            var directory=new DirectoryInfo(root);
            if(!directory.Exists)return;
            if(IsLink(directory) || !LocalPath(root)){report.Skipped++;return;}
            foreach(var profile in directory.EnumerateDirectories()){
                token.ThrowIfCancellationRequested();
                if((profile.Name!="Default" && !Regex.IsMatch(profile.Name,@"\AProfile [0-9]+\z")) || IsLink(profile))continue;
                var cache=new DirectoryInfo(Path.Combine(profile.FullName,"Code Cache"));
                if(!cache.Exists || IsLink(cache))continue;
                foreach(var name in new[]{"js","wasm"}){
                    var stream=new DirectoryInfo(Path.Combine(cache.FullName,name));
                    if(!stream.Exists || IsLink(stream))continue;
                    try {
                        foreach(var file in stream.EnumerateFiles()){
                            token.ThrowIfCancellationRequested();
                            if(!CacheEntry(file.Name))continue;
                            report.Examined++;
                            try {
                                if(!IsLink(file))report.Items.Add(new Finding{Path=file.FullName,Category="Cache de código · "+browser,
                                    Size=file.Length,Modified=file.LastWriteTimeUtc,BrowserRoot=root,BrowserProcess=browser});
                            }catch(Exception ex){if(!Expected(ex))throw;report.Skipped++;}
                        }
                    }catch(Exception ex){if(!Expected(ex))throw;report.Skipped++;}
                }
            }
        }catch(Exception ex){if(!Expected(ex))throw;report.Skipped++;}
    }
    internal static void ScanBrowserCaches(Report report,CancellationToken token){
        var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        ScanCache(report,Path.Combine(local,@"Google\Chrome\User Data"),"chrome",token);
        ScanCache(report,Path.Combine(local,@"Microsoft\Edge\User Data"),"msedge",token);
    }
    public static void Correct(Finding item, string temp, string desktop) {
        if(item.BrowserRoot!=null){
            var cache=new FileInfo(item.Path);
            if(BrowserOpen(item.BrowserProcess) || !CacheEntry(cache.Name) || !CacheLocation(cache,item.BrowserRoot) ||
                cache.Length!=item.Size || cache.LastWriteTimeUtc!=item.Modified)
                throw new IOException("O navegador está aberto ou a entrada do cache mudou. Verifique novamente.");
            FileSystem.DeleteFile(cache.FullName, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
            return;
        }
        bool shortcut = item.Target != null;
        var dir = new DirectoryInfo(shortcut ? desktop : temp);
        var file = new FileInfo(item.Path);
        if (!LocalPath(file.FullName) || IsLink(dir) ||
            !String.Equals(dir.FullName.TrimEnd('\\'),file.DirectoryName.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))
            throw new IOException("Local alterado desde a verificação.");
        var current = Inspect(file,shortcut);
        if (current == null || current.Size != item.Size || current.Modified != item.Modified || current.Target != item.Target)
            throw new IOException("O item mudou. Faça uma nova verificação.");
        FileSystem.DeleteFile(file.FullName, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
    }
}
