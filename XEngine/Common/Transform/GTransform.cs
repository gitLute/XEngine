using Box2D.NET;
using OpenTK.Mathematics;
using XEngine.Core.Base;
using XEngine.Core.Box2DCompat.Components;
using XEngine.Core.Utils;

namespace XEngine.Core.Common.Transform
{
    /// <summary>
    /// Компонент трансформации игрового объекта. Управляет позицией, вращением и иерархией родитель-потомок.
    /// </summary>
    public sealed class GTransform : GameComponent
    {
        private Vector3 _position = Vector3.Zero;
        private float _rotation = 0;
        private Matrix4 _cachedMatrix;
        private bool _isDirty = true;

        public GTransform? Parent { get; private set; } = null;
        public GTransform? FirstChild { get; private set; } = null;
        public GTransform? NextSibling { get; private set; } = null;
        public GTransform? PrevSibling { get; private set; } = null;

        public bool IsSynced { get; private set; } = true;

        /// <summary>
        /// Инициализирует трансформацию заданными значениями позиции и вращения.
        /// </summary>
        /// <param name="values">Объект с начальными параметрами.</param>
        public GTransform Init(TransformValues values) => Init(values.Position, values.Rotation);

        /// <summary>
        /// Инициализирует трансформацию заданными координатами и углом поворота.
        /// </summary>
        /// <param name="pos">Начальная позиция.</param>
        /// <param name="rotation">Начальный угол поворота (в радианах).</param>
        public GTransform Init(Vector3 pos, float rotation)
        {
            _position = pos;
            _rotation = rotation;
            IsSynced = false;
            return this;
        }

        /// <summary>
        /// Устанавливает родителя для текущего объекта, обновляя связи в иерархии.
        /// Нельзя задать родителя объекту с компонентом Box2DBody.
        /// </summary>
        /// <param name="newParent">Новый родительский объект или null для удаления из иерархии.</param>
        public void SetParent(GTransform? newParent)
        {
            if (newParent != null &&  Owner.Get<GBox2DBody>() is not null)
            {
                throw new InvalidOperationException("Can not link entity's transform with Box2DBody, use Box2D constraint");
            }

            if (Parent != null)
            {
                if (Parent.FirstChild == this) Parent.FirstChild = NextSibling;
                if (PrevSibling != null) PrevSibling.NextSibling = NextSibling;
                if (NextSibling != null) NextSibling.PrevSibling = PrevSibling;
            }
            Parent = newParent;
            if (Parent != null)
            {
                NextSibling = Parent.FirstChild;
                if (NextSibling != null) NextSibling.PrevSibling = this;
                Parent.FirstChild = this;
                PrevSibling = null;
            }
        }

        /// <summary>
        /// Возвращает дочерний элемент по индексу в списке потомков.
        /// </summary>
        /// <param name="index">Индекс дочернего элемента.</param>
        /// <returns>Дочерний объект трансформации.</returns>
        public GTransform? GetChild(int index)
        {
            GTransform? current = FirstChild;
            int count = 0;
            while (current != null)
            {
                if (count == index) return current;
                count++;
                current = current.NextSibling;
            }
            throw new IndexOutOfRangeException();
        }

        /// <summary>
        /// Помечает текущий объект и всех его потомков как требующие пересчета матрицы мира.
        /// </summary>
        public void SetDirty()
        {
            if (_isDirty) return;
            _isDirty = true;

            GTransform? current = FirstChild;
            while (current != null)
            {
                current.SetDirty();
                current = current.NextSibling;
            }
        }

        public Vector2 Position2D
        {
            get => new(_position.X, _position.Y);
            set => Position = new Vector3(value.X, value.Y, _position.Z);
        }

        public float Layer
        {
            get => _position.Z;
            set => Position = new Vector3(_position.X, _position.Y, value);
        }

        public Vector3 RelativePosition => Parent != null ? (MathUtils.Homogenize(_position) * Parent.GetWorldMatrix()).Xyz : Position;
        public Vector2 RelativePosition2D => Parent != null ? (MathUtils.Homogenize(_position) * Parent.GetWorldMatrix()).Xy : Position2D;

        public Vector3 Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    _position = value;
                    SetDirty();
                    IsSynced = false;
                }
            }
        }

        /// <summary>
        /// Плавно приближает позицию объекта к целевой точке с заданной силой интерполяции.
        /// </summary>
        /// <param name="newPos">Целевая позиция.</param>
        /// <param name="strength">Коэффициент интерполяции (0..1).</param>
        public void Approach(Vector3 newPos, float strength)
        {
            Position = Vector3.Lerp(Position, newPos, strength);
        }

        public float Rotation
        {
            get => _rotation;
            set
            {
                if (_rotation != value)
                {
                    _rotation = value;
                    SetDirty();
                    IsSynced = false;
                }
            }
        }

        /// <summary>
        /// Синхронизирует позицию и вращение с физическим телом Box2D.
        /// </summary>
        /// <param name="gbody">Компонент физического тела.</param>
        public void SyncToBody(GBox2DBody gbody)
        {
            var pos = B2Bodies.b2Body_GetPosition(gbody.Id);
            var rot = B2MathFunction.b2Rot_GetAngle(B2Bodies.b2Body_GetRotation(gbody.Id));
            _position.X = pos.X;
            _position.Y = pos.Y;
            _rotation = rot;
            SetDirty();
        }

        /// <summary>
        /// Вычисляет и возвращает мировую матрицу трансформации с учетом иерархии родителей.
        /// </summary>
        /// <returns>Мировая матрица 4x4.</returns>
        public Matrix4 GetWorldMatrix()
        {
            if (_isDirty)
            {
                _cachedMatrix = Matrix4.CreateRotationZ(_rotation) *
                                Matrix4.CreateTranslation(_position);
                if (Parent != null) _cachedMatrix *= Parent.GetWorldMatrix();
                _isDirty = false;
            }
            return _cachedMatrix;
        }

        public override string ToString()
        {
            return $"[{_position}] ({_rotation:F2}rad)";
        }
    }
}
