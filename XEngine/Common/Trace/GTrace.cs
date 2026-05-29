using OpenTK.Mathematics;
using XEngine.Core.Base;

namespace XEngine.Core.Common.Trace
{
    /// <summary>
    /// Компонент для хранения истории позиций объекта (следа). Используется для визуализации траектории движения.
    /// </summary>
    public class GTrace : GameComponent
    {
        public readonly Queue<Vector2> PointQueue = new();
        public Vector3 Color;
        public int MaxLength;
        public bool IsAggressive;

        /// <summary>
        /// Инициализирует компонент следа заданным цветом и максимальной длиной очереди точек.
        /// </summary>
        /// <param name="color">Цвет следа.</param>
        /// <param name="length">Максимальное количество сохраняемых точек.</param>
        public GTrace Init(Vector3 color, int length)
        {
            Color = color;
            MaxLength = length;
            return this;
        }
    }
}
