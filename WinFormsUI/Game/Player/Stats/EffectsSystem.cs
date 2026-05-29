using XEngine.Core.Base;
using XEngine.Core.Scenery;

namespace WinFormsUI.Game.Player.Stats
{
    /// <summary>
    /// Система, отвечающая за обновление и истечение временных эффектов игроков.
    /// </summary>
    public class EffectsSystem : IGameSystem
    {
        public int Priority => 200;

        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Обновляет состояние всех активных эффектов на сцене, удаляя истекшие.
        /// </summary>
        /// <param name="_scene">Текущая сцена.</param>
        /// <param name="_dt">Время, прошедшее с последнего кадра.</param>
        public void Update(GScene _scene, float _dt)
        {
            foreach (var (_, eff) in _scene.Query<GEffects>()) eff.WearoffEffects();
        }
    }
}
