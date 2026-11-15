using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class PowerPlans {
    const uint AccessScheme = 16, NoMoreItems = 259;
    [DllImport("powrprof.dll")]
    static extern uint PowerEnumerate(IntPtr key, IntPtr scheme, IntPtr group, uint accessor, uint index,
        out Guid value, ref uint size);
    [DllImport("powrprof.dll")]
    static extern uint PowerReadFriendlyName(IntPtr key, ref Guid scheme, IntPtr group, IntPtr setting,
        [Out] byte[] buffer, ref uint size);
    [DllImport("powrprof.dll")]
    static extern uint PowerGetActiveScheme(IntPtr key, out IntPtr scheme);
    [DllImport("powrprof.dll")]
    static extern uint PowerSetActiveScheme(IntPtr key, ref Guid scheme);
    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr address);

    static void Check(uint error) { if (error != 0) throw new Win32Exception((int)error); }

    internal static Guid Active() {
        IntPtr address;
        Check(PowerGetActiveScheme(IntPtr.Zero, out address));
        try { return (Guid)Marshal.PtrToStructure(address, typeof(Guid)); }
        finally { LocalFree(address); }
    }
    internal static void SetActive(Guid id) { Check(PowerSetActiveScheme(IntPtr.Zero, ref id)); }

    internal static Dictionary<Guid,string> Installed() {
        var plans = new Dictionary<Guid,string>();
        for (uint index=0; ; index++) {
            Guid id;
            uint size = 16;
            uint result = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, AccessScheme, index, out id, ref size);
            if (result == NoMoreItems) break;
            Check(result);
            size = 0;
            uint query = PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero, IntPtr.Zero, null, ref size);
            string name = id.ToString();
            if ((query == 0 || query == 234) && size > 0) {
                var bytes = new byte[size];
                if (PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero, IntPtr.Zero, bytes, ref size) == 0)
                    name = Encoding.Unicode.GetString(bytes, 0, (int)size).TrimEnd('\0');
            }
            plans.Add(id, name);
        }
        return plans;
    }
}

internal sealed class PowerChange {
    const string Value = "LastPowerPlanChange";
    internal Guid Before, After;

    internal static PowerChange Read(RegistryKey key) {
        object raw = key == null ? null : key.GetValue(Value);
        if (raw == null) return null;
        var fields = raw as string[];
        Guid before, after;
        if (fields == null || fields.Length != 2 || !Guid.TryParse(fields[0], out before) ||
            !Guid.TryParse(fields[1], out after) || before == after)
            throw new InvalidOperationException("Histórico de energia inválido. O registro de restauração foi preservado.");
        return new PowerChange { Before=before, After=after };
    }
    static void Store(RegistryKey key, PowerChange change) {
        if (change == null) key.DeleteValue(Value, false);
        else key.SetValue(Value, new[] { change.Before.ToString(), change.After.ToString() }, RegistryValueKind.MultiString);
        key.Flush();
    }
    internal static void Apply(RegistryKey key, Guid expected, Guid selected, Func<Guid> active, Action<Guid> set) {
        if (active() != expected)
            throw new InvalidOperationException("O plano ativo mudou. Atualize a lista e escolha novamente.");
        if (selected == expected) return;
        var oldRecord = Read(key);
        Store(key, new PowerChange { Before=expected, After=selected });
        try {
            set(selected);
            if (active() != selected)
                throw new InvalidOperationException("O Windows não confirmou o plano escolhido.");
        } catch {
            if (active() == expected) Store(key, oldRecord);
            throw;
        }
    }
    internal static void Undo(RegistryKey key, Func<Guid> active, Action<Guid> set) {
        var record = Read(key);
        if (record == null) throw new InvalidOperationException("Ainda não há alteração do Craft para desfazer.");
        Guid current = active();
        if (current != record.Before) {
            if (current != record.After)
                throw new InvalidOperationException("Outro plano está ativo. A seleção externa foi preservada.");
            set(record.Before);
            if (active() != record.Before)
                throw new InvalidOperationException("O Windows não confirmou a restauração. Tente novamente.");
        }
        Store(key, null);
    }
}

internal sealed partial class CraftWindow {
    const string PowerHistoryKey = @"Software\AdvancedSystemCraft";
    void ShowAcceleration() {
        var page = Page("Aceleração");
        page.Controls.Add(new Label { AutoSize=true, Text="Plano de energia do Windows", Font=new Font(Font,FontStyle.Bold) });
        var plans = new ListView { Width=Math.Max(470,body.Width-70), Height=175, View=View.Details,
            FullRowSelect=true, MultiSelect=false, HideSelection=false, AccessibleName="Planos de energia instalados" };
        plans.Columns.Add("Plano",320); plans.Columns.Add("Estado",120);
        page.Controls.Add(plans);
        var status = new Label { AutoSize=true, MaximumSize=new Size(600,0) };
        var row = new FlowLayoutPanel { AutoSize=true, WrapContents=true };
        page.Controls.Add(row);
        page.Controls.Add(status);
        Guid shownActive = Guid.Empty;
        bool ready=false;
        Button apply=null, undo=null;
        Action refresh = () => {
            ready=false; apply.Enabled=undo.Enabled=false; plans.Items.Clear();
            try {
                var installed=PowerPlans.Installed();
                shownActive=PowerPlans.Active();
                foreach(var plan in installed)
                    plans.Items.Add(new ListViewItem(new[]{plan.Value,plan.Key==shownActive?"Em uso":""}){Tag=plan.Key});
                using(var key=Registry.CurrentUser.OpenSubKey(PowerHistoryKey)){
                    var record=PowerChange.Read(key);
                    if(record==null)status.Text="Selecione o plano que deseja usar.";
                    else if(shownActive!=record.After && shownActive!=record.Before)
                        status.Text="O plano mudou fora do Craft. A restauração está suspensa.";
                    else {
                        string name;
                        bool present=installed.TryGetValue(record.Before,out name);
                        status.Text=present?"Plano anterior: "+name:"O plano anterior já não está disponível neste PC.";
                        undo.Enabled=present;
                    }
                }
                ready=true;
            } catch(Exception error){status.Text="Consulta indisponível: "+error.Message;}
        };
        Action<bool> change = restore => {
            string message;
            try{
                using(var gate=new Mutex(false,@"Local\AdvancedSystemCraft.PowerPlan")){
                    bool owned;
                    try{owned=gate.WaitOne(0);}catch(AbandonedMutexException){owned=true;}
                    if(!owned)throw new InvalidOperationException("Outra janela está alterando o plano de energia.");
                    try {
                        using(var key=Registry.CurrentUser.CreateSubKey(PowerHistoryKey)){
                            if(restore)PowerChange.Undo(key,PowerPlans.Active,PowerPlans.SetActive);
                            else if(plans.SelectedItems.Count==1)
                                PowerChange.Apply(key,shownActive,(Guid)plans.SelectedItems[0].Tag,PowerPlans.Active,PowerPlans.SetActive);
                            else return;
                        }
                    } finally {gate.ReleaseMutex();}
                }
                message=restore?"Plano anterior restaurado.":"Plano escolhido confirmado pelo Windows.";
            } catch(Exception error){message=error.Message;}
            refresh();
            status.Text=message+Environment.NewLine+status.Text;
        };
        apply=ActionButton(row,"Aplicar plano selecionado",(s,e)=>change(false),180);
        undo=ActionButton(row,"Desfazer alteração",(s,e)=>change(true),155);
        ActionButton(row,"Atualizar lista",(s,e)=>refresh(),125);
        plans.SelectedIndexChanged+=(s,e)=>apply.Enabled=ready && plans.SelectedItems.Count==1 &&
            (Guid)plans.SelectedItems[0].Tag!=shownActive;
        refresh();
    }
}
