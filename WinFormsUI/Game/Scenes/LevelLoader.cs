using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using WinFormsUI.Game.Scenes.LOCs;

namespace WinFormsUI.Game.Scenes
{
    /// <summary>
    /// Загрузчик конфигурации уровня из JSON-файлов. Поддерживает полиморфную десериализацию объектов уровня.
    /// </summary>
    public class LevelLoader
    {
        private static readonly JsonSerializerOptions options = new()
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { PolySupport } },
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Настраивает опции сериализации для поддержки полиморфизма типов BaseLOC.
        /// </summary>
        /// <param name="typeInfo">Информация о типе для настройки.</param>
        private static void PolySupport(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Type == typeof(BaseLOC))
            {
                typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "$type",
                    IgnoreUnrecognizedTypeDiscriminators = true,
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                    DerivedTypes = {
                        new JsonDerivedType(typeof(TowerLOC), "tower"),
                        new JsonDerivedType(typeof(PlatformLOC), "platform"),
                        new JsonDerivedType(typeof(PlayerLOC), "player"),
                        new JsonDerivedType(typeof(LadderLOC), "ladder"),
                        new JsonDerivedType(typeof(EffectSpawnerLOC), "effectSpawner"),
                        new JsonDerivedType(typeof(WeaponSpawnerLOC), "weaponSpawner"),
                        new JsonDerivedType(typeof(BoxLOC), "box"),
                    }
                };
            }
        }

        /// <summary>
        /// Загружает список объектов уровня из указанного файла конфигурации.
        /// </summary>
        /// <param name="path">Относительный путь к файлу внутри папки Assets/Config.</param>
        /// <returns>Перечисление загруженных объектов уровня или пустой список при ошибке.</returns>
        public static IEnumerable<BaseLOC> Load(string path)
        {
            if (!File.Exists(path))
            {
                Debug.WriteLine($"[Warn]: Config file {path} not. Config is empty.");
                return [];
            }

            List<BaseLOC>? configs = JsonSerializer.Deserialize<List<BaseLOC>>(File.ReadAllText(path), options);

            if (configs == null)
            {
                Debug.WriteLine($"[Warn]: Parser could not parse {path}. Config empty.");
                return [];
            }

            return configs;
        }
    }
}
