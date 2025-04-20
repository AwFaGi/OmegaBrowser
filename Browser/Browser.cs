using Browser.Management;
using Browser.Management.TabCommands;
using Browser.Networking;
using SkiaSharp.Views.Desktop;
using Timer = Browser.Management.Timer;

namespace Browser
{
    public class Browser
    {
        public ResourceManager resourceManager { get; private set; }
        private List<Command> TabCommands;
        public Tab currentTab { get; private set; }
        public BrowserOptions Options;
        private Form mainForm;
        private TextBox urlTextBox;
        private Button goButton;

        public struct BrowserOptions
        {
            public string baseDirectory { get; init; }
            public int viewport { get; init; }
        }

        public Browser(BrowserOptions options)
        {
            var baseDirectory = string.IsNullOrEmpty(options.baseDirectory) ? 
                                Directory.GetCurrentDirectory() : 
                                options.baseDirectory;

            var rmPath = Path.Combine(baseDirectory, "resources");
            Directory.CreateDirectory(rmPath);
            resourceManager = new ResourceManager(rmPath);

            var viewport = options.viewport > 0 ? options.viewport : 960;

            Options = new BrowserOptions()
            {
                baseDirectory = baseDirectory,
                viewport = viewport
            };
            
            InitCommands();
            CreateMainForm();
        }

        private void CreateMainForm()
        {
            mainForm = new Form
            {
                Text = "OmegaBrowser",
                Width = 1280,
                Height = 720,
                FormBorderStyle = FormBorderStyle.FixedSingle,
                MaximizeBox = false
            };

            var panel = new SKControl()
            {
                Dock = DockStyle.Top,
                Height = 40
            };

            urlTextBox = new TextBox
            {
                Width = 520,
                Left = 10,
                Top = 10,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };

            goButton = new Button
            {
                Text = "Go",
                Left = 20,
                Top = 10,
                Width = 80,
                Anchor = AnchorStyles.Top 
            };

            panel.Controls.Add(urlTextBox);
            panel.Controls.Add(goButton);
            
            var renderPanel = new SKControl()
            {
                Dock = DockStyle.None,
                BackColor = Color.White,
                Width = mainForm.Width - 20,
                Height = mainForm.Height - 80,
                Top = 40,
                Left = 0,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right | AnchorStyles.Left
            };

            mainForm.Controls.Add(panel);
            mainForm.Controls.Add(renderPanel);
            
            goButton.Click += async (sender, args) =>
            {
                await LoadAndRenderPage(renderPanel);
                renderPanel.Controls.Add(currentTab.proxyPanel);
            };

            // Добавляем обработчик для нажатия Enter в текстовом поле
            urlTextBox.KeyDown += async (sender, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    await LoadAndRenderPage(renderPanel);
                    renderPanel.Controls.Add(currentTab.proxyPanel);
                }
            };
        }

        private async Task LoadAndRenderPage(SKControl renderPanel)
        {
            var url = urlTextBox.Text;
            if (string.IsNullOrEmpty(url)) return;

            try
            {
                goButton.Enabled = false;
                urlTextBox.Enabled = false;
                Cursor.Current = Cursors.WaitCursor;

                await Task.Run(() => 
                {
                    Timer.start();
                    currentTab = LoadTab(url, renderPanel);
                    // new ObjectRenderer(currentTab, new Layout(currentTab.document, currentTab.owner.Options.viewport));
                });
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error loading page: {e.Message}", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                goButton.Enabled = true;
                urlTextBox.Enabled = true;
                Cursor.Current = Cursors.Default;
            }
        }

        public void Run()
        {
            Application.Run(mainForm);
        }

        private void InitCommands()
        {
            TabCommands = new List<Command>
            {
                new PrintTreeCommand(this),
                new PrintCssTreeCommand(this),
                new PrintResourcesCommand(this),
                new DownloadResources(this),
                new ClearResourcesCommand(this)
            };
        }

        private Tab LoadTab(string url, SKControl renderPanel)
        {
            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    url = "https://" + url;
                }
                var res = new Resource(url, Resource.ResourceType.Html);
                var result = resourceManager.GetResource(ref res);
                if (!result) throw new Exception();

                return new Tab(res, this, renderPanel);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw new Exception("Error on loading page");
            }
        }
    }
}