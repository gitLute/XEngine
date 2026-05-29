using OpenTK.Mathematics;
using XEngine.Core.Utils.JSONConverters;

namespace XEngine.Core.Common.Transform
{
    /// <summary>
    /// Хранит базовые параметры трансформации объекта: позицию и вращение.
    /// </summary>
    public class TransformValues
    {
        /// <summary>
        /// Позиция объекта в трехмерном пространстве.
        /// </summary>
        [JsonVector3] public Vector3 Position { get; set; } = Vector3.Zero;

        /// <summary>
        /// Угол поворота объекта вокруг оси Z (в радианах).
        /// </summary>
        public float Rotation { get; set; } = 0;
    }
}
