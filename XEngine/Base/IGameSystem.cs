using XEngine.Core.Scenery;

namespace XEngine.Core.Base
{
    /// <summary>
    /// Интерфейс игровой системы. Определяет порядок выполнения и метод обновления логики.
    /// </summary>
    public interface IGameSystem
    {
        /// <summary>
        /// Приоритет выполнения системы. Системы с меньшим приоритетом выполняются раньше.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Флаг активности системы. Неактивные системы пропускаются при обновлении.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Метод обновления логики системы. Вызывается каждый кадр.
        /// </summary>
        /// <param name="_scene">Текущая сцена.</param>
        /// <param name="_dt">Время, прошедшее с последнего обновления.</param>
        void Update (GScene _scene, float _dt);
    }
}
