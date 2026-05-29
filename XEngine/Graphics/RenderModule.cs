using OpenTK.Mathematics;
using XEngine.Core.Graphics.OpenGL;
using XEngine.Core.Scenery;

namespace XEngine.Core.Graphics
{
    /// <summary>
    /// Абстрактный класс для создания модкля отрисовщика
    /// </summary>
    public abstract class RenderModule(GLProvider provider)
    {
        protected Vector2 _screenSize;
        protected readonly GLProvider _provider = provider;
        protected static Matrix4 GetProjectionMatrix(Vector2 size) => Matrix4.CreatePerspectiveFieldOfView(MathF.PI / 4, size.X / size.Y, 1, 200);

        public bool IsEnabled { get; set; } = true;
        public abstract int Priority { get; }

        /// <summary>
        /// отрисовка части сцены
        /// </summary>
        public abstract void Render(GScene scene);

        /// <summary>
        /// Установка области рисования (при изменении размера экрана)
        /// </summary>
        public void OnResize(int width, int height)
        {
            _screenSize.X = width;
            _screenSize.Y = height;
        }
    }
}
