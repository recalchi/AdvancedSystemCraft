using System;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class DefenderStatus {
    internal bool CanRequest;
    internal string Text;
}

internal static class DefenderCare {
    internal const string Scope = @"root\Microsoft\Windows\Defender";
    internal enum ScanType : byte { Quick = 1, Full = 2 }

    internal static ManagementBaseObject PrepareRequest(ManagementClass provider, ScanType scan) {
        if (scan != ScanType.Quick && scan != ScanType.Full)
            throw new ArgumentOutOfRangeException("scan");
        var args = provider.GetMethodParameters("Start");
        args["ScanType"] = (byte)scan;
        return args;
    }

    internal static void Request(ScanType scan) {
        using (var provider = new ManagementClass(Scope, "MSFT_MpScan", null))
        using (var args = PrepareRequest(provider, scan))
        using (var result = provider.InvokeMethod("Start", args, null)) {
            if (result == null || result["ReturnValue"] == null)
                throw new InvalidOperationException("O Defender não informou o resultado da solicitação.");
            var code = Convert.ToUInt32(result["ReturnValue"]);
            if (code != 0) throw new InvalidOperationException("O Defender retornou o código 0x" + code.ToString("X8") + ".");
        }
    }

    static string DateValue(object value) {
        string raw = value as string;
        if (String.IsNullOrEmpty(raw)) return "sem registro";
        try { return ManagementDateTimeConverter.ToDateTime(raw).ToString("g"); }
        catch (ArgumentOutOfRangeException) { return "data indisponível"; }
    }

    static string LatestScanEvent() {
        const string log = "Microsoft-Windows-Windows Defender/Operational";
        const string filter = "*[System[Provider[@Name='Microsoft-Windows-Windows Defender'] and (EventID=1000 or EventID=1001 or EventID=1002 or EventID=1005)]]";
        try {
            var query = new EventLogQuery(log, PathType.LogName, filter) { ReverseDirection = true };
            using (var reader = new EventLogReader(query))
            using (var entry = reader.ReadEvent()) {
                if (entry == null) return "Nenhum evento de exame encontrado.";
                string state;
                switch (entry.Id) {
                    case 1000: state = "iniciado"; break;
                    case 1001: state = "concluído"; break;
                    case 1002: state = "interrompido"; break;
                    default: state = "com falha"; break;
                }
                var time = entry.TimeCreated;
                return "Último evento de exame do Defender: " + state +
                    (time.HasValue ? " em " + time.Value.ToString("g") : "") + " (evento " + entry.Id + ").";
            }
        } catch (EventLogException ex) { return "Log de exames indisponível: " + ex.Message; }
          catch (UnauthorizedAccessException ex) { return "Log de exames indisponível: " + ex.Message; }
    }

    internal static DefenderStatus Read() {
        const string query = "SELECT AMServiceEnabled, AntivirusEnabled, RealTimeProtectionEnabled, QuickScanEndTime, FullScanEndTime FROM MSFT_MpComputerStatus";
        using (var search = new ManagementObjectSearcher(Scope, query))
        using (var results = search.Get()) {
            foreach (ManagementObject item in results) using (item) {
                bool? realtime = item["RealTimeProtectionEnabled"] as bool?;
                bool enabled = Object.Equals(item["AMServiceEnabled"], true) && Object.Equals(item["AntivirusEnabled"], true);
                return new DefenderStatus {
                    CanRequest = enabled,
                    Text = (enabled ? "Microsoft Defender ativo" : "Microsoft Defender indisponível para exame") +
                        " · Proteção em tempo real: " + (!realtime.HasValue ? "desconhecida" : realtime.Value ? "ativada" : "desativada") +
                        Environment.NewLine + "Último exame rápido concluído: " + DateValue(item["QuickScanEndTime"]) +
                        Environment.NewLine + "Último exame completo concluído: " + DateValue(item["FullScanEndTime"]) +
                        Environment.NewLine + LatestScanEvent()
                };
            }
        }
        throw new InvalidOperationException("O Windows não informou o estado do Defender.");
    }
}

internal sealed partial class CraftWindow {
    bool requestingDefenderScan;
    string defenderRequestMessage = "";

    async void AddDefenderCare(FlowLayoutPanel page) {
        var details = new Label { Name = "defender-status", AutoSize = true, MaximumSize = new Size(565, 0), Text = "Consultando o Microsoft Defender…" };
        var notice = new Label { AutoSize = true, MaximumSize = new Size(565, 0), Text = defenderRequestMessage };
        var bar = new FlowLayoutPanel { AutoSize = true };
        page.Controls.Add(details);
        page.Controls.Add(bar);
        page.Controls.Add(notice);
        var quick = new Button { Width = 155, Height = 38, Enabled = false };
        var full = new Button { Width = 155, Height = 38, Enabled = false };
        Style(quick, "Exame rápido"); Style(full, "Exame completo");
        bar.Controls.Add(quick); bar.Controls.Add(full);
        bool reading = false;
        bool enabled = false;
        Func<Task> refresh = async () => {
            if (reading || page.IsDisposed) return;
            reading = true;
            try {
                var status = await Task.Run(() => DefenderCare.Read());
                if (page.IsDisposed) return;
                details.Text = status.Text;
                enabled = status.CanRequest;
            } catch (Exception ex) {
                if (page.IsDisposed) return;
                details.Text = "Consulta ao Microsoft Defender indisponível: " + ex.Message;
                enabled = false;
            } finally {
                reading = false;
                if (!page.IsDisposed) {
                    notice.Text = defenderRequestMessage;
                    quick.Enabled = full.Enabled = enabled && !requestingDefenderScan;
                }
            }
        };
        Func<DefenderCare.ScanType,Task> request = async type => {
            if (requestingDefenderScan || !enabled) return;
            requestingDefenderScan = true;
            quick.Enabled = full.Enabled = false;
            string name = type == DefenderCare.ScanType.Quick ? "rápido" : "completo";
            defenderRequestMessage = "Solicitando exame " + name + " ao Defender…";
            notice.Text = defenderRequestMessage;
            try {
                await Task.Run(() => DefenderCare.Request(type));
                defenderRequestMessage = "O Defender aceitou a solicitação de exame " + name + ".";
            } catch (Exception ex) {
                defenderRequestMessage = "O exame " + name + " não pôde ser solicitado: " + ex.Message;
            } finally { requestingDefenderScan = false; }
            if (!page.IsDisposed) { notice.Text = defenderRequestMessage; await refresh(); }
        };
        quick.Click += async (s,e) => await request(DefenderCare.ScanType.Quick);
        full.Click += async (s,e) => await request(DefenderCare.ScanType.Full);
        var timer = new System.Windows.Forms.Timer { Interval = 15000 };
        timer.Tick += async (s,e) => await refresh();
        page.Disposed += (s,e) => timer.Dispose();
        timer.Start();
        await refresh();
    }
}
