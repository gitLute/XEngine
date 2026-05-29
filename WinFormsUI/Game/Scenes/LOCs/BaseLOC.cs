using OpenTK.Mathematics;
using XEngine.Core.Base;
using XEngine.Core.Scenery;
using XEngine.Core.Utils.JSONConverters;

namespace WinFormsUI.Game.Scenes.LOCs
{
    /// <summary>
    /// Базовый класс для объектов уровня (Level Object Config). Определяет позицию, вращение и метод создания сущности на сцене.
    /// </summary>
    public abstract class BaseLOC
    {
        /// <summary>
        /// Позиция объекта в мире.
        /// </summary>
        [JsonVector3] public Vector3 Pos { get; set; } = Vector3.Zero;

        /// <summary>
        /// Угол поворота объекта (в радианах).
        /// </summary>
        public float Rotation { get; set; } = 0;

        /// <summary>
        /// Создает игровую сущность на основе конфигурации объекта.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        /// <returns>Созданная сущность.</returns>
        public abstract Entity Spawn(GScene scene);
    }
}
