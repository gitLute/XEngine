using OpenTK.Mathematics;
using WinFormsUI.Game.Config;
using XEngine.Core.Utils.JSONConverters;

namespace WinFormsUI.Game.Combat.Weapons
{
    /// <summary>
    /// Конфигурация параметров оружия.
    /// </summary>
    public class WeaponConfig : IIdentifilable
    {
        /// <summary>
        /// Уникальный идентификатор оружия.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Идентификатор снаряда, выпускаемого этим оружием.
        /// </summary>
        public string ProjectileId { get; set; } = "";

        /// <summary>
        /// Максимальный боезапас.
        /// </summary>
        public int MaxAmmo { get; set; }

        /// <summary>
        /// Скорострельность (выстрелов в секунду).
        /// </summary>
        public float FireRate { get; set; }

        /// <summary>
        /// Разброс траектории снарядов.
        /// </summary>
        public float Spread { get; set; }

        /// <summary>
        /// Количество снарядов в одном выстреле.
        /// </summary>
        public int Shots { get; set; }

        /// <summary>
        /// Начальная скорость полета снарядов.
        /// </summary>
        public float InitialVelocity { get; set; }


        /// <summary>
        /// Масштаб текстуры оружия.
        /// </summary>
        public float TextureScale { get; set; }

        /// <summary>
        /// Относительное смещение точки выстрела (дула) от центра текстуры.
        /// </summary>
        [JsonVector2]  public Vector2 MuzzleOffsetRatio { get; set; } = Vector2.Zero;

        /// <summary>
        /// Путь к текстуре оружия относительно Assets/.
        /// </summary>
        public string TexturePath { get; set; } = "None.png";
    }
}
