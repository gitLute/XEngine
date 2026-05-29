namespace XEngine.Core.Common
{
    /// <summary>
    /// Таймер игрового цикла. Отслеживает прошедшее время и вызывает событие по завершении интервала.
    /// </summary>
    public class GameTimer
    {
        public bool IsRunning { get; internal set; }
        public float Elapsed { get; internal set; }
        public bool IsLooped;
        public float Duration;

        public GameTimer(float duration, bool isLooped = false)
        {
            Duration = duration;
            IsLooped = isLooped;
            Reset();
        }

        /// <summary>
        /// Внутренний метод для обновления таймера
        /// </summary>
        internal void Tick(float deltaTime)
        {
            if (!IsRunning) return;
            Elapsed += deltaTime;
            if (Elapsed >= Duration)
            {
                Elapsed = Duration;
                IsRunning = false;
                OnComplete?.Invoke();
                if (IsLooped) Start();
            }
        }

        /// <summary>
        /// Запускает таймер с нуля.
        /// </summary>
        public GameTimer Start()
        {
            Elapsed = 0;
            IsRunning = true;
            return this;
        }

        /// <summary>
        /// Сбрасывает таймер в начальное состояние (останавливает).
        /// </summary>
        public GameTimer Reset()
        {
            Elapsed = 0;
            IsRunning = false;
            return this;
        }

        /// <summary>
        /// Принудительно завершает таймер, устанавливая прошедшее время равным длительности.
        /// </summary>
        public GameTimer ForceEnd()
        {
            Elapsed = Duration;
            return this;
        }

        public float Left => Duration - Elapsed;
        public bool IsFinished => Elapsed >= Duration;
        public float Progress => Elapsed / Duration;

        public event Action? OnComplete;
    }
}