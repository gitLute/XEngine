using XEngine.Core.Common.Sprite;
using XEngine.Core.Common.Sprite.NineSlice;
using XEngine.Core.Config;
using XEngine.Core.DebugUtils.Render;
using XEngine.Core.Graphics;
using XEngine.Core.Graphics.OpenGL;
using XEngine.Core.Input;
using XEngine.Core.Scenery;

namespace XEngine.Core
{
    /// <summary>
    /// Движок игры
    /// </summary>
    public sealed class GameEngine : IDisposable
    {
        private readonly AssetManager _assets;
        private readonly SceneManager _sceneManager;
        private readonly RenderPipeline _renderPipeline;
        private readonly InputManager _input;
        private readonly GLProvider _glProvider;

        private bool _disposed = false;

        public AssetManager Assets => _assets;
        public SceneManager SceneManager => _sceneManager;
        public RenderPipeline Renderer => _renderPipeline;
        public InputManager Input => _input;
        public GLProvider GLProvider => _glProvider;

        public GameConfig _config;

        public GameEngine()
        {
            _assets = new AssetManager();
            _sceneManager = new SceneManager(_assets);
            _renderPipeline = new RenderPipeline();
            _input = new InputManager();

            string AssetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            _glProvider = new GLProvider(Path.Combine(AssetPath, "Shaders"));
            _config = new ConfigManager().Load(Path.Combine(AssetPath, "config.json"));
        }

        /// <summary>
        /// Инициализирует движок полсе загрузки OpenGL 
        /// </summary>
        public void Init()
        {
            _assets.Init();

            _glProvider.Init();
            _glProvider.LoadShader("Line", "Line");
            _glProvider.LoadShader("NineSlice", "NineSlice");

            SpriteRendererModule spriteRenderer = new(_glProvider);
            NineSliceRendererModule nineslicerenderer = new(_glProvider);
            Box2DBodyRender debugRenderer = new(_glProvider) { IsEnabled = _config.Debug };
            _renderPipeline.AddRenderModule(spriteRenderer);
            _renderPipeline.AddRenderModule(nineslicerenderer);
            _renderPipeline.AddRenderModule(debugRenderer);

            _input.LoadBindingsFromConfig(_config);
        }

        /// <summary>
        /// обновление движка
        /// </summary>
        /// <param name="dt">Время с предыдущего обновления</param>
        public void Update(float dt)
        {
            _sceneManager.Update(dt);
            _input.Update(dt);
        }

        /// <summary>
        /// лтрисовка кадра
        /// </summary>
        public void Render()
        {
            if (_sceneManager.CurrentScene != null) _renderPipeline.Render(_sceneManager.CurrentScene);
        }

        /// <summary>
        /// Очистка Менеджера сцен
        /// Очистка Ресурсов (графика)
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _sceneManager.Dispose();
            _assets.Dispose();
            _glProvider.Dispose();
        }
    }
}
