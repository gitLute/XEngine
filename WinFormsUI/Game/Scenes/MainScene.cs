using OpenTK.Mathematics;
using WinFormsUI.Game.Combat.Projectiles;
using WinFormsUI.Game.Player;
using WinFormsUI.Game.Player.Stats;
using WinFormsUI.Game.Scenes.PlayerSpawner;
using XEngine.Core;
using XEngine.Core.Base;
using XEngine.Core.Box2DCompat.Systems;
using XEngine.Core.Common.Health;
using XEngine.Core.Common.LifeTime;
using XEngine.Core.Common.Sprite;
using XEngine.Core.Common.Trace;
using XEngine.Core.Graphics.OpenGL;
using XEngine.Core.Scenery;

namespace WinFormsUI.Game.Scenes
{
    /// <summary>
    /// Основная игровая сцена. Управляет логикой матча, спавном игроков и настройкой систем обновления.
    /// </summary>
    internal class MainScene(GameEngine _engine, string AId, string BId, string ArenaPath) : GScene(_engine)
    {
        public string WinnerName = "";

        /// <summary>
        /// Настраивает и регистрирует все необходимые системы для игровой логики.
        /// </summary>
        private void SetupSystems()
        {
            AddSystem(new Box2DContactSystem()); // 50
            AddSystem(new CameraSystem(10)); // 100
            AddSystem(new PlayersSystem(Input)); // 100

            // 200
            AddSystem(new LifeTimerSystem());
            AddSystem(new TraceSystem());
            AddSystem(new HealthSystem());
            AddSystem(new EffectsSystem());
            AddSystem(new ProjectileSystem());

            AddSystem(new Box2DTransformSync(12)); // 400

            AddSystem(new WinSystem()); // 700
        }

        /// <summary>
        /// Создает фоновые слои сцены из набора текстур.
        /// </summary>
        /// <param name="folder">Путь к папке с текстурами фона.</param>
        private void CreateBG(string folder)
        {
            Texture2D tex;
            Entity e;

            for (int i = 1; i <= 4; i++)
            {
                float j = 6 - i;
                string path = Path.Combine(folder, $"{i}.png");
                tex = Assets.LoadTexture(path);
                e = SpawnEntity();

                e.Transform.Init(new(0, 0, -10 * j), 0);

                e.AddComponent<GSprite>()
                    .SetTexture(tex)
                    .SetSizingPolicy(SizingPolicy.Source)
                    .SetSize(Vector2.One * j);
            }
        }

        /// <summary>
        /// Выполняет начальную загрузку сцены: создает системы, фон, камеру и объекты уровня из конфигурации.
        /// </summary>
        public override void Load()
        {
            AddSystem(new PlayerSpawnSystem(AId, BId));

            SetupSystems();
            CreateBG("Environment\\Background");
            Camera.Owner.Transform.Position2D = new(0, 8);

            foreach (var bloc in LevelLoader.Load(ArenaPath)) bloc.Spawn(this);
        }

        /// <summary>
        /// Очищает сцену и освобождает ресурсы при выгрузке.
        /// </summary>
        public override void Unload()
        {
            ClearScene();
            base.Unload();
        }
    }
}
