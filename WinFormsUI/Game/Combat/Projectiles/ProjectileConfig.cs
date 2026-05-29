using WinFormsUI.Game.Config;

namespace WinFormsUI.Game.Combat.Projectiles
{
    /// <summary>
    /// Конфигурация параметров снаряда.
    /// </summary>
    public class ProjectileConfig : IIdentifilable
    {
        /// <summary>
        /// Уникальный идентификатор снаряда.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Базовый урон снаряда.
        /// </summary>
        public float Damage { get; set; }

        /// <summary>
        /// Размер снаряда.
        /// </summary>
        public float Size { get; set; }

        /// <summary>
        /// Максимальное время жизни снаряда в секундах.
        /// </summary>
        public float MaxLifetime { get; set; }

        /// <summary>
        /// Путь к текстуре снаряда относительно Assets/.
        /// </summary>
        public string TexturePath { get; set; } = "";
    }
}
