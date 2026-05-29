using WinFormsUI.Game.Player.Stats.Effects;
using XEngine.Core.Base;

namespace WinFormsUI.Game.Player.Stats
{
    /// <summary>
    /// Компонент-контейнер для единичного эффекта, привязанного к сущности (например, подобранному предмету).
    /// </summary>
    public class GHeldEffect : GameComponent
    {
        /// <summary>
        /// Хранимый эффект.
        /// </summary>
        public Effect Effect = null!;

        /// <summary>
        /// Инициализирует компонент заданным эффектом.
        /// </summary>
        /// <param name="effect">Экземпляр эффекта.</param>
        /// <returns>Текущий экземпляр компонента.</returns>
        public GHeldEffect Init(Effect effect)
        {
            Effect = effect;
            return this;
        }
    }
}
