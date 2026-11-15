using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;
[assembly: AssemblyTitle("Advanced System Craft")]
[assembly: AssemblyProduct("Advanced System Craft")]
[assembly: AssemblyCompany("Renan Recalde")]
[assembly: AssemblyCopyright("Copyright © 2026 Renan Recalde")]
[assembly: AssemblyDescription("Ferramenta local de manutenção para Windows.")]
[assembly: AssemblyVersion("0.6.4.0")]

internal sealed class ScanButton : Button {
    public ScanButton() {
        Text="Verificar"; Size=new Size(158,158); FlatStyle=FlatStyle.Flat;
        Cursor=Cursors.Hand; AccessibleName="Verificar"; DoubleBuffered=true;
    }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent.BackColor);
        var circle=Rectangle.Inflate(ClientRectangle,-8,-8);
        using(var fill=new SolidBrush(Color.FromArgb(21,53,75)))
        using(var ring=new Pen(Focused ? Color.White : Color.Turquoise,4)) {
            e.Graphics.FillEllipse(fill,circle); e.Graphics.DrawEllipse(ring,circle);
        }
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
internal sealed partial class CraftWindow : Form {
    readonly Color surface=Color.FromArgb(22,32,49);
    readonly FlowLayoutPanel menu=new FlowLayoutPanel();
    readonly Panel body=new Panel();
    readonly Panel care=new Panel();
    readonly CheckBox junk=new CheckBox(), shortcuts=new CheckBox(), protection=new CheckBox(), hardware=new CheckBox(), privacy=new CheckBox(), volumes=new CheckBox();
    readonly RadioButton recommended=new RadioButton{Text="Recomendado",AutoSize=true};
    readonly RadioButton manual=new RadioButton{Text="Manual",AutoSize=true,Checked=true};
    readonly Button again=new Button();
    readonly TableLayoutPanel careLayout=new TableLayoutPanel();
    readonly FlowLayoutPanel options=new FlowLayoutPanel();
    readonly Label careHeading=new Label();
    void ResultsMode(bool reviewing){
        options.Visible=!reviewing;
        careLayout.RowStyles[1].Height=reviewing?0:215;
        careHeading.Text=reviewing?"Resultado":"Cuidado";
        again.Visible=reviewing;recommended.Visible=manual.Visible=!reviewing;
    }
    readonly TextBox diagnostic=new TextBox();
    readonly ScanButton scan=new ScanButton();
    readonly ListView list=new ListView();
    readonly Label status=new Label();
    readonly Button fix=new Button(), mark=new Button(), ignore=new Button();
    readonly IgnoredPaths ignored=IgnoredPaths.ForUser();
    readonly Label review=new Label{Dock=DockStyle.Top,Height=40,TextAlign=ContentAlignment.MiddleLeft};
    CancellationTokenSource operation;
    bool repairing;

    public CraftWindow() : this(null) {}
    internal CraftWindow(string historyFolder) {
        history=new CareHistory(historyFolder??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AdvancedSystemCraft","Historico"));
        Text="Advanced System Craft"; Font=new Font("Segoe UI",10); ForeColor=Color.White;
        BackColor=surface; MinimumSize=new Size(840,600); ClientSize=new Size(980,640);
        StartPosition=FormStartPosition.CenterScreen;
        menu.AutoScroll=true;menu.WrapContents=false;menu.Dock=DockStyle.Left; menu.Width=192; menu.FlowDirection=FlowDirection.TopDown;
        menu.Padding=new Padding(12,24,8,0); menu.BackColor=Color.FromArgb(15,23,38);
        var brand=new Label {Text="Advanced\nSystem Craft", Size=new Size(163,88), Font=new Font(Font.FontFamily,16,FontStyle.Bold)};
        menu.Controls.Add(brand);
        AddNav("Cuidado", ShowCare);AddNav("Histórico",ShowHistory); AddNav("Proteção", ShowProtection); AddNav("Software", ShowSoftware); AddNav("Inicialização", ShowStartup); AddNav("Desempenho", ShowPerformance); AddNav("Aceleração",ShowAcceleration); AddNav("Discos",ShowStorage);AddNav("Windows Update",ShowWindowsPatches); AddNav("Ferramentas", ShowTools); AddNav("Funções Pro", ShowPremium);
        body.Dock=DockStyle.Fill; Controls.Add(body); Controls.Add(menu);
        care.Dock=DockStyle.Fill; care.Padding=new Padding(22);
        var layout=careLayout;layout.Dock=DockStyle.Fill;layout.ColumnCount=1;layout.RowCount=4;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,54)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,215));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,91));
                var heading=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
        careHeading.Text="Cuidado";careHeading.AutoSize=true;careHeading.Font=new Font(Font.FontFamily,18,FontStyle.Bold);careHeading.Margin=new Padding(0,0,18,0);heading.Controls.Add(careHeading);
        heading.Controls.Add(recommended);heading.Controls.Add(manual);
        Style(again,"Nova verificação");again.AutoSize=true;again.Visible=false;heading.Controls.Add(again);
        again.Click+=(s,e)=>{
            ResultsMode(false);review.Text="Selecione as categorias e clique em Verificar.";
            list.Items.Clear();list.Visible=diagnostic.Visible=false;status.Text="";UpdateFix();
        };
        layout.Controls.Add(heading,0,0);
        options.Dock=DockStyle.Fill;options.WrapContents=false;
        scan.Font=new Font(Font.FontFamily,16,FontStyle.Bold); scan.Click+=async (s,e)=>await RunScan();
        options.Controls.Add(scan);
        var checks=new FlowLayoutPanel {FlowDirection=FlowDirection.TopDown,Padding=new Padding(15,2,0,0),Size=new Size(420,208)};
        SetupCheck(checks,junk,"Temporários .tmp com mais de 7 dias"); SetupCheck(checks,shortcuts,"Atalhos inválidos da Área de Trabalho");
        SetupCheck(checks,protection,"Proteção antivírus e firewall");
        SetupCheck(checks,hardware,"Saúde dos discos físicos");
        SetupCheck(checks,privacy,"Cache de código do Chrome e do Edge"); privacy.Checked=false;
        SetupCheck(checks,volumes,"Verificação pendente nos volumes");
        var selectAll=new Button{Text="Selecionar tudo",AutoSize=true};
        Style(selectAll,"Selecionar tudo");
        selectAll.Click+=(s,e)=>{if(operation==null && !repairing)junk.Checked=shortcuts.Checked=protection.Checked=hardware.Checked=volumes.Checked=privacy.Checked=true;};
        checks.Controls.Add(selectAll);
                var explanation=new Label{MaximumSize=new Size(365,0),AutoSize=true,Visible=false,
            Text="Verifica temporários, atalhos, proteção e discos. Inclui o cache de código quando a unidade do perfil tem menos de 10% de espaço disponível."};
        recommended.CheckedChanged+=(s,e)=>{checks.Visible=!recommended.Checked;explanation.Visible=recommended.Checked;};
        options.Controls.Add(checks);options.Controls.Add(explanation);layout.Controls.Add(options,0,1);
        list.Dock=DockStyle.Fill; list.View=View.Details; list.CheckBoxes=true; list.FullRowSelect=true;
        list.BackColor=Color.FromArgb(28,42,59); list.ForeColor=Color.White; list.BorderStyle=BorderStyle.None;
        list.Columns.Add("Arquivo",155); list.Columns.Add("Local",270); list.Columns.Add("Categoria",145); list.Columns.Add("Tamanho",85);
        list.ShowItemToolTips=true;
        list.ItemChecked+=(s,e)=>BeginInvoke((Action)UpdateFix);
        var results=new Panel{Dock=DockStyle.Fill};
        diagnostic.Multiline=true;diagnostic.ReadOnly=true;diagnostic.ScrollBars=ScrollBars.Vertical;diagnostic.Dock=DockStyle.Bottom;
        diagnostic.Height=105;diagnostic.BackColor=surface;diagnostic.ForeColor=Color.White;diagnostic.Visible=false;
        review.Text="Os resultados aparecerão aqui após a verificação.";
        list.Visible=false;results.Controls.Add(list);results.Controls.Add(diagnostic);results.Controls.Add(review);layout.Controls.Add(results,0,2);
        var foot=new Panel {Dock=DockStyle.Fill,Padding=new Padding(0,8,0,4)};
        status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;status.Text="Selecione as categorias e clique em Verificar.";
        Style(fix,"Corrigir seleção");fix.Dock=DockStyle.Right;fix.Width=150;fix.Enabled=false;
        fix.Click+=async (s,e)=>await Repair();
        Style(mark,"Marcar todos");mark.Dock=DockStyle.Right;mark.Width=132;mark.Visible=false;
        mark.Click+=(s,e)=>{bool check=list.CheckedItems.Count!=list.Items.Count;list.BeginUpdate();try{foreach(ListViewItem row in list.Items)row.Checked=check;}finally{list.EndUpdate();}UpdateFix();};
        Style(ignore,"Ignorar marcados");ignore.Width=155;ignore.Dock=DockStyle.Right;ignore.Visible=false;
        ignore.Click+=(s,e)=>{
            var rows=list.CheckedItems.Cast<ListViewItem>().ToArray();if(rows.Length==0)return;
            try{
                ignored.Ignore(rows.Select(row=>((Finding)row.Tag).Path));
                foreach(var row in rows)list.Items.Remove(row);
                list.Visible=list.Items.Count>0;review.Text=rows.Length+" arquivo(s) na lista de exclusões. Restam "+list.Items.Count+" para revisar.";
                UpdateFix();
            }catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}
        };
        var buttons=new Panel{Dock=DockStyle.Bottom,Height=36};
        buttons.Controls.Add(ignore);buttons.Controls.Add(mark);buttons.Controls.Add(fix);
        foot.Controls.Add(status);foot.Controls.Add(buttons);layout.Controls.Add(foot,0,3);
        care.Controls.Add(layout);ShowCare();
        FormClosing+=(s,e)=>{if(repairing){e.Cancel=true;}else if(operation!=null){operation.Cancel();}};
    }
    void SetupCheck(FlowLayoutPanel panel,CheckBox check,string label) {
        check.Text=label;check.AutoSize=true;check.Checked=true;panel.Controls.Add(check);
    }
    void Style(Button b,string name) {
        b.Text=name;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;
        b.ForeColor=Color.White;b.BackColor=Color.FromArgb(36,66,86);b.Cursor=Cursors.Hand;
    }
    Label Title(string text) {return new Label {Text=text,Dock=DockStyle.Top,Height=50,Font=new Font(Font.FontFamily,16,FontStyle.Bold)};}
    void AddNav(string text,Action action) {
        var b=new Button {Size=new Size(163,44),Margin=new Padding(0,3,0,3)};Style(b,text);
        b.Click+=(s,e)=>action();menu.Controls.Add(b);
    }
    void ClearBody() {
        foreach(Control c in body.Controls.Cast<Control>().ToArray()) {body.Controls.Remove(c);if(c!=care)c.Dispose();}
    }
    void ShowCare(){ClearBody();body.Controls.Add(care);}
    internal static bool IncludeCache(long available,long capacity){return capacity>0 && available>=0 && available/(double)capacity<0.10;}
    void UpdateFix(){fix.Enabled=operation==null && !repairing && list.CheckedItems.Count>0;ignore.Visible=mark.Visible=list.Visible && list.Items.Count>0;ignore.Enabled=fix.Enabled;mark.Enabled=operation==null && !repairing;mark.Text=list.CheckedItems.Count==list.Items.Count?"Desmarcar todos":"Marcar todos";}
    async Task RunScan() {
        if(operation!=null){operation.Cancel();return;}
        if(!recommended.Checked && !junk.Checked && !shortcuts.Checked && !protection.Checked && !hardware.Checked && !privacy.Checked && !volumes.Checked){status.Text="Selecione ao menos uma categoria.";return;}
        bool files=junk.Checked,links=shortcuts.Checked, security=protection.Checked, disks=hardware.Checked, browsers=privacy.Checked, examineVolumes=volumes.Checked;
        bool automatic=recommended.Checked;
        if(automatic){files=links=security=disks=examineVolumes=true; browsers=false;}
        manual.Enabled=recommended.Enabled=false;
        operation=new CancellationTokenSource();var token=operation.Token;
        review.Text="Examinando as categorias selecionadas…"; mark.Visible=false; list.Visible=false; list.Items.Clear(); fix.Enabled=false; diagnostic.Visible=false; junk.Enabled=shortcuts.Enabled=hardware.Enabled=volumes.Enabled=privacy.Enabled=protection.Enabled=false; scan.Text="Parar"; status.Text="Verificando arquivos locais…";
        try {
            var timer=Stopwatch.StartNew(); var report=await Task.Run(()=>{var findings=Engine.Scan(Engine.Temp,Engine.Desktop,files,links,token);if(automatic){
                try{
                    var root=Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                    var drive=new DriveInfo(root);
                    browsers=IncludeCache(drive.AvailableFreeSpace,drive.TotalSize);
                    findings.Notices.Add(browsers?"Cache incluído por espaço disponível abaixo de 10%.":"Espaço disponível de pelo menos 10%; cache preservado.");
                }catch(IOException){findings.Notices.Add("Espaço disponível desconhecido; cache preservado.");}
                 catch(UnauthorizedAccessException){findings.Notices.Add("Acesso à capacidade indisponível; cache preservado.");}
            }
            if(browsers)Engine.ScanBrowserCaches(findings,token);
            var exclusions=ignored.Load();int hidden=findings.Items.RemoveAll(item=>exclusions.Contains(item.Path));
            if(hidden>0)findings.Notices.Add(hidden+" arquivo(s) ignorado(s) por escolha do usuário.");
            return findings;});
            if(IsDisposed)return;review.Text=String.Format("{0} arquivos examinados. Concluindo as verificações selecionadas…",report.Examined);
            var details=await Task.Run(()=>{
                var sections=new System.Collections.Generic.List<string>(report.Notices);
                token.ThrowIfCancellationRequested();
                if(security)sections.Add(WindowsHealth.Protection());
                token.ThrowIfCancellationRequested();
                if(disks)sections.Add(WindowsHealth.DiskHealth());
                token.ThrowIfCancellationRequested();
                if(examineVolumes)sections.Add(WindowsHealth.VolumeStatus());
                token.ThrowIfCancellationRequested();
                return String.Join(Environment.NewLine,sections);
            });
            if(IsDisposed)return;
            diagnostic.Text=details;diagnostic.Visible=details.Length>0;
            list.BeginUpdate();
            foreach(var item in report.Items) {
                var row=new ListViewItem(new[]{Path.GetFileName(item.Path),item.Path,item.Category,item.Size.ToString("N0")+" B"});
                row.Tag=item;row.ToolTipText=item.Target==null?item.Path:"Destino ausente: "+item.Target;
                list.Items.Add(row);
            }
            list.EndUpdate();list.Visible=report.Items.Count>0;ResultsMode(true);
            review.Text=report.Items.Count==0
                ? "Verificação concluída. Nenhum arquivo elegível para correção nas áreas examinadas."
                : String.Format("{0} arquivo(s) para revisar · {1:N0} bytes. Marque os itens que deseja enviar à Lixeira.",report.Items.Count,report.Items.Sum(item=>item.Size));
            if(report.Skipped>0)review.Text+=" Cobertura parcial.";status.Text=String.Format("{0} examinados   ·   {1} para revisar   ·   {2} inacessíveis   ·   {3} ms",report.Examined,report.Items.Count,report.Skipped,timer.ElapsedMilliseconds);
            try{history.SaveScan(automatic?"recomendado":"manual",report,details,timer.ElapsedMilliseconds);}catch(Exception logError){status.Text+=" · Falha no histórico: "+logError.Message;}
        } catch(OperationCanceledException){if(!IsDisposed){review.Text="Verificação cancelada.";status.Text="Clique em Verificar para começar novamente.";};}
          catch(Exception ex){if(!IsDisposed){review.Text="Não foi possível concluir a verificação.";status.Text=ex.Message;};}
        finally {operation.Dispose();operation=null;if(!IsDisposed){manual.Enabled=recommended.Enabled=true;junk.Enabled=shortcuts.Enabled=hardware.Enabled=volumes.Enabled=privacy.Enabled=protection.Enabled=true; scan.Text="Verificar";UpdateFix();}}
    }
    async Task Repair(){
        var selected=list.CheckedItems.Cast<ListViewItem>().ToArray();
        if(selected.Length==0)return;
        repairing=true;again.Enabled=false;scan.Enabled=ignore.Enabled=mark.Enabled=fix.Enabled=list.Enabled=false;int ok=0;var outcomes=new System.Collections.Generic.List<string>();review.Text="Enviando os arquivos selecionados à Lixeira…";
        try{
            var exclusions=ignored.Load();
            foreach(var row in selected){
                try {var item=(Finding)row.Tag; if(exclusions.Contains(item.Path))throw new IOException("Arquivo na lista de exclusões."); await Task.Run(()=>Engine.Correct(item,Engine.Temp,Engine.Desktop));
                    outcomes.Add("Lixeira\t"+item.Path); list.Items.Remove(row);ok++;}
                catch(Exception ex){outcomes.Add("Falha\t"+((Finding)row.Tag).Path+"\t"+ex.Message);row.ToolTipText=ex.Message;row.SubItems[2].Text="Falha na correção";row.Checked=false;}
            }
            list.Visible=list.Items.Count>0;review.Text=String.Format("Correção concluída. {0} arquivo(s) enviado(s) à Lixeira. Restam {1} na lista.",ok,list.Items.Count);
            status.Text=String.Format("{0} enviados à Lixeira   ·   {1} falhas",ok,selected.Length-ok);
            try{history.SaveRepairs(outcomes);}catch(Exception logError){status.Text+=" · Falha no histórico: "+logError.Message;}
        }catch(Exception ex){review.Text="Correção interrompida.";status.Text=ex.Message;}finally{repairing=false;again.Enabled=true;scan.Enabled=list.Enabled=true;UpdateFix();}
    }
    FlowLayoutPanel Page(string name){
        ClearBody();var p=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(24)};
        var title=Title(name);title.Width=500;p.Controls.Add(title);body.Controls.Add(p);return p;
    }
    void ShowStartup(){
        var p=Page("Programas de inicialização");
        var entries=new ListView{View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,Size=new Size(565,270),BackColor=surface,ForeColor=Color.White};
        entries.Columns.Add("Programa",170);entries.Columns.Add("Origem",100);entries.Columns.Add("Comando ou arquivo",285);p.Controls.Add(entries);
        int skipped=0;
        foreach(var hive in new[]{Microsoft.Win32.RegistryHive.CurrentUser,Microsoft.Win32.RegistryHive.LocalMachine}){
            var views=(Environment.Is64BitOperatingSystem && hive==Microsoft.Win32.RegistryHive.LocalMachine)
                ? new[]{Microsoft.Win32.RegistryView.Registry64,Microsoft.Win32.RegistryView.Registry32}
                : new[]{Microsoft.Win32.RegistryView.Default};
            foreach(var view in views){
                try{
                    using(var root=Microsoft.Win32.RegistryKey.OpenBaseKey(hive,view))
                    using(var key=root.OpenSubKey(StartupEntry.RunPath)){
                        if(key==null)continue;
                        foreach(var name in key.GetValueNames()){
                            var entry=StartupEntry.Read(key,name,false);if(entry==null)continue;
                            var user=hive==Microsoft.Win32.RegistryHive.CurrentUser;
                            entries.Items.Add(new ListViewItem(new[]{name,user?"Usuário":"Sistema",entry.Command}){Tag=user?entry:null});
                        }
                    }
                }catch(UnauthorizedAccessException){skipped++;}catch(System.Security.SecurityException){skipped++;}
            }
        }
        try{
            using(var backup=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupEntry.BackupPath)){
                if(backup!=null)foreach(var name in backup.GetValueNames()){
                    var entry=StartupEntry.Read(backup,name,true);
                    if(entry!=null)entries.Items.Add(new ListViewItem(new[]{name,"Pausado",entry.Command}){Tag=entry});
                }
            }
        }catch(UnauthorizedAccessException){skipped++;}catch(System.Security.SecurityException){skipped++;}
        var userStartup=Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var pausedStartup=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AdvancedSystemCraft","PausedStartupFolder");
        foreach(var folder in new[]{Environment.SpecialFolder.Startup,Environment.SpecialFolder.CommonStartup}){
            var path=Environment.GetFolderPath(folder);
            if(!Directory.Exists(path))continue;
            try{foreach(var file in Directory.GetFiles(path)){
                if(!Path.GetFileName(file).Equals("desktop.ini",StringComparison.OrdinalIgnoreCase))
                    entries.Items.Add(new ListViewItem(new[]{Path.GetFileName(file),folder==Environment.SpecialFolder.Startup?"Pasta do usuário":"Pasta comum",file}){Tag=folder==Environment.SpecialFolder.Startup?StartupFolderEntry.Active(userStartup,file):null});
            }}catch(IOException){skipped++;}catch(UnauthorizedAccessException){skipped++;}
        }
        if(Directory.Exists(pausedStartup))try{foreach(var file in Directory.GetFiles(pausedStartup)){
            var entry=StartupFolderEntry.PausedEntry(userStartup,pausedStartup,file);
            if(entry!=null)entries.Items.Add(new ListViewItem(new[]{entry.Name,"Pasta pausada",file}){Tag=entry});
        }}catch(IOException){skipped++;}catch(UnauthorizedAccessException){skipped++;}
        p.Controls.Add(new Label{Text=String.Format("{0} entradas listadas (inclui pausadas) · {1} origens inacessíveis",entries.Items.Count,skipped),AutoSize=true});
        var row=new FlowLayoutPanel{AutoSize=true};p.Controls.Add(row);
        var toggle=ActionButton(row,"Pausar entrada",(sender,args)=>{
            if(entries.SelectedItems.Count!=1)return;
            var item=entries.SelectedItems[0].Tag;
            if(item==null)return;
            try{
                var value=item as StartupEntry;
                if(value!=null)value.Move(Microsoft.Win32.Registry.CurrentUser,StartupEntry.RunPath,StartupEntry.BackupPath);
                else ((StartupFolderEntry)item).Move();
                ShowStartup();
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}
        },165);
        toggle.Enabled=false;
        entries.SelectedIndexChanged+=(sender,args)=>{
            var item=entries.SelectedItems.Count==1?entries.SelectedItems[0].Tag:null;
            toggle.Enabled=item!=null;
            var paused=item is StartupEntry ? ((StartupEntry)item).Paused : item is StartupFolderEntry && ((StartupFolderEntry)item).Paused;
            toggle.Text=paused?"Restaurar entrada":"Pausar entrada";
        };
        ActionButton(row,"Atualizar lista",(sender,args)=>ShowStartup(),140);
        var manage=new Button{Size=new Size(280,40)};Style(manage,"Gerenciar nas configurações do Windows");
        manage.Click+=(s,e)=>{try{Process.Start("ms-settings:startupapps");}catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}};
        p.Controls.Add(manage);
    }
    void ShowPerformance(){
        var p=Page("Monitor de desempenho");
        AddResourceMonitor(p);
        foreach(var drive in DriveInfo.GetDrives().Where(d=>d.DriveType==DriveType.Fixed)){
            string text;
            try {var total=drive.TotalSize;var free=drive.AvailableFreeSpace;
                text=String.Format("{0}     {1:N1} GiB livres de {2:N1} GiB",drive.Name,free/1073741824d,total/1073741824d);}
            catch(IOException){text=drive.Name+"    Indisponível";}catch(UnauthorizedAccessException){text=drive.Name+"    Acesso restrito";}
            p.Controls.Add(new Label{Text=text,AutoSize=true,Padding=new Padding(8,14,0,8)});
        }
        var b=new Button{Size=new Size(152,40)};Style(b,"Atualizar");b.Click+=(s,e)=>ShowPerformance();p.Controls.Add(b);
    }
    void ShowTools(){
        var p=Page("Ferramentas");
        ActionButton(p,"Revisar registro do usuário",(s,e)=>ShowRegistryCare(),290);
        foreach(var tool in new[]{new[]{"Aplicativos de inicialização","ms-settings:startupapps"},new[]{"Monitor de recursos","resmon.exe"},new[]{"Gerenciador de tarefas","Taskmgr.exe"}}){
            var b=new Button{Size=new Size(290,48),Margin=new Padding(0,5,0,5)};Style(b,tool[0]);
            b.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo(tool[1].StartsWith("ms-settings:")?tool[1]:Path.Combine(Environment.SystemDirectory,tool[1])){UseShellExecute=true});}
                catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}};p.Controls.Add(b);
        }
    }
    [STAThread] static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new CraftWindow());}
}
