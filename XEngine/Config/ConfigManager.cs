using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace XEngine.Core.Config
{
    /// <summary>
    /// Менеджер конфигурации. Отвечает за сохранение и загрузку настроек игры в формате JSON.
    /// </summary>
    public class ConfigManager
    {
        private readonly JsonSerializerOptions _option = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Сохраняет конфигурацию игры в файл по указанному пути.
        /// </summary>
        /// <param name="path">Путь к файлу.</param>
        /// <param name="config">Объект конфигурации для сохранения.</param>
        public void Save(string path, GameConfig config)
        {
            string json = JsonSerializer.Serialize(config, _option);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Загружает конфигурацию игры из файла. Если файл не существует, возвращает настройки по умолчанию.
        /// </summary>
        /// <param name="path">Путь к файлу.</param>
        /// <returns>Загруженная или новая конфигурация.</returns>
        public GameConfig Load(string path)
        {
            if (!File.Exists(path)) return new GameConfig();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GameConfig>(json, _option) ?? new GameConfig();
        }
    }
}
