using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal enum DiskAction { Check, Repair, Analyze, Optimize }

internal static class DiskCommands {
    internal static ProcessStartInfo For(string root,DiskAction action){
        if(root==null || root.Length!=3 || !Char.IsLetter(root[0]) || root[1]!=':' || root[2]!='\\')
            throw new ArgumentException("Selecione uma unidade com letra.");
        string tool,flags;
        switch(action){
            case DiskAction.Check: tool="chkdsk.exe";flags="";break;
            case DiskAction.Repair: tool="chkdsk.exe";flags=" /F";break;
            case DiskAction.Analyze: tool="defrag.exe";flags=" /A /V";break;
            case DiskAction.Optimize: tool="defrag.exe";flags=" /O /U /V";break;
            default:throw new ArgumentOutOfRangeException("action");
        }
        return new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,tool),root.Substring(0,2)+flags){
            UseShellExecute=true,Verb="runas",WorkingDirectory=Environment.SystemDirectory
        };
    }
    internal static string Outcome(DiskAction action,int code){
        return "A ferramenta do Windows terminou com código " + code + ". Consulte a janela dela para o diagnóstico e as ações agendadas.";
    }
}

internal sealed partial class CraftWindow {
    bool storageBusy;
    string storageMessage="Selecione a unidade e a operação.";
    Label storageStatus;
    FlowLayoutPanel storageButtons;
    void UpdateStorageView(){
        if(storageStatus==null || storageStatus.IsDisposed)return;
        storageStatus.Text=storageMessage;
        storageButtons.Enabled=!storageBusy;
    }
    async Task RunStorage(string root,DiskAction action){
        if(storageBusy || root==null)return;
        var command=DiskCommands.For(root,action);
        storageBusy=true;
        storageMessage=action==DiskAction.Repair
            ? "Solicitando ao Windows a correção de " + root + ". Se o volume estiver em uso, o CHKDSK poderá oferecer agendamento para a próxima inicialização."
            : "Em execução: "+Path.GetFileName(command.FileName)+" "+command.Arguments;
        UpdateStorageView();
        try{
            using(var process=Process.Start(command)){
                await Task.Run(()=>process.WaitForExit());
                storageMessage=root+"  "+DiskCommands.Outcome(action,process.ExitCode)+"  Código "+process.ExitCode+".";
            }
        }catch(Win32Exception ex){storageMessage=ex.NativeErrorCode==1223?"Operação cancelada antes de iniciar.":ex.Message;}
         catch(Exception ex){storageMessage="A operação não pôde ser concluída: "+ex.Message;}
        finally{storageBusy=false;if(!IsDisposed)UpdateStorageView();}
    }
    void ShowStorage(){
        var page=Page("Manutenção de discos");
        var choice=new ComboBox{Width=210,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Unidade"};
        foreach(var drive in DriveInfo.GetDrives().Where(d=>d.DriveType==DriveType.Fixed))choice.Items.Add(drive.Name);
        if(choice.Items.Count>0)choice.SelectedIndex=0;
        page.Controls.Add(choice);
        storageButtons=new FlowLayoutPanel{Width=510,Height=100};
        page.Controls.Add(storageButtons);
        ActionButton(storageButtons,"Verificar sistema de arquivos",async(s,e)=>await RunStorage(choice.SelectedItem as string,DiskAction.Check),245);
        ActionButton(storageButtons,"Reparar sistema de arquivos",async(s,e)=>await RunStorage(choice.SelectedItem as string,DiskAction.Repair),245);
        ActionButton(storageButtons,"Analisar otimização",async(s,e)=>await RunStorage(choice.SelectedItem as string,DiskAction.Analyze),245);
        ActionButton(storageButtons,"Otimizar unidade",async(s,e)=>await RunStorage(choice.SelectedItem as string,DiskAction.Optimize),245);
        storageStatus=new Label{MaximumSize=new Size(540,0),AutoSize=true};
        page.Controls.Add(storageStatus);UpdateStorageView();
        page.Controls.Add(new Label{AutoSize=true,MaximumSize=new Size(540,0),Text="O Windows abre a ferramenta em uma janela própria e solicita acesso de administrador. A otimização usa a operação apropriada ao tipo de mídia. Reparar pode ser agendado para a próxima inicialização se o volume estiver em uso."});
    }
}
