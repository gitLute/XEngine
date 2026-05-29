using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XEngine.Core.Base;
using XEngine.Core.Scenery;

namespace XEngine.Core.Common.Health
{
    /// <summary>
    /// Система обновления логики здоровья. Вызывает метод Update у всех активных компонентов GHealth в сцене.
    /// </summary>
    public class HealthSystem : IGameSystem
    {
        public int Priority => 200;

        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Обновляет состояние здоровья всех объектов в сцене.
        /// </summary>
        /// <param name="_scene">Текущая сцена.</param>
        /// <param name="_dt">Время, прошедшее с последнего кадра.</param>
        public void Update(GScene _scene, float _dt)
        {
            foreach (var (_, h) in _scene.Query<GHealth>()) h.Update(_dt);
        }
    }
}
