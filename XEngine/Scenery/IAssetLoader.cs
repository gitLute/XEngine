using XEngine.Core.Graphics.OpenGL;

namespace XEngine.Core.Scenery
{
    public interface IAssetLoader
    {
        /// <summary>
        /// Загрузка Текстуры.
        /// Загрузка поддерживает кеширование, т.е. следующие вызовы не загружаю текстуру снова, если она не была выгружена
        /// </summary>
        /// <param name="path">Путь к текстуре (от /Assets/Textures)</param>
        /// <param name="persistent">Флаг для загрузка текстуры в невыгружвемый пул (остается между сценами)</param>
        /// <returns></returns>
        public Texture2D LoadTexture(string path, bool persistent = false);
    }
}
