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
        private const string REGISTRY_KEY = @"AnoteitorRTF\MyApp";
        private const string TABS_SUBKEY = @"AnoteitorRTF\MyApp\Tabs";

        // v1.3.3 - Correção crítica: não usa Clear() destrutivo
        //   - Mantém a estrutura original do TabControl
        //   - Remove apenas abas dinâmicas (não tb1 nem tb2)
        //   - Garante renderização correta do ATCRTF

        public Form1()
        {
            InitializeComponent();
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            tabControl.MouseDoubleClick += TabControl_MouseDoubleClick;
            this.KeyDown += Form1_KeyDown;
            this.KeyPreview = true;
            Version version = Version.Parse(Application.ProductVersion);
            this.Text = $"AnoteitoRtf v{version.Major}.{version.Minor}";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Debug.WriteLine("[v1.3.3] Iniciando...");
            LoadWindowPosition();
            SetupPlaceholder();
            LoadTabs();
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
                        this.StartPosition = FormStartPosition.Manual;
                        this.Left = (int)(key.GetValue("WindowPositionX") ?? this.Left);
                        this.Top = (int)(key.GetValue("WindowPositionY") ?? this.Top);
                        this.Width = (int)(key.GetValue("WindowWidth") ?? this.Width);
                        this.Height = (int)(key.GetValue("WindowHeight") ?? this.Height);
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
                FormBorderStyle = FormBorderStyle.FixedDialog,
                AcceptButton = null, // Evita foco automático no OK
                CancelButton = null
            })
            using (Label lbl = new Label { Text = "Nome da aba:", AutoSize = true, Location = new Point(20, 25) })
            using (TextBox input = new TextBox
            {
                Location = new Point(20, 50),
                Size = new Size(260, 25),
                Text = current,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            })
            using (Button btnOk = new Button
            {
                Text = "OK",
                Location = new Point(140, 90),
                Size = new Size(80, 30),
                DialogResult = DialogResult.OK
            })
            using (Button btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new Point(230, 90),
                Size = new Size(80, 30),
                DialogResult = DialogResult.Cancel
            })
            {
                // Centraliza os botões horizontalmente
                int totalButtonsWidth = btnOk.Width + btnCancel.Width + 10;
                int startX = (prompt.ClientSize.Width - totalButtonsWidth) / 2;
                btnOk.Location = new Point(startX, 90);
                btnCancel.Location = new Point(startX + btnOk.Width + 10, 90);

                prompt.Controls.Add(lbl);
                prompt.Controls.Add(input);
                prompt.Controls.Add(btnOk);
                prompt.Controls.Add(btnCancel);

                // Define foco no TextBox
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