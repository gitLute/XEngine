using WinFormsUI.Game.Config;

namespace WinFormsUI.Game.Combat.Projectiles
{
    /// <summary>
    /// Загружает и предоставляет доступ к конфигурациям снарядов.
    /// Реализует паттерн Одиночка (Singleton).
    /// </summary>
    public class ProjectileConfigLoader
    {
        private readonly ConfigLoader<ProjectileConfig> configLoader;
        private static readonly Lazy<ProjectileConfigLoader> _instance = new(() => new ProjectileConfigLoader());
        private ProjectileConfigLoader()
        {
            configLoader = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Config", "projectiles.json"));
        }
        public static ProjectileConfigLoader Instance => _instance.Value;

        /// <summary>
        /// Пытается получить конфигурацию снаряда по идентификатору.
        /// </summary>
        /// <param name="projectileId">Идентификатор снаряда.</param>
        /// <param name="pConfig">Выходной параметр с конфигурацией, если она найдена.</param>
        /// <returns>True, если конфигурация найдена; иначе false.</returns>
        public bool TryGetConfig(string projectileId, out ProjectileConfig pConfig)
        {
            return configLoader.TryGetConfig(projectileId, out pConfig);
        }
    }
}
