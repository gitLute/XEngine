namespace WinFormsUI
{
    public partial class MenuForm : Form
    {
        private readonly ComboBox comboPlayerA;
        private readonly ComboBox comboPlayerB;
        private readonly Button btnOk;
        private readonly TextBox txtLevelPath;
        private readonly Button btnBrowseLevel;

        public string PlayerA { get; private set; } = "";
        public string PlayerB { get; private set; } = "";
        public string LevelPath { get; private set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Config", "DefaultLevel.json");
        private readonly string[] vars;

        public MenuForm(string[] variants)
        {
            InitializeComponent();
            vars = variants;

            Text = "Выберите игроков";
            Size = new Size(350, 200);
            StartPosition = FormStartPosition.CenterParent;

            var lblPlayerA = new Label { Text = "Игрок А:", Location = new(10, 13), AutoSize = true };
            comboPlayerA = new ComboBox { Location = new(100, 10), Width = 220 };

            var lblPlayerB = new Label { Text = "Игрок Б:", Location = new(10, 43), AutoSize = true };
            comboPlayerB = new ComboBox { Location = new(100, 40), Width = 220 };

            var lblselectLevel = new Label { Text = "Уровень:", Location = new(10, 73), AutoSize = true };
            txtLevelPath = new TextBox
            {
                Location = new(100, 70),
                Width = 180,
                ReadOnly = true,
                Text = LevelPath
            };
            btnBrowseLevel = new Button
            {
                Text = "...",
                Location = new(285, 69),
                Width = 35,
                Height = 23
            };
            btnBrowseLevel.Click += BtnBrowseLevel_Click;

            btnOk = new Button { Text = "OK", Location = new(10, 110), DialogResult = DialogResult.OK };

            comboPlayerA.Items.AddRange(vars);
            comboPlayerB.Items.AddRange(vars);

            Controls.Add(lblPlayerA);
            Controls.Add(comboPlayerA);
            Controls.Add(lblPlayerB);
            Controls.Add(comboPlayerB);
            Controls.Add(lblselectLevel);
            Controls.Add(txtLevelPath);
            Controls.Add(btnBrowseLevel);
            Controls.Add(btnOk);

            AcceptButton = btnOk;
        }

        private void BtnBrowseLevel_Click(object? sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
            openFileDialog.Title = "Выберите файл уровня";

            if (!string.IsNullOrEmpty(LevelPath) && Directory.Exists(Path.GetDirectoryName(LevelPath)))
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(LevelPath);
            }

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                LevelPath = openFileDialog.FileName;
                txtLevelPath.Text = LevelPath;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                PlayerA = comboPlayerA.SelectedItem?.ToString() ?? vars[0];
                PlayerB = comboPlayerB.SelectedItem?.ToString() ?? vars[0];
            }
            base.OnFormClosing(e);
        }
    }
}
