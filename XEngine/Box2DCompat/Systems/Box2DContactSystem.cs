using Box2D.NET;
using XEngine.Core.Base;
using XEngine.Core.Scenery;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Bodies;

namespace XEngine.Core.Box2DCompat.Systems
{
    /// <summary>
    /// Система обработки физических контактов и сенсорных событий Box2D.
    /// Преобразует низкоуровневые события движка в вызовы методов компонентов GBox2DBody.
    /// </summary>
    public class Box2DContactSystem : IGameSystem
    {
        public int Priority => 50;

        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Обрабатывает все накопленные события контактов и сенсоров за текущий кадр.
        /// </summary>
        /// <param name="_scene">Текущая сцена.</param>
        /// <param name="_dt">Время, прошедшее с последнего кадра (не используется).</param>
        public void Update(GScene _scene, float _dt)
        {
            PollContacts(_scene);
            PollSensors(_scene);
        }

        /// <summary>
        /// Вызывает событие входа в контакт для связанного игрового объекта.
        /// </summary>
        /// <param name="scene">Сцена.</param>
        /// <param name="shapeId">Идентификатор фигуры.</param>
        /// <param name="cw">Обертка контакта.</param>
        private static void Enter(GScene scene, B2ShapeId shapeId, ContactWrapper cw)
        {
            b2Body_GetUserData(b2Shape_GetBody(shapeId)).GetRef<UserData>()
                ?.HostBody.CollisionEnter(scene, cw);
        }

        /// <summary>
        /// Вызывает событие выхода из контакта для связанного игрового объекта.
        /// </summary>
        /// <param name="scene">Сцена.</param>
        /// <param name="shapeId">Идентификатор фигуры.</param>
        /// <param name="cw">Обертка контакта.</param>
        private static void Exit(GScene scene, B2ShapeId shapeId, ContactWrapper cw)
        {
            b2Body_GetUserData(b2Shape_GetBody(shapeId)).GetRef<UserData>()
                ?.HostBody.CollisionExit(scene, cw);
        }

        /// <summary>
        /// Опрос событий начала и окончания твердых контактов.
        /// </summary>
        /// <param name="_scene">Сцена.</param>
        private static void PollContacts(GScene _scene)
        {
            var contact = B2Worlds.b2World_GetContactEvents(_scene.World.Id);
            for (int i = 0; i < contact.beginCount; i++)
            {
                var ev = contact.beginEvents[i];
                Enter(_scene, ev.shapeIdA, new(ev));
                Enter(_scene, ev.shapeIdB, new(ev));
            }
            for (int i = 0; i < contact.endCount; i++)
            {
                var ev = contact.endEvents[i];
                Exit(_scene, ev.shapeIdA, new(ev));
                Exit(_scene, ev.shapeIdB, new(ev));
            }
        }

        /// <summary>
        /// Опрос событий входа и выхода из сенсорных областей.
        /// </summary>
        /// <param name="_scene">Сцена.</param>
        private static void PollSensors(GScene _scene)
        {
            var contact = B2Worlds.b2World_GetSensorEvents(_scene.World.Id);
            for (int i = 0; i < contact.beginCount; i++)
            {
                var ev = contact.beginEvents[i];
                Enter(_scene, ev.sensorShapeId, new(ev));
                Enter(_scene, ev.visitorShapeId, new(ev));
            }
            for (int i = 0; i < contact.endCount; i++)
            {
                var ev = contact.endEvents[i];
                Exit(_scene, ev.sensorShapeId, new(ev));
                Exit(_scene, ev.visitorShapeId, new(ev));
            }
        }
    }
}
