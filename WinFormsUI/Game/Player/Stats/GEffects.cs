using WinFormsUI.Game.Player.Stats.Effects;
using XEngine.Core.Base;

namespace WinFormsUI.Game.Player.Stats
{
    /// <summary>
    /// Компонент, управляющий стеком временных эффектов, влияющих на характеристики игрока.
    /// Реализует цепочку декораторов для динамического расчета итоговых статов.
    /// </summary>
    public class GEffects : GameComponent
    {
        private readonly List<Effect> _effects = [];
        private IPlayerStats _sourceStats = null!;
        private IPlayerStats _cacheStats = null!;
        private bool _isDirty = true;

        /// <summary>
        /// Устанавливает базовые характеристики, которые будут модифицироваться эффектами.
        /// </summary>
        /// <param name="sourceStats">Базовые характеристики.</param>
        /// <returns>Текущий экземпляр компонента.</returns>
        public GEffects SetSource(IPlayerStats sourceStats)
        {
            _sourceStats = sourceStats;
            _isDirty = true;
            return this;
        }

        /// <summary>
        /// Добавляет новый эффект в стек. Если эффект такого типа уже существует, продлевает его длительность.
        /// </summary>
        /// <typeparam name="T">Тип эффекта.</typeparam>
        /// <param name="effect">Экземпляр эффекта.</param>
        /// <returns>Текущий экземпляр компонента.</returns>
        public GEffects Add<T>(T effect) where T : Effect
        {
            if (TryGetEffectOfType<T>(out var _oldEffect)) _oldEffect.Timer.Duration += effect.Timer.Duration;
            else
            {
                Owner.Scene.RegisterTimer(effect.Timer);
                effect.Timer.Start();
                _effects.Add(effect);
                _isDirty = true;
            }
            return this;
        }

        private bool TryGetEffectOfType<T>(out T effect) where T : Effect
        {
            effect = null!;
            var effectsOftype = _effects.OfType<T>();
            if (effectsOftype.Any())
            {
                effect = effectsOftype.First();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Удаляет указанный эффект из стека и останавливает его таймер.
        /// </summary>
        /// <param name="effect">Эффект для удаления.</param>
        /// <returns>Текущий экземпляр компонента.</returns>
        public GEffects Remove(Effect effect)
        {
            Owner.Scene.UnregisterTimer(effect.Timer);
            _effects.Remove(effect);
            _isDirty = true;
            return this;
        }

        /// <summary>
        /// Возвращает актуальные характеристики игрока с учетом всех активных эффектов.
        /// </summary>
        /// <returns>Объект с итоговыми характеристиками.</returns>
        public IPlayerStats GetStats()
        {
            if (_isDirty) Recalculate();
            return _cacheStats;
        }

        /// <summary>
        /// Проверяет таймеры эффектов и удаляет те, время действия которых истекло.
        /// </summary>
        public void WearoffEffects()
        {
            if (!_effects.Any(eff => eff.Timer.IsFinished)) return;
            var effects = _effects.Where(eff => eff.Timer.IsFinished).ToList();
            foreach (var effect in effects) Remove(effect);
        }

        private void Recalculate()
        {
            _isDirty = false;
            IPlayerStats res = _sourceStats;
            foreach (Effect effect in _effects) res = effect.SetBase(res);
            _cacheStats = res;
        }
    }
}
