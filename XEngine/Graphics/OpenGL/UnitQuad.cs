using OpenTK.Graphics.OpenGL4;

namespace XEngine.Core.Graphics.OpenGL
{
    /// <summary>
    /// Вспомогательный элемент для рисования изображений
    /// </summary>
    public sealed class UnitQuad : IDisposable
    {
        private int _vao, _vbo, _ebo;
        private bool _disposed = false;

        public UnitQuad() { }

        /// <summary>
        /// Инициализация после загрузки OpenGL
        /// </summary>
        public void Init()
        {
            float[] vertices = [ // (x, y) | (u, v)
                -0.5f,  0.5f, 0.0f, 1.0f,
                 0.5f,  0.5f, 1.0f, 1.0f,
                 0.5f, -0.5f, 1.0f, 0.0f,
                -0.5f, -0.5f, 0.0f, 0.0f
            ];
            uint[] indices = [0, 1, 2, 2, 3, 0];

            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);

            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            _ebo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // Position attribute (location 0 in shader)
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // UV attribute (location 1 in shader)
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);
        }

        /// <summary>
        /// Привязка (см. OpenGL BindVertexArray)
        /// </summary>
        public void Bind()
        {
            GL.BindVertexArray(_vao);
        }

        /// <summary>
        /// Рисование спрайта
        /// </summary>
        public void Draw()
        {
            if (_disposed) return;
            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        }

        /// <summary>
        /// Очистка
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_ebo);
            GL.DeleteVertexArray(_vao);
        }
    }
}
