using System.Diagnostics;
using XEngine.Core.Graphics.OpenGL;
using XEngine.Core.Scenery;

namespace XEngine.Core.Graphics
{
    /// <summary>
    /// Менеджер рессурсов (графики)
    /// </summary>
    public sealed class AssetManager : IAssetLoader, IDisposable
    {
        private readonly Dictionary<string, Texture2D> _persistentTextures = [];
        private readonly Dictionary<string, Texture2D> _sceneTextures = [];
        private readonly string _assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
        private bool _disposed = false;

        /// <summary>
        /// Инициализация после загрузки OpenGL
        /// </summary>
        public void Init()
        {
            InitDefaultTexture();
        }

        private void InitDefaultTexture()
        {
            byte[] pixels = [255, 0, 255, 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 0, 255, 255];
            _persistentTextures["Default_Error"] = Texture2D.FromBytes(2, 2, pixels);
        }

        public Texture2D LoadTexture(string relativePath, bool isPersistent = false)
        {
            string fullPath = Path.GetFullPath(Path.Combine(_assetsPath, "Textures", relativePath));
            var targetDict = isPersistent ? _persistentTextures : _sceneTextures;

            if (_persistentTextures.TryGetValue(fullPath, out var pTex)) return pTex;
            if (_sceneTextures.TryGetValue(fullPath, out var sTex)) return sTex;

            if (!File.Exists(fullPath))
            {
                Debug.WriteLine($"[Error] Missing texture: {fullPath}");
                return _persistentTextures["Default_Error"];
            }

            var newTex = Texture2D.FromPath(fullPath);
            targetDict.Add(fullPath, newTex);
            return newTex;
        }

        /// <summary>
        /// вызрущка ресурсов сцены
        /// </summary>
        public void UnloadSceneAssets()
        {
            foreach (var tex in _sceneTextures.Values) tex.Dispose();
            _sceneTextures.Clear();
        }

        // <summary>
        /// вызрущка ресурсов сцены и невыгружаемых ресурсов
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var tex in _sceneTextures.Values) tex.Dispose();
            _sceneTextures.Clear();
            foreach (var tex in _persistentTextures.Values) tex.Dispose();
            _persistentTextures.Clear();
        }
    }
}
