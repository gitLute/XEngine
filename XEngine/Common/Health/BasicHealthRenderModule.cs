using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using XEngine.Core.Common.Transform;
using XEngine.Core.Graphics;
using XEngine.Core.Graphics.OpenGL;
using XEngine.Core.Scenery;

namespace XEngine.Core.Common.Health
{
    /// <summary>
    /// Модуль отрисовки базовых индикаторов здоровья. Визуализирует полоски HP поверх объектов с использованием прямоугольников.
    /// </summary>
    public class BasicHealthRenderModule(GLProvider provider) : RenderModule(provider)
    {
        public override int Priority => 200;

        /// <summary>
        /// Отрисовывает все активные индикаторы здоровья в сцене.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        public override void Render(GScene scene)
        {
            if (_screenSize.X <= 0 || _screenSize.Y <= 0) return;
            var _shader = _provider.GetShader("Rectangle");

            _shader.Use();

            _shader.SetMatrix4("uProjection", GetProjectionMatrix(_screenSize));
            _shader.SetMatrix4("uView", scene.Camera.GetViewMatrix());

            _provider.UnitQuad.Bind();
            var size = new Vector2(25, 2) / scene.World.PixelPerMetre;

            foreach (var (_, tr, h, hr) in scene.Query<GTransform, GHealth, GHealthRenderer>())
            {
                var pos = tr.RelativePosition2D + hr.Position;
                var pos_bl = pos - size / 2;
                var pos_tr_full = pos + size / 2;
                var pos_tr_health = pos_tr_full - new Vector2(size.X * h.DisplayLeftRatio, 0);

                _shader.SetMatrix4("uModel", GetModel(new Box2(pos_bl, pos_tr_full), 0.1f));
                _shader.SetVector4("uColor", (Vector4)hr.BackGround);
                _provider.UnitQuad.Draw();

                _shader.SetMatrix4("uModel", GetModel(new Box2(pos_bl, pos_tr_health), 0.101f));
                _shader.SetVector4("uColor", (Vector4)hr.ForeGround);
                _provider.UnitQuad.Draw();

            }

            GL.BindVertexArray(0);
        }

        /// <summary>
        /// Формирует матрицу модели для прямоугольника на основе его границ и слоя отрисовки.
        /// </summary>
        /// <param name="box">Границы прямоугольника.</param>
        /// <param name="layer">Z-координата (слой) для правильного наложения.</param>
        /// <returns>Матрица трансформации 4x4.</returns>
        private static Matrix4 GetModel(Box2 box, float layer)
        {
            var size = box.Size;
            var pos = box.Center;
            return Matrix4.CreateScale(size.X, size.Y, 1) * Matrix4.CreateTranslation(pos.X, pos.Y, layer);
        }
    }
}
