using XEngine.Core.Scenery;

namespace WinFormsUI.Game.Player.PlayerStates
{
    /// <summary>
    /// Интерфейс состояния машины состояний игрока.
    /// Определяет жизненный цикл конкретного поведения персонажа.
    /// </summary>
    public interface IPlayerState
    {
        /// <summary>
        /// Имя состояния для отладки.
        /// </summary>
        public string DebugName { get; }

        /// <summary>
        /// Метод входа в состояние. Инициализирует необходимые ресурсы и визуальные эффекты.
        /// </summary>
        /// <param name="player">Экземпляр игрока.</param>
        /// <param name="scene">Текущая сцена.</param>
        public void Enter(GPlayer player, GScene scene);

        /// <summary>
        /// Метод выхода из состояния. Освобождает ресурсы и восстанавливает предыдущее состояние системы.
        /// </summary>
        /// <param name="player">Экземпляр игрока.</param>
        /// <param name="scene">Текущая сцена.</param>
        public void Exit(GPlayer player, GScene scene);

        /// <summary>
        /// Метод обработки ввода и логики обновления каждый кадр.
        /// </summary>
        /// <param name="player">Экземпляр игрока.</param>
        /// <param name="scene">Текущая сцена.</param>
        /// <param name="dt">Время, прошедшее с последнего кадра.</param>
        public void ProcessInput(GPlayer player, GScene scene, float dt);
    }
}
