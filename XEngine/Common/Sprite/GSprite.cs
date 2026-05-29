using OpenTK.Mathematics;
using XEngine.Core.Base;
using XEngine.Core.Common.Sprite.NineSlice;
using XEngine.Core.Graphics.OpenGL;

namespace XEngine.Core.Common.Sprite
{
    /// <summary>
    /// Базовый компонент спрайта.
    /// Управляет текстурой, позицией, размером и прозрачностью 2D-объекта.
    /// </summary>
    public class GSprite : GameComponent
    {
        public Texture2D Texture { get; set; } = null!;

        protected Vector2 _position = Vector2.Zero;
        protected Vector2 _size = Vector2.One;
        protected float _rotation = 0f;

        protected Matrix4 _modelMatrix = Matrix4.Identity;
        protected bool _isDirty = true;

        public float Alpha { get; protected set; } = 1;
        public SizingPolicy SizingPolicy { get; protected set; }
        public bool FlipX = false;
        public bool FlipY = false;

        /// <summary>
        /// Устанавливает текстуру для спрайта.
        /// </summary>
        /// <param name="texture">Текстура OpenGL.</param>
        public GSprite SetTexture(Texture2D texture)
        {
            Texture = texture;
            _isDirty = true;
            return this;
        }

        /// <summary>
        /// Устанавливает политику изменения размера спрайта.
        /// </summary>
        /// <param name="policy">Политика масштабирования.</param>
        public GSprite SetSizingPolicy(SizingPolicy policy)
        {
            SizingPolicy = policy;
            return this;
        }

        /// <summary>
        /// Устанавливает смещение текстуры относительно центра спрайта.
        /// </summary>
        /// <param name="vec">Вектор смещения.</param>
        public GSprite SetTranslation(Vector2 vec)
        {
            _position = vec;
            _isDirty = true;
            return this;
        }

        /// <summary>
        /// Устанавливает угол поворота спрайта.
        /// </summary>
        /// <param name="rotation">Угол в радианах.</param>
        public GSprite SetRotation(float rotation)
        {
            _rotation = rotation;
            _isDirty = true;
            return this;
        }

        /// <summary>
        /// Устанавливает размер области рендеринга спрайта.
        /// </summary>
        /// <param name="size">Новый размер.</param>
        public GSprite SetSize(Vector2 size)
        {
            _size = size;
            _isDirty = true;
            return this;
        }

        /// <summary>
        /// Устанавливает прозрачность спрайта.
        /// </summary>
        /// <param name="alpha">Значение альфа-канала (0..1).</param>
        public GSprite SetAlpha(float alpha)
        {
            Alpha = alpha;
            return this;
        }

        /// <summary>
        /// Вычисляет и возвращает матрицу модели для текущего состояния спрайта.
        /// </summary>
        /// <returns>Матрица трансформации 4x4.</returns>
        public Matrix4 GetModelMatrix()
        {
            if (_isDirty) Recalculate();
            return _modelMatrix;
        }

        /// <summary>
        /// Возвращает матрицу масштабирования в зависимости от выбранной политики.
        /// </summary>
        /// <param name="ppm">Количество пикселей на метр (для политики World).</param>
        /// <returns>Матрица масштабирования.</returns>
        public Matrix4 GetSize(float ppm = 1) => SizingPolicy switch
        {
            SizingPolicy.Identity => Matrix4.CreateScale(_size.X, _size.Y, 1),
            SizingPolicy.World => Matrix4.CreateScale(_size.X * ppm, _size.Y * ppm, 1),
            SizingPolicy.Source => Matrix4.CreateScale(TextureSize.X * _size.X, TextureSize.Y * _size.X, 1),
            _ => Matrix4.Identity,
        };

        /// <summary>
        /// Пересчитывает матрицу модели при изменении параметров трансформации.
        /// </summary>
        protected void Recalculate()
        {
            if (Texture == null)
            {
                _modelMatrix = Matrix4.Identity;
                _isDirty = false;
                return;
            }

            var rotMat = Matrix4.CreateRotationZ(_rotation);
            var transMat = Matrix4.CreateTranslation(_position.X, _position.Y, 0f);

            _modelMatrix = rotMat * transMat;
            _isDirty = false;
        }

        /// <summary>
        /// Активирует текущую текстуру в контексте OpenGL.
        /// </summary>
        public void UseTexture() => Texture.Use();

        public Vector2 TextureSize => new(Texture.Width, Texture.Height);
    }
}
