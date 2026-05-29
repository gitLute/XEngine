using XEngine.Core.Base;
using XEngine.Core.Utils;

namespace XEngine.Core.Common.Health
{
    /// <summary>
    /// Компонент здоровья игрового объекта. Управляет текущим и максимальным здоровьем, регенерацией и обработкой смерти.
    /// </summary>
    public sealed class GHealth : GameComponent, IDisposable
    {
        private readonly GameTimer RegenDelay = new(float.MaxValue);
        public float MaxHealth { get; private set; } = 0;
        public float HealthRegen { get; private set; } = 0;
        public float Health { get; private set; } = 0;
        public bool IsRendered = false;

        private float _dHealth = 0;
        private const float DHFR = 10f; // Display health fill rate

        public float DisplayRatio => _dHealth / MaxHealth;
        public float DisplayLeftRatio => 1 - _dHealth / MaxHealth;
        public bool CanRegenerate => RegenDelay.IsFinished;

        private Action<Entity>? _onDeath = null;

        /// <summary>
        /// Инициализирует компонент с заданным максимальным запасом здоровья.
        /// </summary>
        /// <param name="maxHealth">Максимальное количество очков здоровья.</param>
        public GHealth Init(float maxHealth)
        {
            _dHealth = Health = MaxHealth = maxHealth;
            Owner.Scene.RegisterTimer(RegenDelay);
            return this;
        }

        /// <summary>
        /// Настраивает параметры регенерации здоровья: скорость восстановления и задержку после получения урона.
        /// </summary>
        /// <param name="healthRegenRate">Скорость восстановления здоровья в секунду.</param>
        /// <param name="healthRegenDelay">Задержка перед началом регенерации (в секундах).</param>
        public GHealth SetRegen(float healthRegenRate, float healthRegenDelay)
        {
            HealthRegen = healthRegenRate;
            RegenDelay.Duration = healthRegenDelay;
            return this;
        }

        /// <summary>
        /// Устанавливает обратный вызов, который выполняется при смерти объекта (когда здоровье достигает нуля).
        /// </summary>
        /// <param name="onDeath">Делегат, принимающий владельца компонента.</param>
        public GHealth SetDeathCallback(Action<Entity> onDeath)
        {
            _onDeath = onDeath;
            return this;
        }

        /// <summary>
        /// Обновляет состояние здоровья: применяет регенерацию и плавно интерполирует отображаемое значение для анимации.
        /// </summary>
        /// <param name="dt">Время, прошедшее с последнего кадра.</param>
        public void Update(float dt)
        {
            if (CanRegenerate) Health = MathUtils.MoveToward(Health, MaxHealth, HealthRegen * dt);
            _dHealth = float.Lerp(_dHealth, Health, dt * DHFR);
        }

        /// <summary>
        /// Наносит указанное количество урона объекту. Если здоровье падает до нуля или ниже, вызывает событие смерти.
        /// </summary>
        /// <param name="amount">Количество наносимого урона.</param>
        public void DealDamage(float amount)
        {
            RegenDelay.Start();
            Health -= amount;
            if (Health <= 0)
            {
                Health = 0;
                _onDeath?.Invoke(Owner);
            }
        }

        /// <summary>
        /// Освобождает ресурсы таймера регенерации при уничтожении компонента.
        /// </summary>
        public void Dispose()
        {
            Owner.Scene.UnregisterTimer(RegenDelay);
        }
    }
}
