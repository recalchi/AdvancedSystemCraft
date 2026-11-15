using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualBasic.FileIO;

internal sealed class Finding {
    public string Path, Category, Target;
    public long Size;
    public DateTime Modified;
}
internal sealed class Report {
    public readonly List<Finding> Items = new List<Finding>();
    public int Examined, Skipped;
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
    public static void Correct(Finding item, string temp, string desktop) {
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
