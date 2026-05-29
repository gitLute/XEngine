namespace XEngine.Core.Base
{
    /// <summary>
    /// Базовый класс для всех компонентов игровых объектов. Содержит ссылку на владельца-сущность.
    /// </summary>
    public abstract class GameComponent
    {
        /// <summary>
        /// Сущность, которой принадлежит данный компонент.
        /// </summary>
        public Entity Owner { get; internal set; } = null!;
    }
}
