using OpenTK.Mathematics;
using XEngine.Core.Common;
using XEngine.Core.Graphics.OpenGL;
using XEngine.Core.Scenery;

namespace WinFormsUI.Game.Combat.Weapons
{
    /// <summary>
    /// Представляет экземпляр оружия с его состоянием и ресурсами.
    /// </summary>
    public class WeaponItem
    {
        private readonly WeaponConfig _config;

        /// <summary>
        /// Таймер контроля скорострельности.
        /// </summary>
        public readonly GameTimer FireTimer = new(float.MaxValue);

        /// <summary>
        /// Идентификатор снаряда, используемого этим оружием.
        /// </summary>
        public string ProjectileId => _config.ProjectileId;

        /// <summary>
        /// Разброс при стрельбе.
        /// </summary>
        public float Spread => _config.Spread;

        /// <summary>
        /// Максимальный боезапас.
        /// </summary>
        public float MaxAmmo => _config.MaxAmmo;

        /// <summary>
        /// Начальная скорость вылета снарядов.
        /// </summary>
        public float InitialVelocity => _config.InitialVelocity;

        /// <summary>
        /// Количество выстрелов за один залп.
        /// </summary>
        public int Shots => _config.Shots;


        /// <summary>
        /// Текущий остаток боезапаса.
        /// </summary>
        public float CurrentAmmo = 0;


        /// <summary>
        /// Смещение дула относительно центра текстуры в мировых координатах.
        /// </summary>
        public Vector2 MuzzleOffset { get; private set; } = Vector2.Zero;

        /// <summary>
        /// Размер текстуры оружия.
        /// </summary>
        public Vector2 TexSize { get; private set; } = Vector2.Zero;

        /// <summary>
        /// Кэшированная текстура оружия.
        /// </summary>
        public Texture2D SavedTexture { get; private set; } = null!;
        private bool _initialized = false;

        /// <summary>
        /// Создает экземпляр оружия на основе конфигурации.
        /// </summary>
        /// <param name="config">Конфигурация оружия.</param>
        public WeaponItem(WeaponConfig config)
        {
            _config = config;
            FireTimer.Duration = 1.0f / config.FireRate;
            CurrentAmmo = MaxAmmo;
        }

        /// <summary>
        /// Инициализирует ресурсы оружия: регистрирует таймер и загружает текстуру.
        /// </summary>
        /// <param name="scene">Сцена, к которой привязано оружие.</param>
        /// <returns>Текущий экземпляр оружия.</returns>
        public WeaponItem Init(GScene scene)
        {
            if (_initialized) return this;
            _initialized = true;

            scene.RegisterTimer(FireTimer);
            FireTimer.Start();

            SavedTexture = scene.Assets.LoadTexture(_config.TexturePath);
            TexSize = new Vector2(_config.TextureScale);
            MuzzleOffset = _config.MuzzleOffsetRatio * _config.TextureScale * SavedTexture.Size / scene.World.PixelPerMetre;
            return this;
        }
    }
}
