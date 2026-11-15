using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

class Verification {
    static void Check(bool ok,string name) {
        if(!ok) throw new Exception(name);
        Console.WriteLine("PASS "+name);
    }
    static void VerifyStartup() {
        string fixture=@"Software\CraftVerification-"+Guid.NewGuid().ToString("N");
        const string raw=@"%TEMP%\Craft fixture.exe";
        using(var root=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(fixture)){
            try{
                using(var active=root.CreateSubKey("Active"))active.SetValue("Fixture",raw,Microsoft.Win32.RegistryValueKind.ExpandString);
                StartupEntry entry;
                using(var active=root.OpenSubKey("Active"))entry=StartupEntry.Read(active,"Fixture",false);
                entry.Move(root,"Active","Paused");
                using(var active=root.OpenSubKey("Active"))Check(active.GetValue("Fixture")==null,"pausa a entrada de amostra");
                using(var copy=root.OpenSubKey("Paused"))entry=StartupEntry.Read(copy,"Fixture",true);
                Check(entry.Command==raw && entry.Kind==Microsoft.Win32.RegistryValueKind.ExpandString,"cópia preserva variáveis e tipo");
                using(var active=root.OpenSubKey("Active",true))active.SetValue("Fixture","outro comando");
                try{entry.Move(root,"Active","Paused");throw new Exception("conflito ignorado");}catch(IOException){}
                using(var active=root.OpenSubKey("Active",true)){
                    Check((string)active.GetValue("Fixture")=="outro comando","conflito preserva entrada nova");
                    active.DeleteValue("Fixture");
                }
                entry.Move(root,"Active","Paused");
                using(var active=root.OpenSubKey("Active"))Check(StartupEntry.Read(active,"Fixture",false).Command==raw,"restaura comando literal");
                using(var paused=root.OpenSubKey("Paused"))Check(paused.ValueCount==0,"restauração conclui a transferência");
            }finally{
                root.DeleteSubKey("Active",false);root.DeleteSubKey("Paused",false);
            }
        }
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(fixture);
    }
    static void VerifyPowerPlans() {
        var plans = PowerPlans.Installed();
        Check(plans.Count > 0 && plans.ContainsKey(PowerPlans.Active()), "enumera o plano de energia ativo");
        string fixture = @"Software\CraftPowerVerification-" + Guid.NewGuid().ToString("N");
        Guid first = Guid.NewGuid(), second = Guid.NewGuid(), third = Guid.NewGuid();
        Guid current = first;
        using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(fixture)) {
            try {
                PowerChange.Apply(key, first, second, () => current, value => current = value);
                Check(current == second && PowerChange.Read(key).Before == first, "registra a troca de plano antes de aplicar");
                current = third;
                try { PowerChange.Undo(key, () => current, value => current = value); throw new Exception("mudança externa ignorada"); }
                catch (InvalidOperationException) { }
                Check(current == third && PowerChange.Read(key) != null, "preserva mudança externa de plano");
                current = second;
                PowerChange.Undo(key, () => current, value => current = value);
                Check(current == first && PowerChange.Read(key) == null, "restaura o plano anterior");
            } finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(fixture, false); }
        }
    }
    [STAThread] static void Main(string[] args) {
        string dir=Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"craft-test-"+Guid.NewGuid())).FullName;
        string temp=Path.Combine(dir,"sample.tmp"), keep=Path.Combine(dir,"recent.tmp"), link=Path.Combine(dir,"missing.lnk");
        try {
            File.WriteAllText(temp,"fixture"); File.SetLastWriteTimeUtc(temp,DateTime.UtcNow.AddDays(-9));
            File.WriteAllText(keep,"fixture");
            dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic shortcut=shell.CreateShortcut(link);shortcut.TargetPath=Path.Combine(dir,"missing.exe");shortcut.Save();
            var report=Engine.Scan(dir,dir,true,true,CancellationToken.None);
            Check(report.Examined==3 && report.Items.Count==2,"identifica temporário antigo e atalho inválido");
            Check(!report.Items.Any(x=>x.Path==keep),"preserva temporário recente");
            var stale=report.Items.Single(x=>x.Path==temp);
            File.AppendAllText(temp,"changed");
            try{Engine.Correct(stale,dir,dir);throw new Exception("aceitou item alterado");}catch(IOException){}
            Check(File.Exists(temp),"revalida antes de corrigir");
            Engine.Correct(report.Items.Single(x=>x.Path==link),dir,dir);
            Check(!File.Exists(link) && File.Exists(keep),"corrige somente o atalho selecionado da amostra");
            try{Engine.Scan(dir,dir,true,true,new CancellationToken(true));throw new Exception("cancelamento ignorado");}
            catch(OperationCanceledException){Console.WriteLine("PASS cancelamento");}
            string root=Directory.CreateDirectory(Path.Combine(dir,"CacheFixture")).FullName;
            string cacheDir=Directory.CreateDirectory(Path.Combine(root,"Default","Code Cache","js")).FullName;
            var entry=Path.Combine(cacheDir,"0123456789abcdef_0");
            var index=Path.Combine(cacheDir,"index");
            try {
                File.WriteAllText(entry,"generated");File.WriteAllText(index,"fixture");
                var cacheReport=new Report(); Engine.ScanCache(cacheReport,root,"craft-fixture-browser",CancellationToken.None);
                Check(cacheReport.Items.Count==1 && cacheReport.Items[0].Path==entry,"reconhece somente fluxo de cache de código");
                Engine.Correct(cacheReport.Items[0],dir,dir);
                Check(!File.Exists(entry) && File.Exists(index),"corrige entrada e preserva índice");
            } finally {
                File.Delete(entry);File.Delete(index);
                Directory.Delete(cacheDir);Directory.Delete(Path.GetDirectoryName(cacheDir));
                Directory.Delete(Path.Combine(root,"Default"));Directory.Delete(root);
            }
            var clock=System.Diagnostics.Stopwatch.StartNew();
            var real=Engine.Scan(Engine.Temp,Engine.Desktop,true,true,CancellationToken.None);
            Console.WriteLine("Leitura local: "+real.Examined+" examinados; "+real.Items.Count+" encontrados; "+real.Skipped+" inacessíveis.");
            Console.WriteLine("Varredura de arquivos concluída em "+clock.ElapsedMilliseconds+" ms.");
            VerifyStartup();
            VerifyPowerPlans();
            var protection=WindowsHealth.Protection();
            Console.WriteLine(protection);
            Check(protection.Length>0,"diagnóstico de proteção");
            Check(WindowsHealth.UpdateCommand("Git.Git").Arguments.Contains("--id Git.Git --exact"),"seleção exata do pacote");
            try{WindowsHealth.UpdateCommand("--all");throw new Exception("ID inválido aceito");}catch(ArgumentException){}
            Check(WindowsHealth.UpdateAllCommand().Arguments=="upgrade --source winget --all --interactive --wait","lote interativo usa catálogo winget");
            Application.EnableVisualStyles();
            using(var window=new CraftWindow()){
                window.Show();Application.DoEvents();
                var privacy=Find<CheckBox>(window,"Cache de código do Chrome e do Edge");
                privacy.Checked=true;Find<RadioButton>(window,"Recomendado").Checked=true;Application.DoEvents();
                Check(!privacy.Visible,"modo recomendado selecionável");
                Find<RadioButton>(window,"Manual").Checked=true;Application.DoEvents();
                Check(privacy.Visible && privacy.Checked,"escolhas manuais preservadas");
                privacy.Checked=false;
                Check(CraftWindow.IncludeCache(9,100) && !CraftWindow.IncludeCache(10,100) && !CraftWindow.IncludeCache(0,0),"limiar de espaço no recomendado");
                Find<RadioButton>(window,"Recomendado").Checked=true;
                var scan=Find<ScanButton>(window,"Verificar");scan.PerformClick();
                var deadline=DateTime.UtcNow.AddSeconds(45);
                do{Application.DoEvents();Thread.Sleep(20);}while(scan.Text=="Parar" && DateTime.UtcNow<deadline);
                Check(Find<Label>(window,null,true)!=null,"verificação pela interface chega ao resumo");
                Find<Button>(window,"Inicialização").PerformClick();Application.DoEvents();
                Check(!Find<Button>(window,"Pausar entrada").Enabled,"alteração exige seleção");
                Check(Find<Button>(window,"Gerenciar nas configurações do Windows")!=null,"leitura da inicialização");
                Find<Button>(window,"Desempenho").PerformClick();Application.DoEvents();
                Check(Find<Button>(window,"Atualizar")!=null,"navegação para discos");
                var end=DateTime.UtcNow.AddSeconds(20);
                var cpu=(Label)window.Controls.Find("cpu-usage",true).Single();
                while(!cpu.Text.Contains("%") && DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(20);}
                var ram=(Label)window.Controls.Find("ram-usage",true).Single();
                var processes=(ListView)window.Controls.Find("ram-processes",true).Single();
                Check(cpu.Text.Contains("%") && ram.Text.Contains("GiB") && processes.Items.Count>0,"monitor real de CPU, RAM e processos");
                Console.WriteLine(cpu.Text+" · "+ram.Text);
                Check(ResourceSample.CpuPercent(new ResourceSample{Total=100,Idle=50},new ResourceSample{Total=200,Idle=75})==75,"cálculo desconta tempo ocioso");
                Check(!ResourceSample.CpuPercent(null,new ResourceSample()).HasValue,"primeira amostra sem percentual");
                Find<Button>(window,"Discos").PerformClick();Application.DoEvents();
                Check(Find<Button>(window,"Verificar sistema de arquivos")!=null,"manutenção de discos pela interface");
                Check(DiskCommands.For(@"E:\",DiskAction.Check).Arguments=="E:" && DiskCommands.For(@"E:\",DiskAction.Repair).Arguments=="E: /F","verificação e reparo usam operações distintas");
                Check(DiskCommands.For(@"E:\",DiskAction.Optimize).Arguments=="E: /O /U /V","otimização respeita o tipo de mídia");
                Find<Button>(window,"Funções Pro").PerformClick();Application.DoEvents();
                Check(Find<Button>(window,"Abrir atualização de software")!=null,"inventário Pro navegável");
                Find<Button>(window,"Software").PerformClick(); Application.DoEvents();
                var lookup=Find<Button>(window,"Buscar atualizações");
                Check(lookup!=null,"consulta de software na interface");
                Check(Find<Label>(window,"PRO").AccessibleDescription.Contains("incluído no Craft"),"selo PRO identificado na tela de software");
                Check(!Find<Button>(window,"Atualizar todos").Enabled,"lote aguarda a prévia");
                if(args.Contains("--catalog")){
                    lookup.PerformClick();var limit=DateTime.UtcNow.AddMinutes(2);
                    do{Application.DoEvents();Thread.Sleep(40);}while(!lookup.Enabled && DateTime.UtcNow<limit);
                    var reports=window.Controls.Find("catalog-output",true);
                    Check(lookup.Enabled && reports.Length==1 && reports[0].Text.Contains("Id") &&
                        !reports[0].Text.StartsWith("WinGet 0x"),"resposta do catálogo real pela interface");
                }
                Find<Button>(window,"Ferramentas").PerformClick();Application.DoEvents();
                Check(Find<Button>(window,"Monitor de recursos")!=null,"navegação para ferramentas");
                Find<Button>(window,"Cuidado").PerformClick();Application.DoEvents();
            }
            if(args.Length>0 && args[0]!="--catalog") using(var preview=new CraftWindow()){
                preview.Show();Application.DoEvents();
                using(var image=new Bitmap(preview.Width,preview.Height)){
                    preview.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
                    image.Save(args[0],System.Drawing.Imaging.ImageFormat.Png);
                    Find<Button>(preview,"Software").PerformClick();Application.DoEvents();
                    preview.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
                    image.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0])),"software.png"),System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        } finally {
            foreach(string file in new[]{link,temp,keep}) if(File.Exists(file)) File.Delete(file);
            Directory.Delete(dir);
        }
    }
    static T Find<T>(Control parent,string text,bool summary=false) where T:Control{
        foreach(Control child in parent.Controls){
            if(child is T && (summary ? child.Text.Contains("examinados") : child.Text==text))return (T)child;
            var match=Find<T>(child,text,summary);if(match!=null)return match;
        }
        return null;
    }
}
