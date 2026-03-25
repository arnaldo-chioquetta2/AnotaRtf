using AtcCtrl;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
//using System.Diagnostics;

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
            this.Text = $"AnoteitoRtf v{version.Major}.{version.Minor}";
            Logger.Write($"[v1.5.1] Título definido: {this.Text}");
            Logger.Write("[v1.5.1] Form1_Load concluído");
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

        //private void CreateTabContextMenu()
        //{
        //    ContextMenuStrip tabContextMenu = new ContextMenuStrip();
        //    ToolStripMenuItem deleteTabItem = new ToolStripMenuItem("Excluir Aba");
        //    deleteTabItem.Click += DeleteTabMenuItem_Click;
        //    tabContextMenu.Items.Add(deleteTabItem);

        //    tabContextMenu.Opening += (s, e) =>
        //    {
        //        Point cursorPos = tabControl.PointToClient(Cursor.Position);
        //        for (int i = 0; i < tabControl.TabCount; i++)
        //        {
        //            Rectangle tabRect = tabControl.GetTabRect(i);
        //            if (tabRect.Contains(cursorPos))
        //            {
        //                TabPage tabUnderCursor = tabControl.TabPages[i];
        //                contextMenuTab = (tabUnderCursor != placeholderTab) ? tabUnderCursor : null;
        //                Logger.Write($"[v1.5.0] Aba detectada sob cursor: '{(contextMenuTab != null ? contextMenuTab.Text : "null")}'");
        //                return;
        //            }
        //        }
        //        contextMenuTab = null;
        //    };

        //    tabControl.ContextMenuStrip = tabContextMenu;
        //}

        #endregion

        #region Ações das Abas

        //private void DeleteTabMenuItem_Click(object sender, EventArgs e)
        //{
        //    Logger.Write($"[v1.5.0] DeleteTabMenuItem_Click acionado");

        //    if (contextMenuTab == null || contextMenuTab == placeholderTab)
        //    {
        //        MessageBox.Show("Clique com o botão direito diretamente sobre uma aba para excluí-la.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        return;
        //    }

        //    if (MessageBox.Show($"Excluir a aba '{contextMenuTab.Text}' permanentemente?", "Confirmação",
        //        MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        //    {
        //        Logger.Write($"[v1.5.0] Excluindo aba: '{contextMenuTab.Text}'");
        //        DeleteTab(contextMenuTab);
        //    }
        //}

        //private void DeleteTab(TabPage tab)
        //{
        //    if (tab == null || tab == placeholderTab) return;

        //    ATCRTF editor = tab.Controls.OfType<ATCRTF>().FirstOrDefault();
        //    if (editor != null)
        //    {
        //        editor.SalvaRTF();
        //        try { if (File.Exists(editor.caminhoDoArquivo)) File.Delete(editor.caminhoDoArquivo); }
        //        catch (Exception ex) { Logger.WriteException(ex, "Delete arquivo"); }
        //    }

        //    tabControl.TabPages.Remove(tab);
        //    SaveTabs();
        //    Logger.Write($"[v1.5.0] Aba excluída: '{tab.Text}'");
        //}

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
            
        //private void TabControl_MouseDoubleClick(object sender, MouseEventArgs e)
        //{
        //    for (int i = 0; i < tabControl.TabCount; i++)
        //    {
        //        if (tabControl.GetTabRect(i).Contains(e.Location) && tabControl.TabPages[i] != placeholderTab)
        //        {
        //            string current = tabControl.TabPages[i].Text;
        //            string newName = PromptForTabName(current);
        //            if (!string.IsNullOrEmpty(newName) && newName != current)
        //            {
        //                tabControl.TabPages[i].Text = newName;
        //                SaveTabs();
        //                Logger.Write($"[v1.5.0] Aba renomeada: '{current}' → '{newName}'");
        //            }
        //            break;
        //        }
        //    }
        //}

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
            if (tabControl.TabPages.Count == 1) return;

            if (tabControl.SelectedTab == placeholderTab)
            {
                int fileIndex = nextFileIndex++;
                int visualCount = tabControl.TabPages.Count - 1;
                string[] numbers = { "Um", "Dois", "Três", "Quatro", "Cinco", "Seis", "Sete", "Oito", "Nove", "Dez" };
                string displayName = visualCount < numbers.Length ? numbers[visualCount] : visualCount.ToString();

                CreateTab(fileIndex, displayName);
                tabControl.SelectedTab = tabControl.TabPages[tabControl.TabPages.Count - 2];
                SaveTabs();

                Logger.Write($"[v1.5.0] Nova aba criada: '{displayName}' com fileIndex={fileIndex}");
            }
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

    }
}