using XEngine.Core.Base;

namespace WinFormsUI.Game.Combat.Projectiles
{
    /// <summary>
    /// Компонент, представляющий снаряд в игровом мире.
    /// </summary>
    public class GProjectile : GameComponent
    {
        /// <summary>
        /// Текущий урон снаряда.
        /// </summary>
        public float Damage { get; set; } = 0;

        /// <summary>
        /// Инициализирует компонент заданным значением урона.
        /// </summary>
        /// <param name="damage">Значение урона.</param>
        /// <returns>Текущий экземпляр компонента для цепочки вызовов.</returns>
        public GProjectile Init(float damage)
        {
            Damage = damage;
            return this;
        }
    }
}
