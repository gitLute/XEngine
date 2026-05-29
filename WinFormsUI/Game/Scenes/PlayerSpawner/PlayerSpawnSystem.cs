using XEngine.Core.Base;
using XEngine.Core.Scenery;

namespace WinFormsUI.Game.Scenes.PlayerSpawner
{
    /// <summary>
    /// Система начального спавна игроков. Находит точки спавна A и B, создает соответствующих игроков и затем самоуничтожается.
    /// </summary>
    public class PlayerSpawnSystem(string AId, string BId) : IGameSystem
    {
        private bool _AUnset = true;
        private bool _BUnset = true;

        public int Priority => -1;

        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Проверяет наличие точек спавна с именами "A" и "B". При нахождении планирует создание игроков и удаляет систему из сцены.
        /// </summary>
        /// <param name="_scene">Текущая сцена.</param>
        /// <param name="_dt">Время, прошедшее с последнего кадра (не используется).</param>
        public void Update(GScene _scene, float _dt)
        {
            foreach (var (_, s) in _scene.Query<GPlayerSpawner>())
            {
                if (_AUnset && s.Name == "A") { _scene.Schedule(() => s.Spawn(AId)); _AUnset = false; }
                if (_BUnset && s.Name == "B") { _scene.Schedule(() => s.Spawn(BId)); _BUnset = false; }
            }
            _scene.Schedule(() => _scene.RemoveSystem(this));
        }
    }
}
