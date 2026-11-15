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
[assembly: AssemblyVersion("0.2.0.0")]

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
    readonly TextBox diagnostic=new TextBox();
    readonly ScanButton scan=new ScanButton();
    readonly ListView list=new ListView();
    readonly Label status=new Label();
    readonly Button fix=new Button();
    CancellationTokenSource operation;
    bool repairing;

    public CraftWindow() {
        Text="Advanced System Craft"; Font=new Font("Segoe UI",10); ForeColor=Color.White;
        BackColor=surface; MinimumSize=new Size(840,600); ClientSize=new Size(980,640);
        StartPosition=FormStartPosition.CenterScreen;
        menu.Dock=DockStyle.Left; menu.Width=192; menu.FlowDirection=FlowDirection.TopDown;
        menu.Padding=new Padding(12,24,8,0); menu.BackColor=Color.FromArgb(15,23,38);
        var brand=new Label {Text="Advanced\nSystem Craft", Size=new Size(163,88), Font=new Font(Font.FontFamily,16,FontStyle.Bold)};
        menu.Controls.Add(brand);
        AddNav("Cuidado", ShowCare); AddNav("Proteção", ShowProtection); AddNav("Software", ShowSoftware); AddNav("Inicialização", ShowStartup); AddNav("Desempenho", ShowPerformance); AddNav("Ferramentas", ShowTools);
        body.Dock=DockStyle.Fill; Controls.Add(body); Controls.Add(menu);
        care.Dock=DockStyle.Fill; care.Padding=new Padding(22);
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4};
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,54)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,215));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,62));
        layout.Controls.Add(Title("Cuidado  /  Modo manual"),0,0);
        var options=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};
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
        options.Controls.Add(checks); layout.Controls.Add(options,0,1);
        list.Dock=DockStyle.Fill; list.View=View.Details; list.CheckBoxes=true; list.FullRowSelect=true;
        list.BackColor=Color.FromArgb(28,42,59); list.ForeColor=Color.White; list.BorderStyle=BorderStyle.None;
        list.Columns.Add("Categoria",148); list.Columns.Add("Arquivo",385); list.Columns.Add("Tamanho",96);
        list.ShowItemToolTips=true;
        list.ItemChecked+=(s,e)=>BeginInvoke((Action)UpdateFix);
        var results=new Panel{Dock=DockStyle.Fill};
        diagnostic.Multiline=true;diagnostic.ReadOnly=true;diagnostic.ScrollBars=ScrollBars.Vertical;diagnostic.Dock=DockStyle.Bottom;
        diagnostic.Height=105;diagnostic.BackColor=surface;diagnostic.ForeColor=Color.White;diagnostic.Visible=false;
        list.Visible=false;results.Controls.Add(list);results.Controls.Add(diagnostic);layout.Controls.Add(results,0,2);
        var foot=new Panel {Dock=DockStyle.Fill,Padding=new Padding(0,8,0,4)};
        status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;status.Text="Selecione as categorias e clique em Verificar.";
        Style(fix,"Corrigir seleção");fix.Dock=DockStyle.Right;fix.Width=150;fix.Enabled=false;
        fix.Click+=async (s,e)=>await Repair();
        foot.Controls.Add(status);foot.Controls.Add(fix);layout.Controls.Add(foot,0,3);
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
    void UpdateFix(){fix.Enabled=operation==null && !repairing && list.CheckedItems.Count>0;}
    async Task RunScan() {
        if(operation!=null){operation.Cancel();return;}
        if(!junk.Checked && !shortcuts.Checked && !protection.Checked && !hardware.Checked && !privacy.Checked && !volumes.Checked){status.Text="Selecione ao menos uma categoria.";return;}
        bool files=junk.Checked,links=shortcuts.Checked, security=protection.Checked, disks=hardware.Checked, browsers=privacy.Checked, examineVolumes=volumes.Checked;
        operation=new CancellationTokenSource();var token=operation.Token;
        list.Visible=false; list.Items.Clear(); fix.Enabled=false; diagnostic.Visible=false; junk.Enabled=shortcuts.Enabled=hardware.Enabled=volumes.Enabled=privacy.Enabled=protection.Enabled=false; scan.Text="Parar"; status.Text="Verificando arquivos locais…";
        try {
            var timer=Stopwatch.StartNew(); var report=await Task.Run(()=>{var findings=Engine.Scan(Engine.Temp,Engine.Desktop,files,links,token);if(browsers)Engine.ScanBrowserCaches(findings,token);return findings;});
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
                var row=new ListViewItem(new[]{item.Category,item.Path,item.Size.ToString("N0")+" B"});
                row.Tag=item;row.ToolTipText=item.Target==null?item.Path:"Destino ausente: "+item.Target;
                list.Items.Add(row);
            }
            list.EndUpdate();list.Visible=report.Items.Count>0;status.Text=String.Format("{0} examinados   ·   {1} para revisar   ·   {2} inacessíveis   ·   {3} ms",report.Examined,report.Items.Count,report.Skipped,timer.ElapsedMilliseconds);
        } catch(OperationCanceledException){if(!IsDisposed)status.Text="Verificação cancelada.";}
          catch(Exception ex){if(!IsDisposed)status.Text=ex.Message;}
        finally {operation.Dispose();operation=null;if(!IsDisposed){junk.Enabled=shortcuts.Enabled=hardware.Enabled=volumes.Enabled=privacy.Enabled=protection.Enabled=true; scan.Text="Verificar";UpdateFix();}}
    }
    async Task Repair(){
        var selected=list.CheckedItems.Cast<ListViewItem>().ToArray();
        if(selected.Length==0)return;
        repairing=true;scan.Enabled=fix.Enabled=list.Enabled=false;int ok=0;
        try{
            foreach(var row in selected){
                try {var item=(Finding)row.Tag; await Task.Run(()=>Engine.Correct(item,Engine.Temp,Engine.Desktop));
                    list.Items.Remove(row);ok++;}
                catch(Exception ex){row.ToolTipText=ex.Message;row.SubItems[0].Text="Falha na correção";row.Checked=false;}
            }
            status.Text=String.Format("{0} enviados à Lixeira   ·   {1} falhas",ok,selected.Length-ok);
        }finally{repairing=false;scan.Enabled=list.Enabled=true;UpdateFix();}
    }
    FlowLayoutPanel Page(string name){
        ClearBody();var p=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(24)};
        var title=Title(name);title.Width=500;p.Controls.Add(title);body.Controls.Add(p);return p;
    }
    void ShowStartup(){
        var p=Page("Programas de inicialização");
        var entries=new ListView{View=View.Details,FullRowSelect=true,Size=new Size(565,270),BackColor=surface,ForeColor=Color.White};
        entries.Columns.Add("Programa",170);entries.Columns.Add("Origem",100);entries.Columns.Add("Comando ou arquivo",285);p.Controls.Add(entries);
        int skipped=0;
        foreach(var hive in new[]{Microsoft.Win32.RegistryHive.CurrentUser,Microsoft.Win32.RegistryHive.LocalMachine}){
            var views=(Environment.Is64BitOperatingSystem && hive==Microsoft.Win32.RegistryHive.LocalMachine)
                ? new[]{Microsoft.Win32.RegistryView.Registry64,Microsoft.Win32.RegistryView.Registry32}
                : new[]{Microsoft.Win32.RegistryView.Default};
            foreach(var view in views){
                try{
                    using(var root=Microsoft.Win32.RegistryKey.OpenBaseKey(hive,view))
                    using(var key=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){
                        if(key==null)continue;
                        foreach(var name in key.GetValueNames()){
                            var command=key.GetValue(name) as string;if(String.IsNullOrEmpty(command))continue;
                            entries.Items.Add(new ListViewItem(new[]{name,hive==Microsoft.Win32.RegistryHive.CurrentUser?"Usuário":"Sistema",command}));
                        }
                    }
                }catch(UnauthorizedAccessException){skipped++;}catch(System.Security.SecurityException){skipped++;}
            }
        }
        foreach(var folder in new[]{Environment.SpecialFolder.Startup,Environment.SpecialFolder.CommonStartup}){
            var path=Environment.GetFolderPath(folder);
            if(!Directory.Exists(path))continue;
            try{foreach(var file in Directory.GetFiles(path)){
                if(!Path.GetFileName(file).Equals("desktop.ini",StringComparison.OrdinalIgnoreCase))
                    entries.Items.Add(new ListViewItem(new[]{Path.GetFileName(file),"Pasta",file}));
            }}catch(IOException){skipped++;}catch(UnauthorizedAccessException){skipped++;}
        }
        p.Controls.Add(new Label{Text=String.Format("{0} entradas nas chaves Run e pastas de inicialização · {1} origens inacessíveis",entries.Items.Count,skipped),AutoSize=true});
        var manage=new Button{Size=new Size(280,40)};Style(manage,"Gerenciar nas configurações do Windows");
        manage.Click+=(s,e)=>{try{Process.Start("ms-settings:startupapps");}catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}};
        p.Controls.Add(manage);
    }
    void ShowPerformance(){
        var p=Page("Desempenho dos discos");
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
        var p=Page("Ferramentas do Windows");
        foreach(var tool in new[]{new[]{"Aplicativos de inicialização","ms-settings:startupapps"},new[]{"Monitor de recursos","resmon.exe"},new[]{"Gerenciador de tarefas","Taskmgr.exe"}}){
            var b=new Button{Size=new Size(290,48),Margin=new Padding(0,5,0,5)};Style(b,tool[0]);
            b.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo(tool[1].StartsWith("ms-settings:")?tool[1]:Path.Combine(Environment.SystemDirectory,tool[1])){UseShellExecute=true});}
                catch(Exception ex){MessageBox.Show(this,ex.Message,Text);}};p.Controls.Add(b);
        }
    }
    [STAThread] static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new CraftWindow());}
}
