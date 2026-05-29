using Box2D.NET;
using XEngine.Core.Base;
using XEngine.Core.Common.Transform;
using XEngine.Core.Scenery;

namespace XEngine.Core.Box2DCompat.Components
{
    /// <summary>
    /// Компонент физического тела Box2D. Управляет созданием, настройкой и взаимодействием тел в физическом мире.
    /// </summary>
    public sealed class GBox2DBody : GameComponent, IDisposable
    {
        private bool _disposed = false;
        private bool _isCollisionCallbackEnabled = false;
        public B2BodyId Id;

        public Action<ContactWrapper>? OnCollisionEnter = null;
        public Action<ContactWrapper>? OnCollisionExit = null;

        #region Building
        /// <summary>
        /// Строитель для конфигурации параметров тела перед его созданием в мире.
        /// </summary>
        public struct GBox2DBodyBuilder
        {
            private B2BodyDef _bodyDef = B2Types.b2DefaultBodyDef();
            private readonly GBox2DBody _targetBody;

            /// <summary>
            /// Инициализирует строитель с начальными параметрами позиции и вращения из трансформации.
            /// </summary>
            /// <param name="body">Целевой компонент тела.</param>
            /// <param name="tr">Компонент трансформации для начальных координат.</param>
            internal GBox2DBodyBuilder(GBox2DBody body, GTransform tr)
            {
                _targetBody = body;
                _bodyDef.position = new B2Vec2(tr.Position.X, tr.Position.Y);
                _bodyDef.rotation = B2MathFunction.b2MakeRot(tr.Rotation);
            }

            /// <summary>
            /// Устанавливает тип физического тела (статическое, кинематическое или динамическое).
            /// </summary>
            /// <param name="type">Тип тела.</param>
            public GBox2DBodyBuilder SetType(B2BodyType type)
            {
                _bodyDef.type = type;
                return this;
            }

            /// <summary>
            /// Устанавливает ограничения на движение тела по осям.
            /// </summary>
            /// <param name="motionLocks">Флаги блокировки движения.</param>
            public GBox2DBodyBuilder SetMotinLocks(B2MotionLocks motionLocks)
            {
                _bodyDef.motionLocks = motionLocks;
                return this;
            }

            /// <summary>
            /// Включает или отключает возможность перехода тела в спящий режим для оптимизации производительности.
            /// </summary>
            /// <param name="value">Значение флага сна.</param>
            public GBox2DBodyBuilder SetEnableSleep(bool value)
            {
                _bodyDef.enableSleep = value;
                return this;
            }

            /// <summary>
            /// Устанавливает флаг "пули" для предотвращения туннелирования при высоких скоростях.
            /// </summary>
            /// <param name="value">Значение флага пули.</param>
            public GBox2DBodyBuilder SetBulletFlag(bool value)
            {
                _bodyDef.isBullet = value;
                return this;
            }

            /// <summary>
            /// Создает тело в указанном физическом мире и присваивает ему идентификатор.
            /// </summary>
            /// <param name="worldId">Идентификатор мира Box2D.</param>
            /// <returns>Экземпляр компонента тела.</returns>
            public readonly GBox2DBody Build(B2WorldId worldId)
            {
                _targetBody.Id = B2Bodies.b2CreateBody(worldId, _bodyDef);
                return _targetBody;
            }
        }

        /// <summary>
        /// Инициализирует процесс создания тела, возвращая объект строителя для дальнейшей настройки.
        /// </summary>
        /// <param name="tr">Компонент трансформации для начальных координат.</param>
        /// <returns>Строитель тела.</returns>
        public GBox2DBodyBuilder Init(GTransform tr)
        {
            return new GBox2DBodyBuilder(this, tr);
        }

        /// <summary>
        /// Включает обработку коллизий для данного тела, связывая пользовательские данные.
        /// </summary>
        public GBox2DBody EnableCollisionCallback()
        {
            if (!_isCollisionCallbackEnabled)
            {
                B2Bodies.b2Body_SetUserData(Id, B2UserData.Ref(new UserData(this)));
                _isCollisionCallbackEnabled = true;
            }
            return this;
        }

        /// <summary>
        /// Отключает обработку коллизий, очищая пользовательские данные тела.
        /// </summary>
        public GBox2DBody DisableCollisionCallbacks()
        {
            if (_isCollisionCallbackEnabled)
            {
                B2Bodies.b2Body_SetUserData(Id, B2UserData.Empty);
                _isCollisionCallbackEnabled = false;
            }
            return this;
        }

        /// <summary>
        /// Прикрепляет фигуры (коллайдеры) к телу с помощью предоставленного делегата.
        /// </summary>
        /// <param name="attachShapes">Делегат, принимающий ID тела для добавления фигур.</param>
        public GBox2DBody AttacShapes(Action<B2BodyId> attachShapes)
        {
            attachShapes(Id);
            return this;
        }

        /// <summary>
        /// Синхронизирует состояние физического тела с текущей трансформацией игрового объекта.
        /// </summary>
        /// <param name="tr">Компонент трансформации.</param>
        public void SyncToTransform(GTransform tr)
        {
            B2Vec2 pos = new(tr.Position.X, tr.Position.Y);
            B2Rot rot = B2MathFunction.b2MakeRot(tr.Rotation);
            B2Bodies.b2Body_SetTransform(Id, pos, rot);
        }
        #endregion

        /// <summary>
        /// Вызывается системой при начале контакта. Планирует вызов пользовательского обработчика OnCollisionEnter.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        /// <param name="contactEvent">Обертка события контакта.</param>
        public void CollisionEnter(GScene scene, ContactWrapper contactEvent)
        {
            scene.Schedule(() => OnCollisionEnter?.Invoke(contactEvent));
        }

        /// <summary>
        /// Вызывается системой при окончании контакта. Планирует вызов пользовательского обработчика OnCollisionExit.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        /// <param name="contactEvent">Обертка события контакта.</param>
        public void CollisionExit(GScene scene, ContactWrapper contactEvent)
        {
            scene.Schedule(() => OnCollisionExit?.Invoke(contactEvent));
        }

        public float GravityScale
        {
            get => B2Bodies.b2Body_GetGravityScale(Id);
            set => B2Bodies.b2Body_SetGravityScale(Id, value);
        }
        public float AngularVelocity
        {
            get => B2Bodies.b2Body_GetAngularVelocity(Id);
            set => B2Bodies.b2Body_SetAngularVelocity(Id, value);
        }
        public B2Vec2 LinearVelocity
        {
            get => B2Bodies.b2Body_GetLinearVelocity(Id);
            set => B2Bodies.b2Body_SetLinearVelocity(Id, value);
        }


        /// <summary>
        /// Применяет импульс к центру масс тела.
        /// </summary>
        /// <param name="impulse">Вектор импульса.</param>
        public void ApplyImpulse(B2Vec2 impulse) => B2Bodies.b2Body_ApplyLinearImpulseToCenter(Id, impulse, true);

        /// <summary>
        /// Применяет импульс к центру масс тела, заданный координатами.
        /// </summary>
        /// <param name="x">X-компонента импульса.</param>
        /// <param name="y">Y-компонента импульса.</param>
        public void ApplyImpulse(float x = 0, float y = 0) => B2Bodies.b2Body_ApplyLinearImpulseToCenter(Id, new(x, y), true);


        /// <summary>
        /// Уничтожает физическое тело в мире Box2D и освобождает ресурсы.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            B2Bodies.b2DestroyBody(Id);
        }
    }
}
