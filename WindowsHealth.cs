using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class WindowsHealth {
    public static string Protection(){
        var lines=new List<string>();
        try {
            dynamic policy=Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
            try {
                int active=policy.CurrentProfileTypes;
                foreach(var profile in new[]{1,2,4}){
                    string name=profile==1?"Domínio":profile==2?"Particular":"Público";
                    bool enabled=policy.FirewallEnabled[profile];
                    lines.Add("Firewall "+name+" — "+(enabled?"ativado":"desativado")+((active & profile)!=0?" (perfil em uso)":""));
                }
            } finally {Marshal.FinalReleaseComObject(policy);}
        } catch(Exception ex){lines.Add("Firewall indisponível: "+ex.Message);}
        try {
            using(var search=new ManagementObjectSearcher(@"root\SecurityCenter2","SELECT displayName FROM AntiVirusProduct"))
            using(var products=search.Get()){
                if(products.Count==0)lines.Add("Nenhum antivírus informado pela Central de Segurança.");
                foreach(ManagementObject product in products)using(product)lines.Add("Antivírus registrado — "+product["displayName"]);
            }
        } catch(Exception ex){lines.Add("Inventário de antivírus indisponível: "+ex.Message);}
        return String.Join(Environment.NewLine,lines);
    }
    public static string DiskHealth(){
        var lines=new List<string>();
        try {
            using(var search=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT FriendlyName,HealthStatus FROM MSFT_PhysicalDisk"))
            using(var disks=search.Get()){
                foreach(ManagementObject disk in disks)using(disk){
                    var state=Convert.ToString(disk["HealthStatus"]);
                    string health=state=="0"?"saudável":state=="1"?"aviso":state=="2"?"crítico":"desconhecido";
                    lines.Add(Convert.ToString(disk["FriendlyName"])+" — "+health);
                }
            }
        } catch(Exception ex){lines.Add("Saúde dos discos indisponível: "+ex.Message);}
        return lines.Count==0?"O Windows não informou discos físicos.":String.Join(Environment.NewLine,lines);
    }
    public static string VolumeStatus(){
        var lines=new List<string>();
        try{
            using(var search=new ManagementObjectSearcher("SELECT DriveLetter,DirtyBitSet FROM Win32_Volume WHERE DriveType=3"))
            using(var volumes=search.Get()){
                foreach(ManagementObject volume in volumes)using(volume){
                    var letter=volume["DriveLetter"] as string;if(String.IsNullOrEmpty(letter))continue;
                    var state=volume["DirtyBitSet"] as bool?;
                    lines.Add(letter+" — "+(!state.HasValue?"sinalizador indisponível":state.Value?"Windows sinaliza verificação necessária":"sem sinalização de verificação pendente"));
                }
            }
        }catch(Exception ex){lines.Add("Estado dos volumes indisponível: "+ex.Message);}
        return lines.Count==0?"Nenhum volume informado.":String.Join(Environment.NewLine,lines);
    }
    static string WingetPath(){
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Microsoft\WindowsApps\winget.exe");
    }
    public static async Task<string> ListSoftware(bool updates){
        var args="list --source winget --disable-interactivity"+(updates?" --upgrade-available":"");
        var info=new ProcessStartInfo(WingetPath(),args){CreateNoWindow=true,UseShellExecute=false,
            RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        using(var process=Process.Start(info)){
            var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
            await Task.WhenAll(output,errors).ConfigureAwait(false);
            process.WaitForExit();
            var result=(output.Result+Environment.NewLine+errors.Result).Trim();
            if(process.ExitCode!=0)return "WinGet 0x"+process.ExitCode.ToString("X8")+Environment.NewLine+result;
            return result;
        }
    }
    public static ProcessStartInfo UpdateCommand(string id){
        if(id==null || !Regex.IsMatch(id,@"\A[A-Za-z0-9][A-Za-z0-9._+-]*\z"))
            throw new ArgumentException("Informe o ID exato que aparece no catálogo WinGet.");
        return new ProcessStartInfo(WingetPath(),"upgrade --source winget --id "+id+" --exact --interactive --wait"){UseShellExecute=false};
    }
}

internal sealed partial class CraftWindow {
    Button ActionButton(FlowLayoutPanel page,string title,EventHandler click,int width){
        var button=new Button{Width=width,Height=38};Style(button,title);button.Click+=click;page.Controls.Add(button);return button;
    }
    async void ShowProtection(){
        var page=Page("Proteção");
        var label=new Label{AutoSize=true,MaximumSize=new System.Drawing.Size(560,0),Text="Consultando o Windows…"};
        page.Controls.Add(label);
        ActionButton(page,"Abrir Segurança do Windows",(s,e)=>{try{Process.Start("windowsdefender:");}catch(Exception ex){MessageBox.Show(ex.Message);}},230);
        var result=await Task.Run(()=>WindowsHealth.Protection());
        if(!page.IsDisposed)label.Text=result;
    }
    Task<string> catalogRequest;
    void ShowSoftware(){
        var page=Page("Atualização de software");
        var bar=new FlowLayoutPanel{AutoSize=true};page.Controls.Add(bar);
        var output=new TextBox{Name="catalog-output",Multiline=true,ReadOnly=true,WordWrap=false,ScrollBars=ScrollBars.Both,
            Width=Math.Max(480,body.Width-65),Height=280,Font=new System.Drawing.Font("Consolas",9),Text="Consulte as versões pelo catálogo WinGet."};
        page.Controls.Add(output);
        var id=new TextBox{Width=300,AccessibleName="Identificador do pacote"};
        page.Controls.Add(new Label{AutoSize=true,Text="ID do pacote a atualizar"});page.Controls.Add(id);
        ActionButton(page,"Atualizar pacote",(s,e)=>{try{using(Process.Start(WindowsHealth.UpdateCommand(id.Text.Trim()))){}}
            catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}},175);
        Button available=null,all=null;
        Func<bool,Task> load=async updates=>{
            if(catalogRequest!=null && !catalogRequest.IsCompleted){output.Text="Uma consulta está em andamento. Aguarde e consulte novamente.";return;}
            available.Enabled=all.Enabled=false;output.Text="Consultando o catálogo WinGet…";
            try {catalogRequest=WindowsHealth.ListSoftware(updates);var result=await catalogRequest;if(!page.IsDisposed)output.Text=result;}
            catch(Exception ex){if(!page.IsDisposed)output.Text="Consulta indisponível: "+ex.Message;}
            finally{if(!page.IsDisposed)available.Enabled=all.Enabled=true;}
        };
        available=ActionButton(bar,"Buscar atualizações",async (s,e)=>await load(true),180);
        all=ActionButton(bar,"Listar instalados",async (s,e)=>await load(false),155);
    }
}
