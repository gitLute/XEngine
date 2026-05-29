namespace WinFormsUI.Game.Player.Stats
{
    /// <summary>
    /// Интерфейс, определяющий основные физические характеристики игрового персонажа.
    /// </summary>
    public interface IPlayerStats
    {
        /// <summary>
        /// Максимальная скорость движения.
        /// </summary>
        public float TopSpeed { get; }

        /// <summary>
        /// Ускорение при движении.
        /// </summary>
        public float Acceleration { get; }

        /// <summary>
        /// Сила прыжка.
        /// </summary>
        public float JumpPower { get; }

        /// <summary>
        /// Значение брони, снижающее получаемый урон.
        /// </summary>
        public float Armor { get; }
    }
}
