using System;
using AtcCtrl;
using System.IO;
using System.Linq;
using System.Drawing;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Diagnostics;

namespace AnotaRtf
{
    public partial class Form1 : Form
    {
        private TabPage placeholderTab;
        private Timer ensureVisibleTimer;
        private const string REGISTRY_KEY = @"AnoteitorRTF\MyApp";
        private const string TABS_SUBKEY = @"AnoteitorRTF\MyApp\Tabs";
        private bool firstShown = true;

        public Form1()
        {
            InitializeComponent();
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            tabControl.MouseDoubleClick += TabControl_MouseDoubleClick;
            this.KeyDown += Form1_KeyDown;
            this.KeyPreview = true;

            // Extrai apenas a parte numérica da versão (remove tudo após o primeiro '+')
            string versionStr = Application.ProductVersion.Split('+')[0];

            // Garante que tenha pelo menos major.minor
            string[] parts = versionStr.Split('.');

            switch (parts.Length)
            {
                case 1:
                    this.Text = $"AnoteitoRtf v{parts[0]}";
                    break;
                case 2:
                    this.Text = $"AnoteitoRtf v{parts[0]}.{parts[1]}";
                    break;
                case 3:
                    this.Text = $"AnoteitoRtf v{parts[0]}.{parts[1]}.{parts[2]}";
                    break;
                default:
                    this.Text = "AnoteitoRtf"; // Fallback seguro
                    break;
            }

        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            Debug.WriteLine($"[v1.4.2] Form1_Shown | firstShown={firstShown}, WindowState={this.WindowState}");

            if (firstShown)
            {
                firstShown = false;

                // Só restaura se NÃO estiver minimizada (respeita intenção do usuário)
                if (this.WindowState != FormWindowState.Minimized)
                {
                    this.WindowState = FormWindowState.Normal;
                    this.BringToFront();
                    this.Activate();

                    // Valida posição apenas na primeira exibição
                    Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
                    if (this.Left < 0 || this.Top < 0 ||
                        this.Right > screenBounds.Right ||
                        this.Bottom > screenBounds.Bottom)
                    {
                        Debug.WriteLine("[v1.4.2] Janela fora da tela — centralizando");
                        this.StartPosition = FormStartPosition.CenterScreen;
                        this.WindowState = FormWindowState.Normal;
                    }
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Debug.WriteLine("[v1.3.9] Form1_Load iniciado");

            // 1. Carrega posição do Registro
            LoadWindowPosition();

            // 2. Garante que a janela esteja visível e dentro da área de trabalho            

            // 3. Configura placeholder e carrega abas
            SetupPlaceholder();
            LoadTabs();

            // 4. Atualiza título com versão
            // var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            // this.Text = $"AnoteitoRtf v{version.Major}.{version.Minor}";
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            EnsureWindowVisible();
        }

        private void EnsureWindowVisible()
        {
            // Força a janela para o estado normal (não minimizada)
            this.WindowState = FormWindowState.Normal;

            // Garante que a janela esteja no topo
            this.BringToFront();
            //this.Activate();

            // Se a janela ainda não estiver visível após 100ms, tenta novamente
            //if (!IsWindowVisible())
            //{
                Debug.WriteLine("[v1.4.0] Janela não visível — agendando correção com timer");

                ensureVisibleTimer = new Timer
                {
                    Interval = 100, // 0.1 segundo
                    Enabled = true
                };

                ensureVisibleTimer.Tick += (s, e) =>
                {
                    Debug.WriteLine("[v1.4.0] Timer acionado — forçando visibilidade");
                    this.WindowState = FormWindowState.Normal;
                    this.BringToFront();
                    this.Activate();
                    ensureVisibleTimer.Stop();
                };
            //}
        }

        private bool IsWindowVisible()
        {
            return this.WindowState != FormWindowState.Minimized &&
                   this.IsHandleCreated &&
                   !this.IsDisposed;
        }
        private void SetupPlaceholder()
        {
            // Mantém tb2 como placeholder "+", mas remove tb1 (será recriada dinamicamente)
            placeholderTab = tb2;
            placeholderTab.Text = "+";

            // Remove tb1 para recriá-la como aba dinâmica
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

                        // Valida se as coordenadas estão dentro da área de trabalho
                        Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
                        bool isValid = x >= 0 && y >= 0 &&
                                       x < screenBounds.Right &&
                                       y < screenBounds.Bottom &&
                                       width > 0 && height > 0;

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
                            Debug.WriteLine("[v1.4.1] Coordenadas inválidas — resetando para centro");
                            this.StartPosition = FormStartPosition.CenterScreen;
                        }
                    }
                }
            }
            catch { }
        }

        private void LoadTabs()
        {
            Debug.WriteLine("[v1.3.5] Carregando abas...");

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

                        Debug.WriteLine($"[v1.3.5] Encontradas {tabNames.Length} abas no Registro");

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
                                        Debug.WriteLine($"[v1.3.5] ✓ Aba '{displayName}' carregada (anotacao{fileIndex}.rtf)");
                                    }
                                }
                            }
                        }
                    }
                }

                // Se nenhuma aba foi carregada, cria a primeira
                if (tabControl.TabPages.Count == 1) // Apenas o placeholder "+"
                {
                    Debug.WriteLine("[v1.3.5] Nenhuma aba encontrada - criando primeira aba");
                    CreateTab(1, "Um");
                }

                // 🔑 FORÇA SELEÇÃO DA PRIMEIRA ABA (evita selecionar o placeholder "+")
                if (tabControl.TabPages.Count > 1)
                {
                    tabControl.SelectedTab = tabControl.TabPages[0];
                    Debug.WriteLine("[v1.3.5] Forçada seleção da primeira aba");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[v1.3.5] ERRO ao carregar abas: {ex.Message}");
                CreateTab(1, "Um");
            }
        }

        private void CreateTab(int fileIndex, string displayName)
        {
            // Cria nova TabPage
            TabPage tab = new TabPage(displayName)
            {
                Name = $"tab{fileIndex}"
            };

            // Cria o componente ATCRTF DINAMICAMENTE (igual ao Designer)
            ATCRTF editor = new ATCRTF
            {
                Dock = DockStyle.Fill,
                caminhoDoArquivo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"anotacao{fileIndex}.rtf"),
                Criptografia = false
            };

            // ⚠️ CRÍTICO: Força inicialização completa do UserControl
            editor.PerformLayout(); // Garante que controles internos sejam renderizados

            // Carrega conteúdo do arquivo RTF
            editor.Carrega();

            // Adiciona o editor à aba
            tab.Controls.Add(editor);

            // Insere ANTES do placeholder "+"
            tabControl.TabPages.Insert(tabControl.TabPages.Count - 1, tab);

            Debug.WriteLine($"[v1.3.3] ✅ Aba criada: '{displayName}' | Controles internos: {editor.Controls.Count}");
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Evita disparar evento durante carregamento inicial
            if (tabControl.TabPages.Count == 1) return;

            // Só cria nova aba se o placeholder "+" estiver selecionado
            if (tabControl.SelectedTab == placeholderTab)
            {
                int realTabCount = tabControl.TabPages.Count - 1; // Exclui "+"
                string[] numbers = { "Um", "Dois", "Três", "Quatro", "Cinco", "Seis", "Sete", "Oito", "Nove", "Dez" };
                string numberName = realTabCount < numbers.Length
                    ? numbers[realTabCount]
                    : (realTabCount + 1).ToString();

                CreateTab(realTabCount + 1, numberName);
                tabControl.SelectedTab = tabControl.TabPages[tabControl.TabPages.Count - 2]; // Seleciona a nova aba
                SaveTabs();
                Debug.WriteLine($"[v1.3.5] Nova aba criada: '{numberName}'");
            }
        }

        private void SaveTabs()
        {
            try
            {
                using (RegistryKey parentKey = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY, true))
                using (RegistryKey tabsKey = parentKey.CreateSubKey("Tabs", true))
                {
                    // Limpa subchaves antigas
                    foreach (string name in tabsKey.GetSubKeyNames().ToArray())
                        tabsKey.DeleteSubKey(name);

                    // Salva abas reais (excluindo "+")
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
                                    Debug.WriteLine($"[v1.3.3] Salva: '{tab.Text}' -> anotacao{fileIndex}.rtf");
                                    index++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[v1.3.3] ERRO ao salvar: {ex.Message}");
            }
        }

        private void TabControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < tabControl.TabCount; i++)
            {
                if (tabControl.GetTabRect(i).Contains(e.Location) && tabControl.TabPages[i] != placeholderTab)
                {
                    string current = tabControl.TabPages[i].Text;
                    string newName = PromptForTabName(current);
                    if (!string.IsNullOrEmpty(newName) && newName != current)
                    {
                        tabControl.TabPages[i].Text = newName;
                        SaveTabs();
                        Debug.WriteLine($"[v1.3.3] Renomeada: '{current}' -> '{newName}'");
                    }
                    break;
                }
            }
        }

        private string PromptForTabName(string current)
        {
            using (Form prompt = new Form
            {
                Text = "Renomear Aba",
                StartPosition = FormStartPosition.CenterScreen,
                Width = 320,
                Height = 160,
                MaximizeBox = false,
                MinimizeBox = false,
                FormBorderStyle = FormBorderStyle.FixedDialog
            })
            using (Label lbl = new Label { Text = "Nome da aba:", AutoSize = true, Location = new Point(20, 25) })
            using (TextBox input = new TextBox
            {
                Location = new Point(20, 50),
                Size = new Size(260, 25),
                Text = current
            })
            {
                Button btnOk = new Button
                {
                    Text = "OK",
                    Size = new Size(80, 30),
                    DialogResult = DialogResult.OK
                };

                Button btnCancel = new Button
                {
                    Text = "Cancelar",
                    Size = new Size(80, 30),
                    DialogResult = DialogResult.Cancel
                };

                // 🔑 Define botões de aceitação/cancelamento (Enter/Esc)
                prompt.AcceptButton = btnOk;
                prompt.CancelButton = btnCancel;

                // Centraliza os botões horizontalmente
                int totalButtonsWidth = btnOk.Width + btnCancel.Width + 10;
                int startX = (prompt.ClientSize.Width - totalButtonsWidth) / 2;
                btnOk.Location = new Point(startX, 90);
                btnCancel.Location = new Point(startX + btnOk.Width + 10, 90);

                prompt.Controls.Add(lbl);
                prompt.Controls.Add(input);
                prompt.Controls.Add(btnOk);
                prompt.Controls.Add(btnCancel);

                // Define foco no TextBox ao abrir
                prompt.Load += (s, e) => input.Focus();

                return prompt.ShowDialog() == DialogResult.OK ? input.Text.Trim() : null;
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            // Só permite exclusão de aba se:
            // 1. A tecla pressionada é Delete
            // 2. O TabControl tem foco (não o editor RTF nem outro controle)
            // 3. Nenhuma aba está sendo editada (TextBox de renomeação não existe)
            if (e.KeyCode == Keys.Delete && tabControl.ContainsFocus && tabControl.SelectedTab != null && tabControl.SelectedTab != placeholderTab)
            {
                // Verifica se o foco não está dentro do editor RTF (para não conflitar com exclusão de texto)
                var activeControl = this.ActiveControl;
                if (activeControl is ATCRTF || (activeControl is Control ctrl && ctrl.Parent is ATCRTF))
                {
                    return; // Ignora Delete se estiver digitando no editor
                }

                if (MessageBox.Show("Excluir esta aba permanentemente?", "Confirmação", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    TabPage tab = tabControl.SelectedTab;
                    ATCRTF editor = tab.Controls.OfType<ATCRTF>().FirstOrDefault();
                    if (editor != null)
                    {
                        editor.SalvaRTF();
                        try { if (File.Exists(editor.caminhoDoArquivo)) File.Delete(editor.caminhoDoArquivo); }
                        catch { }
                    }
                    tabControl.TabPages.Remove(tab);
                    SaveTabs();
                    Debug.WriteLine("[v1.3.8] Aba excluída");
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Salva conteúdo de todas as abas
            foreach (TabPage tab in tabControl.TabPages)
            {
                if (tab != placeholderTab)
                {
                    tab.Controls.OfType<ATCRTF>().FirstOrDefault()?.SalvaRTF();
                }
            }

            SaveTabs();

            // Salva posição da janela
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY, true))
                {
                    key.SetValue("WindowPositionX", this.Left);
                    key.SetValue("WindowPositionY", this.Top);
                    key.SetValue("WindowWidth", this.Width);
                    key.SetValue("WindowHeight", this.Height);
                }
            }
            catch { }
        }
    }
}