using Box2D.NET;
using XEngine.Core.Base;
using XEngine.Core.Box2DCompat.Components;
using XEngine.Core.Common.Transform;
using XEngine.Core.Scenery;

namespace XEngine.Core.Box2DCompat.Systems
{
    /// <summary>
    /// Система синхронизации трансформаций между физическим движком Box2D и игровыми объектами.
    /// Выполняет шаг симуляции и обновляет позиции/вращения тел на основе результатов расчета.
    /// </summary>
    /// <param name="SubSteps">Шаги симуляции</param>
    public class Box2DTransformSync(int SubSteps) : IGameSystem
    {
        public int Priority => 400;
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Обновляет состояние физических тел и синхронизирует их с компонентами трансформации.
        /// </summary>
        /// <param name="_scene">Текущая сцена.</param>
        /// <param name="_dt">Время, прошедшее с последнего кадра.</param>
        public void Update(GScene _scene, float _dt)
        {
            var pairs = _scene.Query<GTransform, GBox2DBody>();

            foreach (var (_, tr, b2b) in pairs) if (!tr.IsSynced) b2b.SyncToTransform(tr);
            B2Worlds.b2World_Step(_scene.World.Id, _dt, SubSteps);
            foreach (var (_, tr, b2b) in pairs) tr.SyncToBody(b2b);
        }
    }
}
