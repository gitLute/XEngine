using WinFormsUI.Game.Config;

namespace WinFormsUI.Game.Player
{
    public class PlayerConfig : IIdentifilable
    {
        /// <summary>
        /// Уникальный идентификатор конфигурации.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Значение брони, снижающее получаемый урон.
        /// </summary>
        public float Armor { get; set; }

        /// <summary>
        /// Максимальный запас здоровья.
        /// </summary>
        public float MaxHealth { get; set; }
        
        /// <summary>
        /// Скорость восстановления здоровья в секунду.
        /// </summary>
        public float HealthRegenRate { get; set; }

        /// <summary>
        /// Задержка перед началом восстановления здоровья после получения урона.
        /// </summary>
        public float HealthRegenDelay { get; set; }

        /// <summary>
        /// Ускорение при начале движения.
        /// </summary>
        public float Speed { get; set; }

        /// <summary>
        /// Ускорение при начале движения.
        /// </summary>
        public float Acceleration { get; set; }

        /// <summary>
        /// Сила прыжка.
        /// </summary>
        public float JumpPower { get; set; }

        /// <summary>
        /// Идентификатор стартового оружия.
        /// </summary>
        public string StartWeaponId { get; set; } = "";

        /// <summary>
        /// Имя набора текстур персонажа.
        /// </summary>
        public string TextureName { get; set; } = "";
    }
}
