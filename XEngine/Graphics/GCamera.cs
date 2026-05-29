using OpenTK.Mathematics;
using XEngine.Core.Base;

namespace XEngine.Core.Graphics
{
    /// <summary>
    /// Компонент-маркер камеры
    /// </summary>
    public sealed class GCamera : GameComponent
    {
        /// <summary>
        /// Приближеки камеры к позиции 'newPos'. Использует метод 'Approach' из компонента 'GTransform'
        /// </summary>
        public void Approach(Vector3 newPos, float strength) => Owner.Transform.Approach(newPos, strength);

        /// <summary>
        /// Получает обратную матрицу положения камеры
        /// </summary>
        /// <returns></returns>
        public Matrix4 GetViewMatrix() => Matrix4.Invert(Owner.Transform.GetWorldMatrix());
    }
}
