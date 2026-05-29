using Box2D.NET;
using static Box2D.NET.B2MathFunction;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Shapes;
using XEngine.Core.Base;

namespace XEngine.Core.Box2DCompat
{
    /// <summary>
    /// Вспомогательные методы для работы с физическим движком Box2D.
    /// </summary>
    public static class B2Helpers
    {
        /// <summary>
        /// Создает капсулу, вписанную в заданный ограничивающий прямоугольник (AABB).
        /// </summary>
        /// <param name="aabb">Ограничивающий прямоугольник.</param>
        /// <returns>Структура капсулы Box2D.</returns>
        public static B2Capsule MakeCapsule(B2AABB aabb)
        {
            var w = aabb.upperBound.X - aabb.lowerBound.X;
            var h = aabb.upperBound.Y - aabb.lowerBound.Y;
            var center = b2Lerp(aabb.lowerBound, aabb.upperBound, 0.5f);

            B2Vec2 p1, p2;
            float r;
            if (w > h)
            {
                r = h / 2;
                B2Vec2 v = new(w / 2 - r, 0);
                p1 = center + v;
                p2 = center - v;
            }
            else
            {
                r = w / 2;
                B2Vec2 v = new(0, h / 2 - r);
                p1 = center + v;
                p2 = center - v;

            }

            return new B2Capsule()
            {
                center1 = p1,
                center2 = p2,
                radius = r
            };
        }

        /// <summary>
        /// Проверяет, установлен ли определенный бит категории фильтрации для фигуры.
        /// </summary>
        /// <param name="id">Идентификатор фигуры.</param>
        /// <param name="flag">Проверяемый флаг категории.</param>
        /// <returns>True, если флаг установлен.</returns>
        public static bool CheckFlag(B2ShapeId id, ulong flag) => (B2Shapes.b2Shape_GetFilter(id).categoryBits & flag) == flag;

        /// <summary>
        /// Пытается получить игровую сущность (Entity), связанную с фигурой через пользовательские данные тела.
        /// </summary>
        /// <param name="id">Идентификатор фигуры.</param>
        /// <param name="entity">Выходной параметр: найденная сущность или значение по умолчанию.</param>
        /// <returns>True, если сущность найдена.</returns>
        public static bool TryFetchEntity(B2ShapeId id, out Entity entity)
        {
            var e = b2Body_GetUserData(b2Shape_GetBody(id)).GetRef<UserData>()?.HostBody.Owner;
            entity = e ?? default!;
            return e != null;
        }
    }
}
