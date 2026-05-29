using OpenTK.Graphics.OpenGL4;
using XEngine.Core.Common.Transform;
using XEngine.Core.Graphics;
using XEngine.Core.Graphics.OpenGL;
using XEngine.Core.Scenery;

namespace XEngine.Core.Common.Sprite.NineSlice
{
    /// <summary>
    /// Модуль отрисовки девятичастных спрайтов.
    /// Использует специализированный шейдер для корректного масштабирования текстур с сохранением пропорций границ.
    /// </summary>
    public class NineSliceRendererModule(GLProvider provider) : RenderModule(provider)
    {
        public override int Priority => 500;

        /// <summary>
        /// Отрисовывает все активные девятичастные спрайты в сцене.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        public override void Render(GScene scene)
        {
            if (_screenSize.X <= 0 || _screenSize.Y <= 0) return;
            var _shader = _provider.GetShader("NineSlice");

            _shader.Use();

            _shader.SetMatrix4("uProjection", GetProjectionMatrix(_screenSize));
            _shader.SetMatrix4("uView", scene.Camera.GetViewMatrix());

            _shader.SetInt("uTexture", 0);
            GL.ActiveTexture(TextureUnit.Texture0);
            float ppm = scene.World.PixelPerMetre;

            _provider.UnitQuad.Bind();
            foreach (var (_, tr, nineslice) in scene.Query<GTransform, GNineSlice>())
            {
                _shader.SetVector2("uTexSize", nineslice.TextureSize);
                _shader.SetVector4("uBorder", nineslice.Borders);
                _shader.SetVector2("uRenderSize", nineslice.RenderSize * ppm);
                nineslice.UseTexture();

                var modelMatrix = scene.World.PPMScale * nineslice.GetSize(ppm) * nineslice.GetModelMatrix() * tr.GetWorldMatrix();
                _shader.SetMatrix4("uModel", modelMatrix);
                _shader.SetFloat("uAlpha", nineslice.Alpha);

                _provider.UnitQuad.Draw();
            }

            GL.BindVertexArray(0);
        }
    }
}
