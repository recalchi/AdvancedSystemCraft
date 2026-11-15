using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

internal sealed class CareHistory {
    readonly string root;
    internal string LastFile { get; private set; }
    long freeAtScan=-1;
    internal CareHistory(string folder){root=folder;}
    static long SystemFree(out string drive){
        drive=null;
        try{
            var info=new DriveInfo(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
            drive=info.Name.TrimEnd('\\');return info.AvailableFreeSpace;
        }catch{return -1;}
    }
    static string Gb(long bytes){return (bytes/1073741824.0).ToString("0.00")+" GB";}
    internal void SaveScan(string mode,Report report,string diagnostics,long duration){
        LastFile=null;Directory.CreateDirectory(root);
        var stamp=DateTime.Now;
        var name=stamp.ToString("yyyy-MM-dd HH-mm-ss")+" "+Guid.NewGuid().ToString("N").Substring(0,8)+".txt";
        var final=Path.Combine(root,name);
        var draft=final+".partial";
        try{
            using(var writer=new StreamWriter(draft,false,new UTF8Encoding(true))){
                writer.WriteLine("Advanced System Craft · "+stamp.ToString("G"));
                writer.WriteLine("Modo "+mode);
                writer.WriteLine("{0} arquivos examinados · {1} para revisar · {2} inacessíveis · {3} ms",report.Examined,report.Items.Count,report.Skipped,duration);
                string drive;freeAtScan=SystemFree(out drive);
                if(freeAtScan>=0)writer.WriteLine("Livre em "+drive+" antes da correção: "+Gb(freeAtScan));
                writer.WriteLine(diagnostics);
                foreach(var item in report.Items)writer.WriteLine("Encontrado\t{0}\t{1} bytes\t{2}",item.Category,item.Size,item.Path);
            }
            File.Move(draft,final);LastFile=final;
        }finally{if(File.Exists(draft))File.Delete(draft);}
    }
    internal void SaveRepairs(IEnumerable<string> results){
        if(LastFile==null)return;
        using(var writer=new StreamWriter(LastFile,true,new UTF8Encoding(true))){
            writer.WriteLine();writer.WriteLine("Correção · "+DateTime.Now.ToString("G"));
            foreach(var line in results)writer.WriteLine(line);
            string drive;var now=SystemFree(out drive);
            if(now>=0&&freeAtScan>=0){
                var delta=now-freeAtScan;
                writer.WriteLine("Livre em "+drive+" depois da correção: "+Gb(now)+" (variação "+(delta<0?"-":"+")+Gb(Math.Abs(delta))+"). A Lixeira só libera espaço ao ser esvaziada.");
            }
        }
    }
    internal FileInfo[] Reports(){
        var directory=new DirectoryInfo(root);
        return !directory.Exists?new FileInfo[0]:directory.GetFiles("*.txt").OrderByDescending(f=>f.Name,StringComparer.Ordinal).ToArray();
    }
}
internal sealed partial class CraftWindow {
    readonly CareHistory history;
    void ShowHistory(){
        var page=Page("Histórico de cuidado");
        ActionButton(page,"Gerenciar exclusões",(sender,args)=>ShowExclusions(),200);
        var picker=new ComboBox{Width=470,DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Name",AccessibleName="Verificação"};
        page.Controls.Add(picker);
        var preview=new TextBox{Width=Math.Max(500,body.Width-65),Height=290,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false};
        page.Controls.Add(preview);
        picker.SelectedIndexChanged+=(s,e)=>{
            var file=picker.SelectedItem as FileInfo;
            try{if(file!=null)preview.Text=File.ReadAllText(file.FullName);}catch(Exception ex){preview.Text=ex.Message;}
        };
        try{picker.Items.AddRange(history.Reports());if(picker.Items.Count>0)picker.SelectedIndex=0;else preview.Text="Nenhuma verificação registrada.";}
        catch(Exception ex){preview.Text=ex.Message;}
        ActionButton(page,"Exportar relatório",(s,e)=>{
            var file=picker.SelectedItem as FileInfo;if(file==null)return;
            using(var dialog=new SaveFileDialog{Filter="Texto (*.txt)|*.txt",FileName=file.Name}){
                if(dialog.ShowDialog(this)==DialogResult.OK){
                    try{File.Copy(file.FullName,dialog.FileName,true);}catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}
                }
            }
        },180).Enabled=picker.Items.Count>0;
        ActionButton(page,"Abrir Lixeira para restaurar",(s,e)=>{
            try{System.Diagnostics.Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),"shell:RecycleBinFolder");}
            catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}
        },240);
    }
    void ShowExclusions(){
        var page=Page("Arquivos ignorados");
        var entries=new ListBox{Width=Math.Max(500,body.Width-65),Height=220,SelectionMode=SelectionMode.MultiExtended,HorizontalScrollbar=true};
        page.Controls.Add(entries);
        try{entries.Items.AddRange(ignored.Load().OrderBy(path=>path).ToArray());}
        catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}
        var info=new Label{AutoSize=true,Text=entries.Items.Count+" caminho(s) excluído(s) das correções."};page.Controls.Add(info);
        var remove=ActionButton(page,"Incluir na próxima verificação",(s,e)=>{
            var paths=entries.SelectedItems.Cast<string>().ToArray();if(paths.Length==0)return;
            try{ignored.Remove(paths);ShowExclusions();}catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}
        },260);
        remove.Enabled=false;entries.SelectedIndexChanged+=(s,e)=>remove.Enabled=entries.SelectedItems.Count>0;
    }
}
