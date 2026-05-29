using WinFormsUI.Game.Combat.Weapons;
using WinFormsUI.Game.Drop;
using XEngine.Core.Base;
using XEngine.Core.Common;

namespace WinFormsUI.Game.Scenes
{
    /// <summary>
    /// Компонент спавнера оружия. Периодически создает случайное оружие в точке расположения объекта.
    /// </summary>
    internal class GWeaponSpawner : GameComponent, IDisposable
    {
        public GameTimer SpawnTimer = new(20, true);
        public float ExpirationTime = 10;

        /// <summary>
        /// Инициализирует таймер спавна и регистрирует его в сцене.
        /// </summary>
        public GWeaponSpawner Init()
        {
            Owner.Scene.RegisterTimer(SpawnTimer);
            SpawnTimer.OnComplete += SpawnRandom;
            SpawnTimer.Start();
            SpawnTimer.ForceEnd();
            return this;
        }

        /// <summary>
        /// Создает случайное оружие из доступных типов и сбрасывает его как подбираемый предмет.
        /// </summary>
        private void SpawnRandom()
        {
            Owner.Scene.Schedule(() =>
            {
                var l = WeaponUtils.Instance.GetIds().ToList();
                var id = l[Owner.Scene.Random.Next(l.Count)];
                if (WeaponUtils.Instance.TryCreateWeapon(id, out var w))
                {
                    w.Init(Owner.Scene);
                    DropBuilder.Init(w)
                        .SetExpirationTime(ExpirationTime)
                        .CanPickup(true)
                        .SetVelocity(new(0, 3), 0)
                        .Spawn(Owner.Scene, Owner.Transform.Position);
                }
            });
        }

        /// <summary>
        /// Освобождает ресурсы таймера при уничтожении компонента.
        /// </summary>
        public void Dispose()
        {
            SpawnTimer.OnComplete -= SpawnRandom;
            Owner.Scene.UnregisterTimer(SpawnTimer);
        }
    }
}
