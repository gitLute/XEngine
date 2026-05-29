using Box2D.NET;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XEngine.Core.Input.InputAxis;

namespace XEngine.Core.Utils
{
    /// <summary>
    /// Набор Вспомогательных Математических Функций
    /// </summary>
    public static class MathUtils
    {
        public const float Epsilon = 1e-6f;

        /// <summary>
        /// Создание вектора из длины и угла
        /// </summary>
        /// <param name="rho">длина вектора относительно (0, 0)</param>
        /// <param name="theta">угол вектора относительно OX против часовой стрелки</param>
        public static Vector2 FromPolar(float rho, float theta)
        {
            var (s, c) = MathF.SinCos(theta);
            return rho * new Vector2(c, s);
        }

        /// <summary>
        /// Вращение вектора
        /// </summary>
        /// <param name="v">Исходный вектор</param>
        /// <param name="angle">угол вращения против часовой стрелки</param>
        public static Vector2 Rotate(Vector2 v, float angle)
        {
            var (s, c) = MathF.SinCos(angle);
            return new Vector2(c * v.X + s * v.Y, -s * v.X + c * v.Y);
        }

        /// <summary>
        /// Перевод вектора из вида (x, y, z) в вид (x, y, z, 1)
        /// </summary>
        public static Vector4 Homogenize(Vector3 v) => new(v.X, v.Y, v.X, 1);

        /// <summary>
        /// Перевод вектора из вида (x, y, z, w) в вид (x/w, y/w, z/w)
        /// </summary>
        public static Vector3 Dehomogenize(Vector4 v) => v.Xyz / v.W;

        public static Vector2 FromB2Vec2(B2Vec2 v) => new(v.X, v.Y);
        public static B2Vec2 FromVector2(Vector2 v) => new(v.X, v.Y);

        /// <summary>
        /// Вектор смещения из точки 'from' в точку 'to' с ограничением длины в 'step'
        /// </summary>
        public static float LimitedStep(float from, float to, float step)
        {
            float diff = to - from;
            return Math.Abs(diff) <= step ? diff : Math.Sign(diff) * step;
        }

        /// <summary>
        /// Вектор смещения из вектора 'from' в вектор 'to' с ограничением длины в 'step'
        /// </summary>
        public static Vector2 LimitedStep(Vector2 from, Vector2 to, float step)
        {
            Vector2 diff = to - from;
            return diff.LengthSquared <= step * step ? diff : diff.Normalized() * step;
        }

        /// <summary>
        /// Перемещение точки 'from' к точки 'to' с сипользованием LimitedStep
        /// </summary>
        public static float MoveToward(float from, float to, float step)
        {
            if (Math.Abs(to - from) <= step)
                return to;

            return from + Math.Sign(to - from) * step;
        }
    }
}
