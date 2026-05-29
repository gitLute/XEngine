using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Text.Json.Serialization;
using XEngine.Core.Input.InputAxis;

namespace XEngine.Core.Config
{
    /// <summary>
    /// Конфигурация игры. Содержит настройки отладки, раскладку клавиш и параметры осей ввода.
    /// </summary>
    public class GameConfig
    {
        /// <summary>
        /// Флаг включения режима отладки.
        /// </summary>
        [JsonPropertyName("debug")]
        public bool Debug { get; set; } = true;

        /// <summary>
        /// Словарь сопоставления действий и клавиш клавиатуры.
        /// </summary>
        [JsonPropertyName("keymap")]
        public Dictionary<string, Keys> KeyMap { get; set; } = [];

        /// <summary>
        /// Словарь настроек осей ввода.
        /// </summary>
        [JsonPropertyName("axes")]
        public Dictionary<string, AxisSettings> Axes { get; set; } = [];
    }
}
