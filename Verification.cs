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
            var real=Engine.Scan(Engine.Temp,Engine.Desktop,true,true,CancellationToken.None);
            Console.WriteLine("Leitura local: "+real.Examined+" examinados; "+real.Items.Count+" encontrados; "+real.Skipped+" inacessíveis.");
            Application.EnableVisualStyles();
            using(var window=new CraftWindow()){
                window.Show();Application.DoEvents();
                var scan=Find<ScanButton>(window,"Verificar");scan.PerformClick();
                var deadline=DateTime.UtcNow.AddSeconds(45);
                do{Application.DoEvents();Thread.Sleep(20);}while(scan.Text=="Parar" && DateTime.UtcNow<deadline);
                Check(Find<Label>(window,null,true)!=null,"verificação pela interface chega ao resumo");
                Find<Button>(window,"Inicialização").PerformClick();Application.DoEvents();
                Check(Find<Button>(window,"Gerenciar nas configurações do Windows")!=null,"leitura da inicialização");
                Find<Button>(window,"Desempenho").PerformClick();Application.DoEvents();
                Check(Find<Button>(window,"Atualizar")!=null,"navegação para discos");
                Find<Button>(window,"Ferramentas").PerformClick();Application.DoEvents();
                Check(Find<Button>(window,"Monitor de recursos")!=null,"navegação para ferramentas");
                Find<Button>(window,"Cuidado").PerformClick();Application.DoEvents();
            }
            if(args.Length>0) using(var preview=new CraftWindow()){
                preview.Show();Application.DoEvents();
                using(var image=new Bitmap(preview.Width,preview.Height)){
                    preview.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
                    image.Save(args[0],System.Drawing.Imaging.ImageFormat.Png);
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
