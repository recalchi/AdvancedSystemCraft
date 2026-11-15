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

internal sealed class SoftwarePackage {
    internal string Name,Id,Installed,Available;
}

internal sealed class CatalogResult {
    internal string Text;
    internal bool Success,Recognized;
    internal readonly List<SoftwarePackage> Packages=new List<SoftwarePackage>();

    internal static bool ValidId(string id) {
        return id!=null && Regex.IsMatch(id,@"\A[A-Za-z0-9][A-Za-z0-9._+-]*\z") && !id.EndsWith("...",StringComparison.Ordinal);
    }

    internal static CatalogResult FromOutput(string stdout,string stderr,int code) {
        var result=new CatalogResult{Success=code==0,Text=(stdout+Environment.NewLine+stderr).Trim()};
        if(code!=0){
            result.Text="WinGet 0x"+code.ToString("X8")+Environment.NewLine+result.Text;
            return result;
        }
        var lines=stdout.Replace("\r\n","\n").Replace('\r','\n').Split('\n');
        // WinGet list prints a localized fixed-width table. Unknown layouts stay available as text.
        var header=new Regex(@"^(?:Name|Nome) +(?<id>Id) +(?<version>Version|Versão)(?: +(?<available>Available|Disponível))? *$",RegexOptions.IgnoreCase);
        for(int i=0;i+1<lines.Length;i++){
            var columns=header.Match(lines[i]);
            if(!columns.Success || !Regex.IsMatch(lines[i+1],@"^-{4,}\s*$"))continue;
            result.Recognized=true;
            int id=columns.Groups["id"].Index, version=columns.Groups["version"].Index;
            var next=columns.Groups["available"];
            for(int row=i+2;row<lines.Length;row++){
                string line=lines[row];
                if(line.Length<=version || line[id-1]!=' ' || line[version-1]!=' ' ||
                    line[id]==' ' || line[version]==' ')continue;
                var name=line.Substring(0,id).Trim();
                var identity=line.Substring(id,version-id).TrimEnd();
                if(name.Length==0 || !ValidId(identity))continue;
                int end=next.Success?Math.Min(next.Index,line.Length):line.Length;
                if(end<=version || (next.Success && line.Length>next.Index && line[next.Index-1]!=' '))continue;
                string installed=line.Substring(version,end-version).TrimEnd();
                string available=next.Success && line.Length>next.Index?line.Substring(next.Index).Trim():String.Empty;
                if(installed.Length==0)continue;
                result.Packages.Add(new SoftwarePackage{Name=name,Id=identity,Installed=installed,Available=available});
            }
            break;
        }
        return result;
    }
}

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
    public static async Task<CatalogResult> ListSoftware(bool updates){
        var args="list --source winget --disable-interactivity"+(updates?" --upgrade-available":"");
        var info=new ProcessStartInfo(WingetPath(),args){CreateNoWindow=true,UseShellExecute=false,
            RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        using(var process=Process.Start(info)){
            var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
            await Task.WhenAll(output,errors).ConfigureAwait(false);
            process.WaitForExit();
            return CatalogResult.FromOutput(output.Result,errors.Result,process.ExitCode);
        }
    }
    public static ProcessStartInfo UpdateAllCommand(){
        return new ProcessStartInfo(WingetPath(),"upgrade --source winget --all --interactive --wait"){UseShellExecute=false};
    }
    public static ProcessStartInfo UpdateCommand(string id){
        if(!CatalogResult.ValidId(id))
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
        AddDefenderCare(page);
        var label=new Label{AutoSize=true,MaximumSize=new System.Drawing.Size(560,0),Text="Consultando o Windows…"};
        page.Controls.Add(label);
        ActionButton(page,"Abrir Segurança do Windows",(s,e)=>{try{Process.Start("windowsdefender:");}catch(Exception ex){MessageBox.Show(ex.Message);}},230);
        var result=await Task.Run(()=>WindowsHealth.Protection());
        if(!page.IsDisposed)label.Text=result;
    }
    void ShowPremium(){
        var page=Page("Funções Pro do programa de referência");
        var info=new Label{AutoSize=true,MaximumSize=new System.Drawing.Size(520,0),
            Text="O selo PRO identifica uma função da edição paga do Advanced SystemCare. No Craft, as funções disponíveis estão incluídas."};
        page.Controls.Add(info);
        ActionButton(page,"Abrir atualização de software",(s,e)=>ShowSoftware(),255);
        ActionButton(page,"Revisar registro do usuário",(s,e)=>ShowRegistryCare(),255);
        var features=new ListView{View=View.Details,FullRowSelect=true,Width=570,Height=265,BackColor=surface,ForeColor=System.Drawing.Color.White};
        features.Columns.Add("Função de referência",300);features.Columns.Add("Disponibilidade no Craft",250);
        features.Items.Add(new ListViewItem(new[]{"Atualizar todos os programas","Disponível via WinGet"}));
        features.Items.Add(new ListViewItem(new[]{"Limpeza do registro","Parcial: App Paths do usuário"}));
        foreach(var feature in new[]{"Atualização automática do próprio aplicativo","Limpeza automática de RAM","Otimização da Internet","Desativação de programas","Proteção antispyware em tempo real","Pastas protegidas","Bloqueio automático de acesso","Proteção de impressão digital do navegador","Antirrastreamento"})
            features.Items.Add(new ListViewItem(new[]{feature,"Ainda sem equivalente"}));
        page.Controls.Add(features);
    }
    Task<CatalogResult> catalogRequest;
    void ShowSoftware(){
        var page=Page("Atualização de software");
        var bar=new FlowLayoutPanel{AutoSize=true};page.Controls.Add(bar);
        var message=new Label{AutoSize=true,MaximumSize=new System.Drawing.Size(600,0),Text="Consulte o catálogo WinGet para comparar versões."};
        page.Controls.Add(message);
        var tabs=new TabControl{Width=Math.Max(480,body.Width-65),Height=285};
        var catalog=new TabPage("Programas");var details=new TabPage("Saída do WinGet");
        tabs.TabPages.Add(catalog);tabs.TabPages.Add(details);page.Controls.Add(tabs);
        var list=new ListView{Name="catalog-packages",Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,
            MultiSelect=false,HideSelection=false,BackColor=surface,ForeColor=System.Drawing.Color.White,ShowItemToolTips=true};
        list.Columns.Add("Programa",230);list.Columns.Add("Instalada",115);list.Columns.Add("Disponível",120);list.Columns.Add("ID WinGet",220);catalog.Controls.Add(list);
        var output=new TextBox{Name="catalog-output",Multiline=true,ReadOnly=true,WordWrap=false,ScrollBars=ScrollBars.Both,
            Dock=DockStyle.Fill,Font=new System.Drawing.Font("Consolas",9)};
        details.Controls.Add(output);
        var batchRow=new FlowLayoutPanel{AutoSize=true,WrapContents=false};page.Controls.Add(batchRow);
        var update=ActionButton(batchRow,"Atualizar selecionado",(s,e)=>{
            if(list.SelectedItems.Count!=1)return;
            var package=(SoftwarePackage)list.SelectedItems[0].Tag;
            if(package.Available.Length==0)return;
            try{using(Process.Start(WindowsHealth.UpdateCommand(package.Id))){}}
            catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}
        },190);
        update.Enabled=false;
        list.SelectedIndexChanged+=(s,e)=>update.Enabled=list.SelectedItems.Count==1 &&
            ((SoftwarePackage)list.SelectedItems[0].Tag).Available.Length!=0;
        var batch=ActionButton(batchRow,"Atualizar todos",(s,e)=>{
            try{using(Process.Start(WindowsHealth.UpdateAllCommand())){}}
            catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}
        },175);
        const string premiumInfo="Recurso Pro no Advanced SystemCare, incluído no Craft.";
        var badge=new Label{Text="PRO",AccessibleDescription=premiumInfo,AccessibleName=premiumInfo,
            AutoSize=true,BackColor=System.Drawing.Color.FromArgb(255,194,87),ForeColor=System.Drawing.Color.FromArgb(36,28,14),
            Font=new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),Margin=new Padding(3,10,0,0),Padding=new Padding(6,2,6,2)};
        batchRow.Controls.Add(badge);
        var hint=new ToolTip();hint.SetToolTip(badge,premiumInfo);hint.SetToolTip(batch,premiumInfo);
        batch.AccessibleDescription=premiumInfo;
        page.Disposed+=(sender,eventArgs)=>hint.Dispose();
        batch.Enabled=false;
        Button available=null,all=null;
        Func<bool,Task> load=async updates=>{
            if(catalogRequest!=null && !catalogRequest.IsCompleted){message.Text="Uma consulta está em andamento. Aguarde e consulte novamente.";return;}
            update.Enabled=batch.Enabled=available.Enabled=all.Enabled=false;
            list.Items.Clear();output.Clear();message.Text="Consultando o catálogo WinGet…";
            try {
                catalogRequest=WindowsHealth.ListSoftware(updates);
                var result=await catalogRequest;
                if(page.IsDisposed)return;
                output.Text=result.Text;
                int upgrades=0;
                list.BeginUpdate();
                try{
                    foreach(var package in result.Packages){
                        if(package.Available.Length!=0)upgrades++;
                        var row=new ListViewItem(new[]{package.Name,package.Installed,
                            package.Available.Length==0?"Não informada":package.Available,package.Id});
                        row.Tag=package;row.ToolTipText=package.Id;list.Items.Add(row);
                    }
                }finally{list.EndUpdate();}
                tabs.SelectedTab=list.Items.Count>0?catalog:details;
                message.Text=list.Items.Count>0?String.Format("{0} programas na tabela · {1} com versão disponível. Selecione uma linha para atualizar.",list.Items.Count,upgrades):
                    result.Success?"Confira o resultado completo na saída do WinGet.":"Falha na consulta. Consulte a saída do WinGet.";
                batch.Enabled=updates && result.Success && upgrades>0;
            }
            catch(Exception ex){if(!page.IsDisposed){message.Text="Consulta indisponível: "+ex.Message;output.Text=ex.Message;}}
            finally{if(!page.IsDisposed)available.Enabled=all.Enabled=true;}
        };
        available=ActionButton(bar,"Buscar atualizações",async (s,e)=>await load(true),180);
        all=ActionButton(bar,"Listar instalados",async (s,e)=>await load(false),155);
    }
}
