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

internal sealed partial class CraftWindow {
    void AddResourceMonitor(FlowLayoutPanel page) {
        var cpu=new Label{Name="cpu-usage",AutoSize=true,Text="CPU   medindo…"};
        var memory=new Label{Name="ram-usage",AutoSize=true};
        page.Controls.Add(cpu);page.Controls.Add(memory);
        page.Controls.Add(new Label{AutoSize=true,Text="Processos com maior memória residente",Margin=new Padding(3,12,3,3)});
        var list=new ListView{Name="ram-processes",Width=490,Height=185,View=View.Details,FullRowSelect=true,
            BackColor=surface,ForeColor=Color.White};
        list.Columns.Add("Processo",245);list.Columns.Add("PID",85);list.Columns.Add("Memória",135);
        page.Controls.Add(list);
        var timer=new System.Windows.Forms.Timer{Interval=1};
        page.Disposed+=(s,e)=>timer.Dispose();
        ResourceSample previous=null;
        Func<Task> refresh=async ()=>{
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
