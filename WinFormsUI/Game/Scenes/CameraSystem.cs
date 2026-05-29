using OpenTK.Mathematics;
using WinFormsUI.Game.Player;
using XEngine.Core.Base;
using XEngine.Core.Common.Transform;
using XEngine.Core.Scenery;

namespace WinFormsUI.Game.Scenes
{
    /// <summary>
    /// Система управления камерой. Центрирует камеру на группе игроков и масштабирует обзор в зависимости от их разброса.
    /// </summary>
    internal class CameraSystem(float padding) : IGameSystem
    {
        public int Priority => 100;

        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Обновляет позицию и масштаб камеры, ориентируясь на среднюю позицию всех игроков.
        /// </summary>
        /// <param name="_scene">Текущая сцена.</param>
        /// <param name="_dt">Время, прошедшее с последнего кадра.</param>
        public void Update(GScene _scene, float _dt)
        {
            Vector2 center = new(0, 0);
            var E = _scene.Query<GTransform>((e, _) => e.Has<GPlayer>());
            foreach (var (_, tr) in E) center += tr.Position2D;
            if (E.Any())
            {
                center /= E.Count();
                var radius = E.Select(e => (center - e.Item2.Position2D).Length).Max();
                _scene.Camera.Owner.Transform.Approach(new(center.X, center.Y, radius * 2 + padding), 2f * _dt);
            }
        }
    }
}
