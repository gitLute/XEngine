using System.Diagnostics;
using System.Text.Json;

namespace WinFormsUI.Game.Config
{
    /// <summary>
    /// Универсальный загрузчик конфигураций из JSON-файла.
    /// Кэширует данные в памяти для быстрого доступа по идентификатору.
    /// </summary>
    /// <typeparam name="T">Тип конфигурации, реализующий интерфейс IIdentifilable.</typeparam>
    public class ConfigLoader<T> where T : class, IIdentifilable
    {
        private readonly JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
        private readonly Dictionary<string, T> _database = [];

        /// <summary>
        /// Инициализирует загрузчик, читая указанный файл.
        /// </summary>
        /// <param name="_filePath">Путь к файлу конфигурации.</param>
        public ConfigLoader(string _filePath)
        {
            if (!File.Exists(_filePath))
            {
                Debug.WriteLine($"[Warn]: Config file {_filePath} not. Config is empty.");
                return;
            }

            List<T>? configs = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(_filePath), options);

            if (configs == null)
            {
                Debug.WriteLine($"[Warn]: Parser could not parse {_filePath}. Config empty.");
                return;
            }

            foreach (var config in configs)
                if (!_database.TryAdd(config.Id, config))
                    Debug.WriteLine($"[Warn]: Duplicate ID '{config.Id}' ignored.");
        }

        /// <summary>
        /// Пытается получить конфигурацию по её уникальному идентификатору.
        /// </summary>
        /// <param name="Id">Идентификатор конфигурации.</param>
        /// <param name="res">Выходной параметр с найденной конфигурацией.</param>
        /// <returns>True, если конфигурация найдена; иначе false.</returns>
        public bool TryGetConfig(string Id, out T res) => _database.TryGetValue(Id, out res!);

        /// <summary>
        /// Возвращает коллекцию всех доступных идентификаторов конфигураций.
        /// </summary>
        /// <returns>Перечисление строк-идентификаторов.</returns>
        public IEnumerable<string> GetAllIds() => _database.Keys;

        /// <summary>
        /// Возвращает коллекцию всех загруженных объектов конфигурации.
        /// </summary>
        /// <returns>Перечисление объектов типа T.</returns>
        public IEnumerable<T> GetAllConfigs() => _database.Values;
    }
}
