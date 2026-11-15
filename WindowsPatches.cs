using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class PatchSearchResult {
    internal readonly List<string[]> Updates = new List<string[]>();
    internal readonly List<string> Warnings = new List<string>();
    internal string Source;
    internal DateTime Finished;
}

internal static class WindowsPatches {
    // OR is only valid at the top level in the WUA search language.
    const string Criteria = "(IsInstalled=0 and IsHidden=0 and Type='Software') or " +
                            "(IsInstalled=0 and IsHidden=0 and Type='Driver' and BrowseOnly=1)";

    static void Release(object instance) {
        if (instance != null && Marshal.IsComObject(instance)) Marshal.ReleaseComObject(instance);
    }

    internal static PatchSearchResult Search(bool online) {
        var report = new PatchSearchResult {
            Source = online ? "Serviço configurado no Windows" : "Catálogo local do Windows"
        };
        dynamic session = null, searcher = null, result = null, updates = null, warnings = null;
        try {
            session = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session", true));
            session.ClientApplicationID = "Advanced System Craft";
            searcher = session.CreateUpdateSearcher();
            searcher.Online = online;
            result = searcher.Search(Criteria);
            int code = result.ResultCode;
            if (code != 2 && code != 3)
                throw new InvalidOperationException("O Windows Update não concluiu a consulta (resultado " + code + ").");
            if (code == 3) report.Warnings.Add("O agente indicou que o catálogo retornado pode estar incompleto.");
            warnings = result.Warnings;
            int warningCount = warnings.Count;
            for (int i = 0; i < warningCount; i++) {
                dynamic warning = warnings[i];
                try {
                    report.Warnings.Add(String.Format("0x{0:X8} {1}", (int)warning.HResult, (string)warning.Message));
                } finally { Release((object)warning); }
            }
            updates = result.Updates;
            int count = updates.Count;
            for (int i = 0; i < count; i++) {
                dynamic update = updates[i];
                try {
                    report.Updates.Add(new[] {
                        (string)update.Title, (int)update.Type == 2 ? "Driver opcional" : "Software",
                        (bool)update.IsDownloaded ? "Baixada, pendente de instalação" : "Disponível para baixar"
                    });
                } finally { Release((object)update); }
            }
            report.Finished = DateTime.Now;
            return report;
        } finally {
            Release((object)updates); Release((object)warnings); Release((object)result);
            Release((object)searcher); Release((object)session);
        }
    }
}

internal sealed partial class CraftWindow {
    Task<PatchSearchResult> patchSearch;
    bool patchSearchOnline;

    async void ShowWindowsPatches() {
        var page = Page("Windows Update");
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        page.Controls.Add(actions);
        var status = new Label { AutoSize = true, MaximumSize = new Size(620, 0), AccessibleName = "Resultado da pesquisa",
            Text = "Consulte atualizações de software e drivers opcionais aplicáveis a este PC." };
        var results = new ListView {
            Width = Math.Max(520, body.Width - 64), Height = 250, View = View.Details,
            FullRowSelect = true, ShowItemToolTips = true, AccessibleName = "Atualizações do Windows"
        };
        results.Columns.Add("Atualização", results.Width - 312);
        results.Columns.Add("Tipo", 108);
        results.Columns.Add("Estado", 178);
        page.Controls.Add(status);
        page.Controls.Add(results);
        Button local = null, network = null;

        Func<Task> display = async () => {
            var request = patchSearch;
            bool online = patchSearchOnline;
            local.Enabled = network.Enabled = false;
            status.Text = online ? "Consultando o serviço configurado no Windows…" : "Lendo o catálogo local do Windows…";
            results.Items.Clear();
            try {
                var report = await request;
                if (page.IsDisposed) return;
                results.BeginUpdate();
                try {
                    foreach (var row in report.Updates)
                        results.Items.Add(new ListViewItem(row) { ToolTipText = row[0] });
                } finally { results.EndUpdate(); }
                status.Text = String.Format("{0} · {1:g} · {2} atualização(ões) retornada(s).",
                    report.Source, report.Finished, report.Updates.Count);
                if (!online) status.Text += " O catálogo local pode estar desatualizado.";
                if (report.Warnings.Count > 0) status.Text += Environment.NewLine + String.Join(Environment.NewLine, report.Warnings);
            } catch (Exception error) {
                if (!page.IsDisposed)
                    status.Text = String.Format("Consulta indisponível (0x{0:X8}). {1}", error.HResult, error.Message);
            } finally {
                if (!page.IsDisposed) local.Enabled = network.Enabled = true;
            }
        };
        Func<bool,Task> start = online => {
            patchSearchOnline = online;
            patchSearch = Task.Run(() => WindowsPatches.Search(online));
            return display();
        };
        local = ActionButton(actions, "Consultar catálogo local", async (s,e) => await start(false), 180);
        network = ActionButton(actions, "Pesquisar online", async (s,e) => await start(true), 155);
        ActionButton(page, "Abrir Windows Update para instalar", (s,e) => {
            try {
                using (Process.Start(new ProcessStartInfo("ms-settings:windowsupdate") { UseShellExecute = true })) {}
            } catch (Win32Exception error) { MessageBox.Show(this, error.Message, Text); }
        }, 286);
        if (patchSearch != null) await display();
    }


}
