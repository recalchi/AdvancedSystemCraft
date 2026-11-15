using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class AppPathReference {
    internal string Application, RawValue;
    internal bool IsBackup;
}

internal sealed class AppPathReport {
    internal readonly List<AppPathReference> Entries = new List<AppPathReference>();
    internal int Examined, Unverified;
}

internal sealed class RegistryCare {
    internal const string AppPaths = @"Software\Microsoft\Windows\CurrentVersion\App Paths";
    internal const string Backups = @"Software\AdvancedSystemCraft\RegistryCare\AppPaths";
    readonly RegistryKey root;
    internal RegistryCare(RegistryKey root) { this.root = root; }

    static bool ValidName(string name) {
        return name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) &&
               name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    // null = not safely verifiable, true = absent, false = present.
    static bool? Missing(string raw) {
        if (String.IsNullOrEmpty(raw)) return null;
        var path = raw;
        if (path.Length > 2 && path[0] == '"' && path[path.Length-1] == '"')
            path = path.Substring(1,path.Length-2);
        if (path.Length < 7 || !Char.IsLetter(path[0]) || path[1] != ':' || path[2] != '\\' ||
            !path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) || path.IndexOf(':',2) >= 0 ||
            path.IndexOfAny(new[]{'%','"','*','?'}) >= 0 || Path.GetFullPath(path) != path) return null;
        var drive = new DriveInfo(path.Substring(0,3));
        if (drive.DriveType != DriveType.Fixed || !drive.IsReady) return null;
        // Check ancestors first so an unresolved junction cannot masquerade as a missing executable.
        string current = drive.Name;
        foreach (string part in path.Substring(3).Split('\\')) {
            current = Path.Combine(current,part);
            try {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return null;
            } catch (FileNotFoundException) { return true; }
              catch (DirectoryNotFoundException) { return true; }
        }
        return false;
    }

    static bool Expected(Exception ex) {
        return ex is UnauthorizedAccessException || ex is IOException || ex is SecurityException ||
               ex is ArgumentException || ex is NotSupportedException;
    }

    static string ReadLiteral(RegistryKey key, string name) {
        if (key == null) return null;
        var value = key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        return value != null && key.GetValueKind(name) == RegistryValueKind.String ? value : null;
    }

    internal AppPathReport Inspect() {
        var report = new AppPathReport();
        using (var paths = root.OpenSubKey(AppPaths)) {
            if (paths != null) foreach (string name in paths.GetSubKeyNames()) {
                if (!ValidName(name)) continue;
                report.Examined++;
                try {
                    using (var key = paths.OpenSubKey(name)) {
                        var value = ReadLiteral(key,"");
                        if (value == null && key != null && !key.GetValueNames().Contains("")) continue;
                        var missing = Missing(value);
                        if (missing == true)
                            report.Entries.Add(new AppPathReference {Application=name, RawValue=value});
                        else if (!missing.HasValue) report.Unverified++;
                    }
                } catch (Exception ex) { if (!Expected(ex)) throw; report.Unverified++; }
            }
        }
        using (var backup = root.OpenSubKey(Backups)) {
            if (backup != null) foreach (var name in backup.GetValueNames()) {
                string raw = ReadLiteral(backup,name);
                if (ValidName(name) && raw != null)
                    report.Entries.Add(new AppPathReference {Application=name, RawValue=raw, IsBackup=true});
                else report.Unverified++;
            }
        }
        return report;
    }

    internal void Apply(AppPathReference entry) {
        if (entry == null || !ValidName(entry.Application) || entry.RawValue == null)
            throw new ArgumentException("Referência inválida.");
        using (var gate = new Mutex(false,@"Local\AdvancedSystemCraft.RegistryCare")) {
            bool locked;
            try { locked = gate.WaitOne(0); } catch (AbandonedMutexException) { locked = true; }
            if (!locked) throw new IOException("Outra operação de registro está em andamento.");
            try { Transfer(entry); } finally { gate.ReleaseMutex(); }
        }
    }

    void Transfer(AppPathReference entry) {
        using (var key = root.OpenSubKey(AppPaths + "\\" + entry.Application,true)) {
            if (key == null) throw new IOException("A chave de registro mudou. Atualize a lista.");
            if (entry.IsBackup) {
                using (var backup = root.OpenSubKey(Backups,true)) {
                    if (ReadLiteral(backup,entry.Application) != entry.RawValue)
                        throw new IOException("A cópia mudou. Atualize a lista.");
                    if (key.GetValueNames().Contains("") && ReadLiteral(key,"") != entry.RawValue)
                        throw new IOException("Há outro valor registrado. A cópia foi preservada.");
                    key.SetValue("",entry.RawValue,RegistryValueKind.String);
                    key.Flush();
                    backup.DeleteValue(entry.Application);
                }
            } else {
                if (ReadLiteral(key,"") != entry.RawValue || Missing(entry.RawValue) != true)
                    throw new IOException("A referência mudou ou o destino já não está comprovadamente ausente.");
                using (var backup = root.CreateSubKey(Backups)) {
                    if (backup.GetValueNames().Contains(entry.Application,StringComparer.OrdinalIgnoreCase) &&
                        ReadLiteral(backup,entry.Application) != entry.RawValue)
                        throw new IOException("Uma cópia diferente já existe para este aplicativo.");
                    backup.SetValue(entry.Application,entry.RawValue,RegistryValueKind.String);
                    backup.Flush();
                    if (ReadLiteral(key,"") != entry.RawValue || Missing(entry.RawValue) != true)
                        throw new IOException("A origem mudou. A cópia foi preservada.");
                    key.DeleteValue("");
                }
            }
        }
    }
}

internal sealed partial class CraftWindow {
    void ShowRegistryCare() {
        var page = Page("Revisão do registro do usuário");
        page.Controls.Add(new Label {AutoSize=true,MaximumSize=new Size(565,0),
            Text="Revise referências App Paths de executáveis locais ausentes. A correção guarda uma cópia do valor padrão para restauração."});
        var rows = new ListView {Name="registry-results", View=View.Details, MultiSelect=false,
            HideSelection=false,FullRowSelect=true,Width=560,Height=225,BackColor=surface,ForeColor=Color.White};
        rows.Columns.Add("Aplicativo",145); rows.Columns.Add("Estado",130); rows.Columns.Add("Valor registrado",280);
        page.Controls.Add(rows);
        var bar = new FlowLayoutPanel {AutoSize=true};
        var status = new Label {AutoSize=true,MaximumSize=new Size(565,0)};
        page.Controls.Add(bar);page.Controls.Add(status);
        var store = new RegistryCare(Registry.CurrentUser);
        var refresh = new Button {Width=125,Height=38};
        var apply = new Button {Width=200,Height=38,Enabled=false};
        Style(refresh,"Verificar");Style(apply,"Corrigir referência");bar.Controls.Add(refresh);bar.Controls.Add(apply);
        bool busy = false;
        rows.SelectedIndexChanged+=(s,e)=>{
            var selected=rows.SelectedItems.Count==1 ? rows.SelectedItems[0].Tag as AppPathReference : null;
            apply.Enabled=!busy && selected!=null;
            apply.Text=selected!=null && selected.IsBackup ? "Restaurar referência" : "Corrigir referência";
        };
        Func<AppPathReference,Task> run=async selected=>{
            if(busy)return;
            busy=true;refresh.Enabled=apply.Enabled=false;rows.Enabled=false;
            status.Text=selected==null?"Verificando referências…":"Aplicando a seleção…";
            try{
                var result=await Task.Run(()=>{
                    if(selected!=null)store.Apply(selected);
                    return store.Inspect();
                });
                if(page.IsDisposed)return;
                rows.Items.Clear();
                foreach(var entry in result.Entries)
                    rows.Items.Add(new ListViewItem(new[]{entry.Application,entry.IsBackup?"Cópia guardada":"Destino ausente",entry.RawValue}){Tag=entry});
                string outcome=selected==null?"":selected.IsBackup?"Referência restaurada. ":"Referência removida e cópia guardada. ";
                status.Text=outcome+String.Format("{0} chaves examinadas · {1} referências ausentes · {2} não verificadas.",
                    result.Examined,result.Entries.Count(v=>!v.IsBackup),result.Unverified);
            }catch(Exception ex){if(!page.IsDisposed){rows.Items.Clear();status.Text="Operação não concluída: "+ex.Message;}}
            finally{busy=false;if(!page.IsDisposed){refresh.Enabled=rows.Enabled=true;apply.Enabled=false;}}
        };
        refresh.Click+=async (s,e)=>await run(null);
        apply.Click+=async (s,e)=>{if(rows.SelectedItems.Count==1)await run((AppPathReference)rows.SelectedItems[0].Tag);};
        refresh.PerformClick();
    }
}

#if CRAFT_REGISTRY_TESTS
internal static class RegistryCareVerification {
    static void Check(bool passed, string message) {
        if(!passed)throw new InvalidOperationException(message);
        Console.WriteLine("PASS "+message);
    }
    static void ExpectConflict(Action call) {
        try{call();}catch(IOException){return;}
        throw new InvalidOperationException("Conflito de registro não detectado.");
    }
    internal static void Run() {
        string suffix=Guid.NewGuid().ToString("N");
        string branch=@"Software\CraftRegistryTest-"+suffix;
        string local=Path.Combine(Path.GetTempPath(),"craft-registry-"+suffix+".exe");
        string quoted="\""+local+"\"";
        try{
            using(var fixture=Registry.CurrentUser.CreateSubKey(branch)){
                var store=new RegistryCare(fixture);
                using(var app=fixture.CreateSubKey(RegistryCare.AppPaths+@"\sample.exe")){
                    app.SetValue("",quoted,RegistryValueKind.String);
                    app.SetValue("Path","%TEMP%",RegistryValueKind.ExpandString);
                }
                using(var network=fixture.CreateSubKey(RegistryCare.AppPaths+@"\network.exe"))
                    network.SetValue("",@"\\example.invalid\share\tool.exe");
                var report=store.Inspect();
                Check(report.Examined==2 && report.Unverified==1 && report.Entries.Count==1,
                    "identificação local e exclusão de referência de rede");
                var found=report.Entries.Single();
                File.WriteAllText(local,"fixture");
                ExpectConflict(()=>store.Apply(found));
                File.Delete(local);
                store.Apply(found);
                var backup=store.Inspect().Entries.Single();
                Check(backup.IsBackup && backup.RawValue==quoted,"remoção e cópia literal da referência");
                using(var app=fixture.OpenSubKey(RegistryCare.AppPaths+@"\sample.exe",true)){
                    Check(app.GetValue("")==null && app.GetValueKind("Path")==RegistryValueKind.ExpandString &&
                        (string)app.GetValue("Path",null,RegistryValueOptions.DoNotExpandEnvironmentNames)=="%TEMP%",
                        "valor auxiliar íntegro");
                    app.SetValue("","referência nova");
                    ExpectConflict(()=>store.Apply(backup));
                    Check((string)app.GetValue("")=="referência nova","conflito preserva registro novo");
                    app.DeleteValue("");
                }
                store.Apply(backup);
                using(var app=fixture.OpenSubKey(RegistryCare.AppPaths+@"\sample.exe")){
                    Check((string)app.GetValue("")==quoted && app.GetValueKind("")==RegistryValueKind.String,
                        "restauração literal do valor e tipo originais");
                }
                Check(!store.Inspect().Entries.Any(e=>e.IsBackup),"restauração finalizada");
            }
        }finally{
            File.Delete(local);
            Registry.CurrentUser.DeleteSubKeyTree(branch,false);
        }
    }
}
#endif
