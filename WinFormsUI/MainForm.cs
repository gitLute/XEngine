using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL4;
using System.Diagnostics;
using WinFormsUI.Game.Input;
using WinFormsUI.Game.Player;
using WinFormsUI.Game.Scenes;
using XEngine.Core;
using XEngine.Core.Common.Health;
using XEngine.Core.Common.Trace;

namespace WinFormsUI
{
    public partial class MainForm : Form
    {
        private readonly GameEngine _engine;
        private MainScene _scene = null!;

        private readonly Stopwatch _stopwatch = new();

        private double _accumulator = 0;
        private double _lastTime = 0;
        private const double TargetUpdateFPS = 60.0;
        private const double _dt = 1.0 / TargetUpdateFPS;

        public MainForm()
        {
            InitializeComponent();
            _engine = new GameEngine();
            KeyPreview = true;

            FormClosing += (_, _) => _engine.Dispose();
            Resize += (_, _) => _engine.Renderer.OnResize(ClientSize.Width, ClientSize.Height);

            KeyDown += (_, e) => _engine.Input.SetKeyDown(KeyConverter.ToOpenTK(e.KeyCode));
            KeyUp += (_, e) => _engine.Input.SetKeyUp(KeyConverter.ToOpenTK(e.KeyCode));
            LostFocus += (_, _) => _engine.Input.ClearStates();
            Deactivate += (_, _) => _engine.Input.ClearStates();
        }

        private void MainFormLoad(object sender, EventArgs e)
        {
            _engine.Renderer.AddRenderModule(new TracerRenderModule(_engine.GLProvider));

            _engine.Renderer.AddRenderModule(new BasicHealthRenderModule(_engine.GLProvider));
            _engine.GLProvider.LoadShader("Rectangle", "Rectangle");

            _engine.Renderer.OnResize(ClientSize.Width, ClientSize.Height);

            if (TryGetScene(out _scene)) _engine.SceneManager.SwitchTo(_scene);

            _stopwatch.Start();
            MainTimer.Start();

            Activate();
            Focus();
        }

        private void Restart()
        {
            string message = _scene.WinnerName == ""
                ? "ÍÈ×Üß! Îáà èãðîêà ïîãèáëè îäíîâðåìåííî!"
                : $"ÏÎÁÅÄÈË ÈÃÐÎÊ {_scene.WinnerName}!";
            DialogResult result = MessageBox.Show(message + "\n\nÍà÷àòü íîâóþ èãðó?", "ÈÃÐÀ ÎÊÎÍ×ÅÍÀ", MessageBoxButtons.YesNo);

            if (result == DialogResult.Yes && TryGetScene(out _scene)) _engine.SceneManager.SwitchTo(_scene);
            if (result == DialogResult.No) Environment.Exit(0);
        }

        private bool TryGetScene(out MainScene scene)
        {
            scene = default!;
            string[] variants = [.. PlayerUtil.Instance.GetIds()];
            using var form = new MenuForm(variants);
            if (form.ShowDialog() == DialogResult.OK)
            {
                scene = new(_engine, form.PlayerA, form.PlayerB, form.LevelPath);
                scene.OnEnd += Restart;
                return true;
            }
            return false;
        }

        private void OnGLLoad(object sender, EventArgs e)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.ClearColor(Color.Black);

            MainGLControl.Enabled = false;
            _engine.Init();
        }

        private void OnGLPaint(object sender, PaintEventArgs e)
        {
            var _gl = (GLControl)sender;
            _gl.MakeCurrent();
            _engine.Render();
            _gl.SwapBuffers();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            double currentTime = _stopwatch.Elapsed.TotalSeconds;
            double frameTime = currentTime - _lastTime;
            _lastTime = currentTime;
            _accumulator += frameTime;

            while (_accumulator >= _dt)
            {
                _engine.Update((float)_dt);
                _accumulator -= _dt;
            }

            MainGLControl.Invalidate();
        }
    }
}
