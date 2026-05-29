using OpenTK.Graphics.OpenGL4;
using XEngine.Core.Graphics.OpenGL;
using XEngine.Core.Scenery;


namespace XEngine.Core.Graphics
{
    /// <summary>
    /// Класс для создания сборщика кадра
    /// </summary>
    public class RenderPipeline
    {
        private readonly List<RenderModule> _renderModules = [];

        /// <summary>
        /// Добавит модуль сборщика
        /// </summary>
        public void AddRenderModule(RenderModule renderModule)
        {
            _renderModules.Add(renderModule);
            _renderModules.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        /// <summary>
        /// Установка области рисования (при изменении размера экрана)
        /// </summary>
        public void OnResize(int width, int height)
        {
            GL.Viewport(0, 0, width, height);
            foreach (var _r in _renderModules) _r.OnResize(width, height);
        }

        /// <summary>
        /// Отрисовка кадра
        /// </summary>
        /// <param name="scene">Сценя даля отрисоваки</param>
        public void Render(GScene scene)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            foreach (var _r in _renderModules) if (_r.IsEnabled) _r.Render(scene);
        }
    }
}
