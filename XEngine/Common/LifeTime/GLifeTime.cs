using XEngine.Core.Base;

namespace XEngine.Core.Common.LifeTime
{
    /// <summary>
    /// Компонент времени жизни объекта. Автоматически удаляет владельца по истечении заданного интервала и поддерживает эффект мигания перед исчезновением.
    /// </summary>
    public sealed class GLifeTime : GameComponent, IDisposable
    {
        public readonly GameTimer LifeTimer = new(float.MaxValue);
        public bool IsBlinking = false;

        /// <summary>
        /// Инициализирует таймер жизни с заданной длительностью.
        /// Регистрирует таймер в сцене и настраивает автоматическое удаление объекта при завершении.
        /// </summary>
        /// <param name="lifeTime">Длительность жизни в секундах.</param>
        public GLifeTime Init(float lifeTime)
        {
            LifeTimer.Duration = lifeTime;
            Owner.Scene.RegisterTimer(LifeTimer);
            LifeTimer.Start();
            LifeTimer.OnComplete += () => Owner.MarkDelete();
            return this;
        }

        /// <summary>
        /// Отменяет регистрацию таймера в сцене при уничтожении компонента.
        /// </summary>
        public void Dispose()
        {
            Owner.Scene.UnregisterTimer(LifeTimer);
        }
    }
}
