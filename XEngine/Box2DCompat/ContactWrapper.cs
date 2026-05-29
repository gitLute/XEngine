using Box2D.NET;
using XEngine.Core.Base;
using XEngine.Core.Box2DCompat.Components;

namespace XEngine.Core.Box2DCompat
{
    /// <summary>
    /// Обертка над контактом Box2D. Предоставляет удобный доступ к идентификаторам фигур, тел и связанных сущностей движка.
    /// </summary>
    public readonly struct ContactWrapper
    {
        private readonly B2ShapeId _shapeIdA;
        private readonly B2ShapeId _shapeIdB;
        private readonly B2ContactId _contactId;
        public readonly bool IsSensor;

        public B2ContactId? ContactId => IsSensor ? null : _contactId;
        public B2ShapeId ShapeIdA => _shapeIdA;
        public B2ShapeId ShapeIdB => _shapeIdB;
        public B2BodyId BodyIdA => B2Shapes.b2Shape_GetBody(_shapeIdA);
        public B2BodyId BodyIdB => B2Shapes.b2Shape_GetBody(_shapeIdB);
        public GBox2DBody? GBodyA => B2Bodies.b2Body_GetUserData(BodyIdA).GetRef<UserData>()?.HostBody;
        public GBox2DBody? GBodyB => B2Bodies.b2Body_GetUserData(BodyIdB).GetRef<UserData>()?.HostBody;
        public Entity? EntityA => B2Bodies.b2Body_GetUserData(BodyIdA).GetRef<UserData>()?.HostBody.Owner;
        public Entity? EntityB => B2Bodies.b2Body_GetUserData(BodyIdB).GetRef<UserData>()?.HostBody.Owner;

        /// <summary>
        /// Инициализирует обертку на основе события начала физического контакта.
        /// </summary>
        /// <param name="ev">Событие начала контакта.</param>
        public ContactWrapper(B2ContactBeginTouchEvent ev)
        {
            _shapeIdA = ev.shapeIdA;
            _shapeIdB = ev.shapeIdB;
            _contactId = ev.contactId;
            IsSensor = false;
        }

        /// <summary>
        /// Инициализирует обертку на основе события окончания физического контакта.
        /// </summary>
        /// <param name="ev">Событие окончания контакта.</param>
        public ContactWrapper(B2ContactEndTouchEvent ev)
        {
            _shapeIdA = ev.shapeIdA;
            _shapeIdB = ev.shapeIdB;
            _contactId = ev.contactId;
            IsSensor = false;
        }

        /// <summary>
        /// Инициализирует обертку на основе события входа в сенсорную область.
        /// </summary>
        /// <param name="ev">Событие входа в сенсор.</param>
        public ContactWrapper(B2SensorBeginTouchEvent ev)
        {
            _shapeIdA = ev.sensorShapeId;
            _shapeIdB = ev.visitorShapeId;
            IsSensor = true;
        }

        /// <summary>
        /// Инициализирует обертку на основе события выхода из сенсорной области.
        /// </summary>
        /// <param name="ev">Событие выхода из сенсора.</param>
        public ContactWrapper(B2SensorEndTouchEvent ev)
        {
            _shapeIdA = ev.sensorShapeId;
            _shapeIdB = ev.visitorShapeId;
            IsSensor = true;
        }
    }
}
