using AtcCtrl;
using System;
using System.IO;
using System.Linq;
using System.Drawing;
using Microsoft.Win32;
using System.Reflection;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace AnotaRtf
{
    public partial class Form1 : Form
    {

        #region Inicialização        

        private TabPage placeholderTab;
        private TabPage contextMenuTab;
        private int nextFileIndex = 1;
        private bool firstShown = true;
        private const string REGISTRY_KEY = @"AnoteitorRTF\MyApp";
        private const string TABS_SUBKEY = @"AnoteitorRTF\MyApp\Tabs";
        private readonly string LOG_TAB_NAME="log";

        private readonly Dictionary<TabPage, EstadoVisualAba> _estadoVisualPorAba = new();

        #region ApiWindows

        private const int WM_USER = 0x0400;
        private const int EM_GETSEL = 0x00B0;
        private const int EM_SETSEL = 0x00B1;
        private const int EM_GETSCROLLPOS = WM_USER + 221;
        private const int EM_SETSCROLLPOS = WM_USER + 222;

        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int msg,
            IntPtr wParam,
            ref Point lParam
        );

        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int msg,
            ref int wParam,
            ref int lParam
        );

        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam
        );

        #endregion

        public Form1()
        {
            InitializeComponent();
            CreateLogTab();
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            //tabControl.MouseDoubleClick += TabControl_MouseDoubleClick;
            tabControl.MouseDown += TabControl_MouseDown;
            this.Shown += Form1_Shown;
            this.Resize += Form1_Resize;
            //CreateTabContextMenu();
        }

        private void CreateLogTab()
        {
            Logger.Write("[LOG] >>> CreateLogTab EXECUTOU");

            TabPage tab = new TabPage(LOG_TAB_NAME)
            {
                Name = "tabLog"
            };

            RichTextBox rtf = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White
            };

            tab.Controls.Add(rtf);

            tabControl.TabPages.Insert(0, tab);

            Logger.Write($"[LOG] Aba Log inserida. Total abas: {tabControl.TabPages.Count}");
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            try
            {
                string testPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "teste_escrita.txt");
                File.WriteAllText(testPath, "Teste de escrita funcionou!");
                Logger.Write($"[TESTE] Arquivo criado: {testPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            Logger.Write("[v1.5.1] Form1_Load iniciado");

            // 🔑 DIAGNÓSTICO: Log das informações do sistema
            Logger.Write($"[v1.5.1] Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");

            var allRtf = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "anotacao*.rtf");
            Logger.Write($"[v1.5.1] Arquivos RTF encontrados: {allRtf.Length}");
            foreach (var file in allRtf)
            {
                Logger.Write($"[v1.5.1]   → {Path.GetFileName(file)}");
            }

            LoadWindowPosition();
            SetupPlaceholder();
            LoadTabs();
            RestoreActiveTab();

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            this.Text = $"AnoteitoRtf v{version.Major}.{version.Minor}.{version.Build}";
            // this.Text = $"AnoteitoRtf v{version.Major}.{version.Minor}";

            Logger.Write($"Título definido: {this.Text}");
        }

        private void SetupPlaceholder()
        {
            placeholderTab = tb2;
            placeholderTab.Text = "+";
            if (tabControl.TabPages.Contains(tb1))
                tabControl.TabPages.Remove(tb1);
        }

        private void LoadWindowPosition()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY))
                {
                    if (key != null)
                    {
                        int x = (int)(key.GetValue("WindowPositionX") ?? this.Left);
                        int y = (int)(key.GetValue("WindowPositionY") ?? this.Top);
                        int width = (int)(key.GetValue("WindowWidth") ?? this.Width);
                        int height = (int)(key.GetValue("WindowHeight") ?? this.Height);

                        Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
                        bool isValid = x >= 0 && y >= 0 && x < screenBounds.Right && y < screenBounds.Bottom && width > 0 && height > 0;

                        if (isValid)
                        {
                            this.StartPosition = FormStartPosition.Manual;
                            this.Left = x;
                            this.Top = y;
                            this.Width = width;
                            this.Height = height;
                        }
                        else
                        {
                            Logger.Write("[v1.5.0] Coordenadas inválidas — centralizando");
                            this.StartPosition = FormStartPosition.CenterScreen;
                        }
                    }
                }
            }
            catch { }
        }

        private void LoadTabs()
        {
            Logger.Write("[v1.5.0] LoadTabs iniciado");
            tabControl.TabPages.Clear();
            tabControl.TabPages.Add(placeholderTab);
            nextFileIndex = 1;

            try
            {
                using (RegistryKey tabsKey = Registry.CurrentUser.OpenSubKey(TABS_SUBKEY))
                {
                    if (tabsKey != null)
                    {
                        var tabNames = tabsKey.GetSubKeyNames()
                            .Where(name => name.StartsWith("tab"))
                            .OrderBy(name => name)
                            .ToArray();

                        Logger.Write($"[v1.5.0] Encontradas {tabNames.Length} abas no Registro");

                        foreach (string tabName in tabNames)
                        {
                            using (RegistryKey tabKey = tabsKey.OpenSubKey(tabName))
                            {
                                if (tabKey != null)
                                {
                                    string displayName = (string)tabKey.GetValue("DisplayName", "");
                                    int fileIndex = (int)tabKey.GetValue("FileIndex", 0);

                                    if (fileIndex > 0 && !string.IsNullOrEmpty(displayName))
                                    {
                                        CreateTab(fileIndex, displayName);
                                        if (fileIndex >= nextFileIndex)
                                            nextFileIndex = fileIndex + 1;
                                        Logger.Write($"[v1.5.0] ✓ Aba '{displayName}' carregada (anotacao{fileIndex}.rtf)");
                                    }
                                }
                            }
                        }
                    }
                }

                if (tabControl.TabPages.Count == 1)
                {
                    Logger.Write("[v1.5.0] Nenhuma aba encontrada - criando primeira aba");
                    CreateTab(1, "Um");
                    nextFileIndex = 2;
                }

                Logger.Write($"[v1.5.0] nextFileIndex definido para: {nextFileIndex}");
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex, "LoadTabs");
                CreateTab(1, "Um");
                nextFileIndex = 2;
            }
        }

        private void CreateTab(int fileIndex, string displayName)
        {
            Logger.Write($"[v1.5.0] CreateTab(fileIndex={fileIndex}, displayName='{displayName}')");

            TabPage tab = new TabPage(displayName) { Name = $"tab{fileIndex}" };

            ATCRTF editor = new ATCRTF
            {
                Dock = DockStyle.Fill,
                caminhoDoArquivo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"anotacao{fileIndex}.rtf"),
                Criptografia = false
            };

            editor.PerformLayout();
            editor.Carrega();
            tab.Controls.Add(editor);
            tabControl.TabPages.Insert(tabControl.TabPages.Count - 1, tab);

            Logger.Write($"[v1.5.0] ✅ Aba criada: '{displayName}' | Controles internos: {editor.Controls.Count}");
        }        

        #endregion

        #region Ações das Abas        

        private void TabControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                for (int i = 0; i < tabControl.TabCount; i++)
                {
                    Rectangle tabRect = tabControl.GetTabRect(i);
                    if (tabRect.Contains(e.Location))
                    {
                        TabPage clickedTab = tabControl.TabPages[i];

                        if (clickedTab != placeholderTab)
                            tabControl.SelectedIndex = i;

                        contextMenuTab = clickedTab;

                        Logger.Write($"[v1.5.0] Botão direito na aba: '{clickedTab.Text}' (índice {i})");

                        // 👇 NOVO
                        if (clickedTab != placeholderTab)
                            ShowTabContextMenu(i, e.Location);

                        break;
                    }
                }
            }
        }

        private void ShowTabContextMenu(int index, Point location)
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            int lastIndex = tabControl.TabPages.Count - 2; // ignora "+"

            var moverEsquerda = new ToolStripMenuItem("Mover para Esquerda");
            var moverDireita = new ToolStripMenuItem("Mover para Direita");
            var renomear = new ToolStripMenuItem("Renomear Aba");
            var excluir = new ToolStripMenuItem("Excluir Aba");

            moverEsquerda.Enabled = index > 0;
            moverDireita.Enabled = index < lastIndex;

            moverEsquerda.Click += (s, e) => MoveTab(index, index - 1);
            moverDireita.Click += (s, e) => MoveTab(index, index + 1);

            // usa sua aba já capturada
            //renomear.Click += (s, e) => RenomearAba(contextMenuTab);
            //excluir.Click += (s, e) => ExcluirAba(contextMenuTab);
            renomear.Click += (s, e) => RenomearAba(contextMenuTab);
            excluir.Click += (s, e) => ExcluirAba(contextMenuTab);

            menu.Items.Add(moverEsquerda);
            menu.Items.Add(moverDireita);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(renomear);
            menu.Items.Add(excluir);

            menu.Show(tabControl, location);
        }

        private void ExcluirAba(TabPage tab)
        {
            if (tab == null || tab == placeholderTab) return;

            ATCRTF editor = tab.Controls.OfType<ATCRTF>().FirstOrDefault();
            if (editor != null)
            {
                editor.SalvaRTF();
                try { if (File.Exists(editor.caminhoDoArquivo)) File.Delete(editor.caminhoDoArquivo); }
                catch (Exception ex) { Logger.WriteException(ex, "Delete arquivo"); }
            }

            tabControl.TabPages.Remove(tab);
            SaveTabs();
            Logger.Write($"[v1.5.0] Aba excluída: '{tab.Text}'");

        }

        private void RenomearAba(TabPage tab)
        {
            if (tab == null || tab == placeholderTab)
                return;

            string current = tab.Text;
            string newName = PromptForTabName(current);

            if (!string.IsNullOrEmpty(newName) && newName != current)
            {
                tab.Text = newName;
                SaveTabs();

                Logger.Write($"[v1.5.0] Aba renomeada: '{current}' → '{newName}'");
            }

        }

        private void MoveTab(int fromIndex, int toIndex)
        {
            if (toIndex < 0 || toIndex >= tabControl.TabPages.Count - 1)
                return;

            var tab = tabControl.TabPages[fromIndex];

            tabControl.TabPages.RemoveAt(fromIndex);
            tabControl.TabPages.Insert(toIndex, tab);
            tabControl.SelectedIndex = toIndex;

            Logger.Write($"[v1.5.x] Aba '{tab.Text}' movida para posição {toIndex}");

            SaveTabs(); // se já existir
        }                   

        private string PromptForTabName(string current)
        {
            using (Form prompt = new Form { Text = "Renomear Aba", StartPosition = FormStartPosition.CenterScreen, Width = 320, Height = 160, MaximizeBox = false, MinimizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog })
            using (Label lbl = new Label { Text = "Nome da aba:", AutoSize = true, Location = new Point(20, 25) })
            using (System.Windows.Forms.TextBox input = new System.Windows.Forms.TextBox { Location = new Point(20, 50), Size = new Size(260, 25), Text = current })
            {
                System.Windows.Forms.Button btnOk = new System.Windows.Forms.Button { Text = "OK", Size = new Size(80, 30), DialogResult = DialogResult.OK };
                System.Windows.Forms.Button btnCancel = new System.Windows.Forms.Button { Text = "Cancelar", Size = new Size(80, 30), DialogResult = DialogResult.Cancel };

                int totalButtonsWidth = btnOk.Width + btnCancel.Width + 10;
                int startX = (prompt.ClientSize.Width - totalButtonsWidth) / 2;
                btnOk.Location = new Point(startX, 90);
                btnCancel.Location = new Point(startX + btnOk.Width + 10, 90);

                prompt.AcceptButton = btnOk;
                prompt.CancelButton = btnCancel;
                prompt.Controls.Add(lbl);
                prompt.Controls.Add(input);
                prompt.Controls.Add(btnOk);
                prompt.Controls.Add(btnCancel);
                prompt.Load += (s, e) => input.Focus();

                return prompt.ShowDialog() == DialogResult.OK ? input.Text.Trim() : null;
            }
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl.TabPages.Count == 1)
                return;

            if (tabControl.SelectedTab == placeholderTab)
            {
                int fileIndex = nextFileIndex++;

                int visualCount = tabControl.TabPages.Count - 1;

                string[] numbers =
                {
                    "Um", "Dois", "Três", "Quatro", "Cinco",
                    "Seis", "Sete", "Oito", "Nove", "Dez"
                };

                string displayName = visualCount < numbers.Length
                    ? numbers[visualCount]
                    : visualCount.ToString();

                CreateTab(fileIndex, displayName);

                tabControl.SelectedTab = tabControl.TabPages[tabControl.TabPages.Count - 2];

                SaveTabs();

                Logger.Write($"[v1.5.0] Nova aba criada: '{displayName}' com fileIndex={fileIndex}");

                return;
            }

            RestaurarEstadoVisualAba(tabControl.SelectedTab);
        }

        private void SaveTabs()
        {
            try
            {
                using (RegistryKey parentKey = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY, true))
                using (RegistryKey tabsKey = parentKey.CreateSubKey("Tabs", true))
                {
                    foreach (string name in tabsKey.GetSubKeyNames().ToArray())
                        tabsKey.DeleteSubKey(name);

                    int index = 1;
                    foreach (TabPage tab in tabControl.TabPages)
                    {
                        if (tab != placeholderTab)
                        {
                            ATCRTF editor = tab.Controls.OfType<ATCRTF>().FirstOrDefault();
                            if (editor != null && !string.IsNullOrEmpty(editor.caminhoDoArquivo))
                            {
                                string fileName = Path.GetFileNameWithoutExtension(editor.caminhoDoArquivo);
                                if (int.TryParse(fileName.Replace("anotacao", ""), out int fileIndex))
                                {
                                    using (RegistryKey tabKey = tabsKey.CreateSubKey($"tab{index}"))
                                    {
                                        tabKey.SetValue("DisplayName", tab.Text);
                                        tabKey.SetValue("FileIndex", fileIndex);
                                    }
                                    Logger.Write($"[v1.5.0] Salva: '{tab.Text}' → anotacao{fileIndex}.rtf");
                                    index++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex, "SaveTabs");
            }
        }

        private void RestoreActiveTab()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("ActiveTabIndex");
                        if (value is int savedIndex && savedIndex >= 0 && savedIndex < tabControl.TabPages.Count)
                        {
                            if (tabControl.TabPages[savedIndex] != placeholderTab)
                            {
                                tabControl.SelectedIndex = savedIndex;
                                Logger.Write($"[v1.5.0] Aba restaurada: índice {savedIndex} ('{tabControl.TabPages[savedIndex].Text}')");
                                return;
                            }
                        }
                    }
                }
            }
            catch { }

            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                if (tabControl.TabPages[i] != placeholderTab)
                {
                    tabControl.SelectedIndex = i;
                    Logger.Write($"[v1.5.0] Fallback: selecionada primeira aba (índice {i})");
                    break;
                }
            }
        }

        #endregion

        #region Form

        private void Form1_Shown(object sender, EventArgs e)
        {
            Logger.Write($"[v1.5.0] Form1_Shown | firstShown={firstShown}, WindowState={this.WindowState}");

            if (firstShown)
            {
                firstShown = false;
                if (this.WindowState != FormWindowState.Minimized)
                {
                    this.WindowState = FormWindowState.Normal;
                    this.BringToFront();
                    this.Activate();

                    Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
                    if (this.Left < 0 || this.Top < 0 || this.Right > screenBounds.Right || this.Bottom > screenBounds.Bottom)
                    {
                        Logger.Write("[v1.5.0] Janela fora da tela — centralizando");
                        this.StartPosition = FormStartPosition.CenterScreen;
                        this.WindowState = FormWindowState.Normal;
                    }
                }
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            Logger.Write($"[v1.5.0] Resize | WindowState={this.WindowState}");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Logger.Write("[v1.5.0] Form1_FormClosing iniciado");

            foreach (TabPage tab in tabControl.TabPages)
            {
                if (tab != placeholderTab)
                {
                    tab.Controls.OfType<ATCRTF>().FirstOrDefault()?.SalvaRTF();
                }
            }

            SaveTabs();

            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY, true))
                {
                    key.SetValue("WindowPositionX", this.Left);
                    key.SetValue("WindowPositionY", this.Top);
                    key.SetValue("WindowWidth", this.Width);
                    key.SetValue("WindowHeight", this.Height);

                    int activeTabIndex = tabControl.SelectedIndex;
                    if (activeTabIndex >= 0 && activeTabIndex < tabControl.TabPages.Count && tabControl.TabPages[activeTabIndex] != placeholderTab)
                        key.SetValue("ActiveTabIndex", activeTabIndex);
                }
            }
            catch (Exception ex) { Logger.WriteException(ex, "Salvar configurações"); }

            Logger.Write("[v1.5.0] Aplicativo encerrado");
        }

        #endregion

        #region GuardarEstadosDasAbas

        private void SalvarEstadoVisualAba(TabPage aba)
        {
            Control editor = ObterControleEditorDaAba(aba);

            if (editor == null || editor.IsDisposed)
                return;

            Point scrollPos = Point.Empty;
            SendMessage(editor.Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref scrollPos);

            ObterSelecao(editor, out int selectionStart, out int selectionLength);

            _estadoVisualPorAba[aba] = new EstadoVisualAba
            {
                SelectionStart = selectionStart,
                SelectionLength = selectionLength,
                ScrollPosition = scrollPos
            };
        }


        private void ObterSelecao(Control editor, out int selectionStart, out int selectionLength)
        {
            selectionStart = 0;
            selectionLength = 0;

            if (editor == null || editor.IsDisposed)
                return;

            PropertyInfo propStart = editor.GetType().GetProperty("SelectionStart");
            PropertyInfo propLength = editor.GetType().GetProperty("SelectionLength");

            if (propStart != null && propLength != null)
            {
                object valorStart = propStart.GetValue(editor);
                object valorLength = propLength.GetValue(editor);

                if (valorStart is int start && valorLength is int length)
                {
                    selectionStart = start;
                    selectionLength = length;
                    return;
                }
            }

            int inicio = 0;
            int fim = 0;

            SendMessage(editor.Handle, EM_GETSEL, ref inicio, ref fim);

            selectionStart = inicio;
            selectionLength = Math.Max(0, fim - inicio);
        }

        private void RestaurarEstadoVisualAba(TabPage aba)
        {
            if (aba == null)
                return;

            if (!_estadoVisualPorAba.TryGetValue(aba, out EstadoVisualAba estado))
                return;

            Control editor = ObterControleEditorDaAba(aba);

            if (editor == null || editor.IsDisposed)
                return;

            BeginInvoke(new Action(() =>
            {
                if (editor.IsDisposed)
                    return;

                DefinirSelecao(editor, estado.SelectionStart, estado.SelectionLength);

                Point scrollPos = estado.ScrollPosition;
                SendMessage(editor.Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref scrollPos);

                editor.Focus();
            }));
        }

        private Control ObterControleEditorDaAba(TabPage aba)
        {
            if (aba == null)
                return null;

            // Primeiro tenta encontrar um RichTextBox interno.
            // Se o ATCRTF usa RichTextBox por dentro e ele estiver exposto na árvore de controles,
            // este é o melhor alvo para seleção e scroll.
            RichTextBox richTextBox = ProcurarControle<RichTextBox>(aba);

            if (richTextBox != null)
                return richTextBox;

            // Se não encontrar RichTextBox, usa o próprio componente ATCRTF.
            AtcCtrl.ATCRTF atcRtf = ProcurarControle<AtcCtrl.ATCRTF>(aba);

            if (atcRtf != null)
                return atcRtf;

            return null;
        }
        private void DefinirSelecao(Control editor, int selectionStart, int selectionLength)
        {
            if (editor == null || editor.IsDisposed)
                return;

            selectionStart = Math.Max(0, selectionStart);
            selectionLength = Math.Max(0, selectionLength);

            PropertyInfo propTextLength = editor.GetType().GetProperty("TextLength");

            if (propTextLength != null)
            {
                object valorTextLength = propTextLength.GetValue(editor);

                if (valorTextLength is int textLength)
                {
                    selectionStart = Math.Min(selectionStart, textLength);
                    selectionLength = Math.Min(selectionLength, textLength - selectionStart);
                }
            }

            PropertyInfo propStart = editor.GetType().GetProperty("SelectionStart");
            PropertyInfo propLength = editor.GetType().GetProperty("SelectionLength");

            if (propStart != null && propLength != null && propStart.CanWrite && propLength.CanWrite)
            {
                propStart.SetValue(editor, selectionStart);
                propLength.SetValue(editor, selectionLength);
                return;
            }

            int selectionEnd = selectionStart + selectionLength;

            SendMessage(
                editor.Handle,
                EM_SETSEL,
                new IntPtr(selectionStart),
                new IntPtr(selectionEnd)
            );
        }

        private T ProcurarControle<T>(Control controlePai) where T : Control
        {
            if (controlePai == null)
                return null;

            foreach (Control controle in controlePai.Controls)
            {
                if (controle is T encontrado)
                    return encontrado;

                T encontradoFilho = ProcurarControle<T>(controle);

                if (encontradoFilho != null)
                    return encontradoFilho;
            }

            return null;
        }

        private void TabControl_Deselecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage == null)
                return;

            if (e.TabPage == placeholderTab)
                return;

            SalvarEstadoVisualAba(e.TabPage);
        }


        #endregion

    }

    public class EstadoVisualAba
    {
        public int SelectionStart { get; set; }
        public int SelectionLength { get; set; }
        public Point ScrollPosition { get; set; }
    }

}