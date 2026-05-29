using WinFormsUI.Game.Config;

namespace WinFormsUI.Game.Combat.Weapons
{
    /// <summary>
    /// Утилта для создания экземпляров оружия на основе конфигураций.
    /// Реализует паттерн Одиночка (Singleton).
    /// </summary>
    public class WeaponUtils
    {
        private readonly ConfigLoader<WeaponConfig> configLoader;

        private static readonly Lazy<WeaponUtils> _instance = new(() => new WeaponUtils());
        public static WeaponUtils Instance => _instance.Value;
        private WeaponUtils()
        {
            configLoader = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Config", "weapons.json"));
        }

        /// <summary>
        /// Возвращает список всех доступных идентификаторов оружия.
        /// </summary>
        /// <returns>Перечисление идентификаторов.</returns>
        public IEnumerable<string> GetIds() => configLoader.GetAllIds();

        /// <summary>
        /// Пытается создать экземпляр оружия по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор оружия.</param>
        /// <param name="item">Выходной параметр с созданным экземпляром.</param>
        /// <returns>True, если оружие успешно создано; иначе false.</returns>
        public bool TryCreateWeapon(string id, out WeaponItem item)
        {
            item = default!;
            bool res = configLoader.TryGetConfig(id, out var wConfig);
            if (res) item = new(wConfig);
            return res;
        }
    }
}
