using System;
using System.Diagnostics;
using XEngine.Core.Base;
using XEngine.Core.Common.Sprite;
using XEngine.Core.Scenery;

namespace XEngine.Core.Common.LifeTime
{
    /// <summary>
    /// Система визуального эффекта мигания для объектов с ограниченным временем жизни.
    /// Изменяет прозрачность спрайта в зависимости от оставшегося времени.
    /// </summary>
    public class LifeTimerSystem : IGameSystem
    {
        public int Priority => 200;

        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Обновляет прозрачность всех мигающих спрайтов в сцене.
        /// </summary>
        /// <param name="_scene">Текущая сцена.</param>
        /// <param name="_dt">Время, прошедшее с последнего кадра.</param>
        public void Update(GScene _scene, float _dt)
        {
            foreach (var (_, t, s) in _scene.Query<GLifeTime, GSprite>((_, t, _) => t.IsBlinking))
                s.SetAlpha(BlinkAlpha(t.LifeTimer));
        }

        private const float StartAlpha = 1;
        private const float EndAlpha = 0;
        private const float StartHz = 0.5f;
        private const float EndHz = 3;

        private static readonly Func<float, float, float, float> InterAlpha = float.Lerp;
        private static readonly Func<float, float, float, float> InterHz = float.Lerp;

        /// <summary>
        /// Вычисляет текущий коэффициент прозрачности (альфа-канал) на основе прогресса таймера.
        /// </summary>
        /// <param name="t">Таймер жизненного цикла.</param>
        /// <returns>Значение прозрачности от 0 до 1.</returns>
        private static float BlinkAlpha(GameTimer t)
        {
            float currentHz = InterHz(StartHz, EndHz, t.Progress);
            float minAlpha = InterAlpha(StartAlpha, EndAlpha, t.Progress);
            float raw = Math.Abs(MathF.Sin(t.Elapsed * MathF.PI * currentHz));

            return float.Lerp(minAlpha, 1, raw);
        }
    }
}
