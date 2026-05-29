using OpenTK.Graphics.OpenGL4;
using XEngine.Core.Graphics;
using XEngine.Core.Graphics.OpenGL;
using XEngine.Core.Scenery;

namespace XEngine.Core.Common.Trace
{
    /// <summary>
    /// Модуль отрисовки следов объектов. Визуализирует накопленные точки с затуханием яркости.
    /// </summary>
    public class TracerRenderModule(GLProvider provider) : RenderModule(provider)
    {
        public override int Priority => -1;

        /// <summary>
        /// Отрисовывает все активные следы в сцене с использованием шейдера линий.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        public override void Render(GScene scene)
        {
            if (_screenSize.X <= 0 || _screenSize.Y <= 0) return;
            var _shader = _provider.GetShader("Line");

            _shader.Use();

            _shader.SetMatrix4("uProjection", GetProjectionMatrix(_screenSize));
            _shader.SetMatrix4("uView", scene.Camera.GetViewMatrix());
            var lb = _provider.LineBatcher;
            foreach (var (_, tr) in scene.Query<GTrace>())
            {
                float brightness = 1;
                using (lb.TraceLine(closed: false))
                {
                    foreach (var p in tr.PointQueue.Reverse())
                    {
                        lb.AddPoint(new(p.X, p.Y), new(tr.Color.X, tr.Color.Y, tr.Color.Z, brightness));
                        brightness *= 0.2f;
                    }
                }
            }

            _provider.LineBatcher.Draw();
            _provider.LineBatcher.Clear();
            GL.BindVertexArray(0);
        }
    }
}
