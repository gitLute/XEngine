using Box2D.NET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace XEngine.Core.Graphics.OpenGL
{
    /// <summary>
    /// Rkfcc для отрисовки линий через OpenGL. Поддерживает динамическое добавление точек.
    /// </summary>
    public sealed class LineBatcher : IDisposable
    {
        /// <summary>
        /// Контекст для построения линии внутри LineBatcher.
        /// </summary>
        public readonly struct LineLoopScope : IDisposable
        {
            private readonly LineBatcher _batcher;
            private readonly bool _close;

            public readonly Vector4? Color;
            public readonly int loopStartIndex;

            public LineLoopScope(LineBatcher batcher, Vector4? color, bool close)
            {
                _batcher = batcher;
                _close = close;
                Color = color;
                this.loopStartIndex = batcher.VertexCount;
                batcher.CurrentLoop = this;
            }

            public void Dispose() => _batcher.EndLoop(_close);
        }

        private const int STRIDE = 2 + 4;
        private readonly List<float> _vertices = [];
        private readonly List<uint> _indices = [];

        private float[] _vertexCache = [];
        private uint[] _indexCache = [];
        private int _vboCapacity = 512 * STRIDE;
        private int _eboCapacity = 512;

        private int _vao, _vbo, _ebo;
        private bool _disposed = false;

        private int VertexCount => _vertices.Count / STRIDE;
        private LineLoopScope? CurrentLoop = null;

        /// <summary>
        /// Начинает построение линии. Возвращает контекст для добавления точек.
        /// </summary>
        /// <param name="color">Цвет линии (опционально).</param>
        /// <param name="closed">Замыкать ли линию в петлю.</param>
        public LineLoopScope TraceLine(Vector4? color = null, bool closed = true)
        {
            return new LineLoopScope(this, color, closed);
        }

        private void EndLoop(bool closeLoop = true)
        {
            if (CurrentLoop == null) return;
            if (closeLoop && VertexCount > CurrentLoop.Value.loopStartIndex + 2)
            {
                _indices.Add((uint)VertexCount - 1);
                _indices.Add((uint)CurrentLoop.Value.loopStartIndex);
            }
        }

        /// <summary>
        /// Инициализирует OpenGL ресурсы (VAO, VBO, EBO).
        /// </summary>
        public void Init()
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vboCapacity * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, STRIDE * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, STRIDE * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _eboCapacity * sizeof(uint), IntPtr.Zero, BufferUsageHint.DynamicDraw);

            GL.BindVertexArray(0);

            UpdateBuffers();
        }

        /// <summary>
        /// Добавляет точку к текущей линии.
        /// </summary>
        /// <param name="pos">Позиция точки.</param>
        /// <param name="color">Цвет точки (переопределяет цвет линии).</param>
        public void AddPoint(B2Vec2 pos, Vector4? color = null)
        {
            if (CurrentLoop == null) throw new InvalidOperationException("Unable to add point to line without context");

            Vector4 c = color ?? CurrentLoop.Value.Color ?? Vector4.One;

            _vertices.Add(pos.X);
            _vertices.Add(pos.Y);
            _vertices.Add(c.X);
            _vertices.Add(c.Y);
            _vertices.Add(c.Z);
            _vertices.Add(c.W);

            int current = VertexCount - 1;
            if (current > CurrentLoop.Value.loopStartIndex)
            {
                _indices.Add((uint)(current - 1));
                _indices.Add((uint)current);
            }
        }

        /// <summary>
        /// Очищает буферы вершин и индексов.
        /// </summary>
        public void Clear()
        {
            _vertices.Clear();
            _indices.Clear();
        }

        /// <summary>
        /// Отрисовывает накопленные линии.
        /// </summary>
        public void Draw()
        {
            if (_indices.Count == 0) return;
            GL.BindVertexArray(_vao);

            GL.Enable(EnableCap.Blend);
            UpdateBuffers();
            GL.DrawElements(PrimitiveType.Lines, _indices.Count, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// Освобождает OpenGL ресурсы.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_ebo);
            GL.DeleteVertexArray(_vao);

            GC.SuppressFinalize(this);
        }

        private void UpdateBuffers()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            int vboElementCount = _vertices.Count;
            int vboSizeBytes = vboElementCount * sizeof(float);

            if (vboElementCount > 0)
            {
                if (_vertexCache.Length < vboElementCount) _vertexCache = new float[vboElementCount];
                _vertices.CopyTo(_vertexCache, 0);

                if (vboElementCount > _vboCapacity)
                {
                    _vboCapacity = Math.Max(vboElementCount, _vboCapacity * 2);
                    GL.BufferData(BufferTarget.ArrayBuffer, _vboCapacity * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);
                }

                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vboSizeBytes, _vertexCache);
            }


            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            int eboElementCount = _indices.Count;
            int eboSizeBytes = eboElementCount * sizeof(uint);

            if (eboElementCount > 0)
            {
                if (_indexCache.Length < eboElementCount) _indexCache = new uint[eboElementCount];
                _indices.CopyTo(_indexCache, 0);

                if (eboElementCount > _eboCapacity)
                {
                    _eboCapacity = Math.Max(eboElementCount, _eboCapacity * 2);
                    GL.BufferData(BufferTarget.ElementArrayBuffer, _eboCapacity * sizeof(uint), IntPtr.Zero, BufferUsageHint.DynamicDraw);
                }

                GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, eboSizeBytes, _indexCache);
            }
        }
    }
}
