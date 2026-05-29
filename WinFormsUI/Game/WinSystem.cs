using WinFormsUI.Game.Player;
using WinFormsUI.Game.Scenes;
using XEngine.Core.Base;
using XEngine.Core.Scenery;

namespace WinFormsUI.Game
{
    /// <summary>
    /// Система проверки условия победы. Завершает игру, если на сцене остается только один игрок.
    /// </summary>
    internal class WinSystem : IGameSystem
    {
        private bool IsWon = false;
        public int Priority => 700;

        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Проверяет количество игроков в сцене. Если остался один, объявляет его победителем и завершает сцену.
        /// </summary>
        /// <param name="_scene">Текущая сцена.</param>
        /// <param name="_dt">Время, прошедшее с последнего кадра (не используется).</param>
        public void Update(GScene _scene, float _dt)
        {
            if (!IsWon && _scene is MainScene scene)
            {
                var e = scene.Query<GPlayer>();
                if (e.Count() == 1)
                {
                    IsWon = true;
                    scene.WinnerName = e.FirstOrDefault().Item2.Control.Name;
                    scene.End();
                }
            }
        }
    }
}
