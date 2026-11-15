using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic.Devices;

internal sealed class ResourceSample {
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    internal long Idle, Total;
    internal string Memory;
    internal List<ListViewItem> Processes;

    internal static double? CpuPercent(ResourceSample before, ResourceSample after) {
        if(before==null || after.Total<=before.Total)return null;
        long total=after.Total-before.Total, idle=after.Idle-before.Idle;
        if(idle<0 || idle>total)return null;
        return 100.0*(total-idle)/total;
    }

    internal static ResourceSample Read() {
        long idle,kernel,user;
        if(!GetSystemTimes(out idle,out kernel,out user))throw new Win32Exception();
        var memory=new ComputerInfo();
        var total=memory.TotalPhysicalMemory;
        var available=memory.AvailablePhysicalMemory;
        var entries=new List<Tuple<string,int,long>>();
        foreach(var process in Process.GetProcesses())using(process) {
            try {
                if(process.Id!=0)entries.Add(Tuple.Create(process.ProcessName,process.Id,process.WorkingSet64));
            }catch(InvalidOperationException){}catch(Win32Exception){}
        }
        return new ResourceSample{
            Idle=idle, Total=kernel+user,
            Memory=String.Format("RAM em uso   {0:N1} de {1:N1} GiB     ·     {2:N1} GiB disponíveis",
                (total-available)/1073741824d,total/1073741824d,available/1073741824d),
            Processes=entries.OrderByDescending(p=>p.Item3).ThenBy(p=>p.Item2).Take(8)
                .Select(p=>new ListViewItem(new[]{p.Item1,p.Item2.ToString(),(p.Item3/1048576d).ToString("N1")+" MiB"})).ToList()
        };
    }
}

internal sealed class MemoryTrimResult {
    internal int ProcessId;
    internal long Before, After;
    internal string Error;
}

internal static class MemoryCare {
    [DllImport("psapi.dll", SetLastError=true)]
    static extern bool EmptyWorkingSet(IntPtr process);

    internal static MemoryTrimResult Trim(Process process) {
        var result=new MemoryTrimResult { ProcessId=process.Id };
        try {
            if(process.Id==0 || process.SessionId!=Process.GetCurrentProcess().SessionId)
                throw new InvalidOperationException("Selecione um processo da sua sessão.");
            result.Before=process.WorkingSet64;
            if(!EmptyWorkingSet(process.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error());
            process.Refresh();result.After=process.WorkingSet64;
        } catch(Exception error) { result.Error=error.Message; }
        return result;
    }
}

internal sealed partial class CraftWindow {
    void AddResourceMonitor(FlowLayoutPanel page) {
        var cpu=new Label{Name="cpu-usage",AutoSize=true,Text="CPU   medindo…"};
        var memory=new Label{Name="ram-usage",AutoSize=true};
        page.Controls.Add(cpu);page.Controls.Add(memory);
        page.Controls.Add(new Label{AutoSize=true,Text="Processos com maior memória residente",Margin=new Padding(3,12,3,3)});
        var list=new ListView{Name="ram-processes",Width=490,Height=185,View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,
            BackColor=surface,ForeColor=Color.White};
        list.Columns.Add("Processo",245);list.Columns.Add("PID",85);list.Columns.Add("Memória",135);
        page.Controls.Add(list);
        var trimStatus=new Label{Name="ram-trim-status",AutoSize=true,MaximumSize=new Size(520,0),Text="Selecione um processo da sua sessão para solicitar ao Windows a liberação de páginas de memória residentes."};
        Func<Task> refresh=null;
        Button trim=null;
        trim=ActionButton(page,"Liberar memória do processo",async (s,e)=>{
            if(list.SelectedItems.Count!=1)return;
            var pid=Int32.Parse(list.SelectedItems[0].SubItems[1].Text);
            trim.Enabled=false;trimStatus.Text="Solicitando ao Windows…";
            var result=await Task.Run(()=>{try{using(var process=Process.GetProcessById(pid))return MemoryCare.Trim(process);}catch(Exception error){return new MemoryTrimResult{ProcessId=pid,Error=error.Message};}});
            if(!page.IsDisposed){
                trimStatus.Text=result.Error!=null?"Não foi possível liberar memória: "+result.Error:
                    String.Format("Processo {0}: {1:N1} MiB antes · {2:N1} MiB depois.",result.ProcessId,result.Before/1048576d,result.After/1048576d);
                await refresh();
            }
        },240);
        trim.Enabled=false;page.Controls.Add(trim);page.Controls.Add(trimStatus);
        list.SelectedIndexChanged+=(s,e)=>trim.Enabled=list.SelectedItems.Count==1;
        var timer=new System.Windows.Forms.Timer{Interval=1};
        page.Disposed+=(s,e)=>timer.Dispose();
        ResourceSample previous=null;
        refresh=async ()=>{
            timer.Stop();
            try {
                var sample=await Task.Run(()=>ResourceSample.Read());
                if(page.IsDisposed)return;
                var usage=ResourceSample.CpuPercent(previous,sample);previous=sample;
                cpu.Text=usage.HasValue?String.Format("CPU   {0:N1}%",usage.Value):"CPU   aguardando segunda amostra…";
                memory.Text=sample.Memory;
                list.BeginUpdate();
                try{list.Items.Clear();list.Items.AddRange(sample.Processes.ToArray());}finally{list.EndUpdate();}
            }catch(Exception ex){if(!page.IsDisposed){previous=null;cpu.Text="Medição indisponível: "+ex.Message;memory.Text="";list.Items.Clear();}}
            finally{if(!page.IsDisposed)timer.Start();}
        };
        timer.Tick+=async (s,e)=>{timer.Interval=2000;await refresh();};
        timer.Start();
    }
}
