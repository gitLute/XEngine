using XEngine.Core.Common;

namespace WinFormsUI.Game.Player.Stats.Effects
{
    /// <summary>
    /// Базовый класс для эффектов, изменяющих характеристики игрока.
    /// Реализует паттерн Декоратор для динамического изменения статистики.
    /// </summary>
    public abstract class Effect : IPlayerStats
    {
        protected IPlayerStats? _stats;
        public readonly GameTimer Timer = new(float.MaxValue);

        public virtual float TopSpeed => _stats?.TopSpeed ?? 0f;
        public virtual float Acceleration => _stats?.Acceleration ?? 0f;
        public virtual float JumpPower => _stats?.JumpPower ?? 0f;
        public virtual float Armor => _stats?.Armor ?? 0f;

        /// <summary>
        /// Устанавливает базовые характеристики, которые будут модифицироваться эффектом.
        /// </summary>
        /// <param name="stats">Базовые характеристики.</param>
        /// <returns>Текущий экземпляр эффекта.</returns>
        public Effect SetBase(IPlayerStats stats)
        {
            _stats = stats;
            return this;
        }

        /// <summary>
        /// Устанавливает длительность действия эффекта.
        /// </summary>
        /// <param name="duration">Длительность в секундах.</param>
        /// <returns>Текущий экземпляр эффекта.</returns>
        public Effect Duration(float duration)
        {
            Timer.Duration = duration;
            return this;
        }
    }
}
