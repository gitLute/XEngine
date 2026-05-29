using Box2D.NET;
using WinFormsUI.Game.Box2D;
using WinFormsUI.Game.Config;
using WinFormsUI.Game.Player.Components;
using XEngine.Core.Base;
using XEngine.Core.Box2DCompat.Components;
using XEngine.Core.Common.Health;
using XEngine.Core.Scenery;
using static XEngine.Core.Box2DCompat.B2Helpers;

namespace WinFormsUI.Game.Player
{
    /// <summary>
    /// Утилита для создания сущностей игроков на основе конфигурации.
    /// Реализует паттерн Одиночка (Singleton).
    /// </summary>
    public class PlayerUtil
    {
        private readonly static float _r = 0.5f * GPlayerModel.PLAYER_SIZE.X;

        private readonly ConfigLoader<PlayerConfig> configLoader;
        private static readonly Lazy<PlayerUtil> _instance = new(() => new PlayerUtil());
        public static PlayerUtil Instance => _instance.Value;
        private PlayerUtil()
        {
            configLoader = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Config", "players.json"));
        }

        /// <summary>
        /// Создает и размещает сущность игрока на сцене.
        /// </summary>
        /// <param name="scene">Сцена, на которой создается игрок.</param>
        /// <param name="pos">Начальная позиция игрока.</param>
        /// <param name="name">Имя игрока, используемое для идентификации ввода.</param>
        /// <param name="playerConfigId">Идентификатор конфигурации игрока.</param>
        /// <returns>Созданная сущность игрока.</returns>
        /// <exception cref="ArgumentException">Выбрасывается, если конфигурация с указанным ID не найдена.</exception>
        public Entity CreatePlayer(GScene scene, B2Vec2 pos, string name, string playerConfigId)
        {
            if (!configLoader.TryGetConfig(playerConfigId, out var config)) throw new ArgumentException("player Id not found");

            var playerEntity = scene.SpawnEntity();
            playerEntity.Transform.Init(new(pos.X, pos.Y, 0), 0f);
            
            var bodyComp = playerEntity.AddComponent<GBox2DBody>()
                .Init(playerEntity.Transform)
                .SetType(B2BodyType.b2_dynamicBody)
                .SetMotinLocks(new B2MotionLocks { angularZ = true })
                .Build(scene.World.Id)
                .EnableCollisionCallback()
                .AttacShapes(CreateCollider)
                .AttacShapes(CreateFoot);

            bodyComp.OnCollisionEnter = GContacts.ContactParseAdd;
            bodyComp.OnCollisionExit = GContacts.ContactParseRemove;

            playerEntity.AddComponent<GHealth>()
                .Init(config.MaxHealth)
                .SetRegen(config.HealthRegenRate, config.HealthRegenDelay)
                .SetDeathCallback(e => e.MarkDelete(true))
                .IsRendered = true;

            playerEntity.AddComponent<GHealthRenderer>();

            playerEntity.AddComponent<GPlayer>()
                .Init(config)
                .SetName(name);

            return playerEntity;
        }

        private static void CreateCollider(B2BodyId bid)
        {
            B2Capsule capsule = MakeCapsule(new(new(-_r, 0), new(_r, GPlayerModel.PLAYER_SIZE.Y)));
            B2ShapeDef capsuleDef = B2Types.b2DefaultShapeDef();
            capsuleDef.density = 1f;
            capsuleDef.material.friction = 0.1f;
            capsuleDef.enableSensorEvents = true;
            capsuleDef.filter.categoryBits = (ulong)ContactFlags.PLAYER;
            capsuleDef.filter.maskBits = (ulong)ContactFlags.PLAYER_MASK;
            B2Shapes.b2CreateCapsuleShape(bid, capsuleDef, capsule);
        }

        private static void CreateFoot(B2BodyId bid)
        {
            B2ShapeDef circleSensorDef = B2Types.b2DefaultShapeDef();
            circleSensorDef.isSensor = true;
            circleSensorDef.enableSensorEvents = true;
            circleSensorDef.filter.categoryBits = (ulong)ContactFlags.FOOT;
            circleSensorDef.filter.maskBits = (ulong)ContactFlags.SOLID;
            B2Shapes.b2CreateCircleShape(bid, circleSensorDef, new(new(0, _r * 0.9f), _r * 0.95f));
        }

        /// <summary>
        /// Возвращает список всех доступных идентификаторов конфигураций игроков.
        /// </summary>
        /// <returns>Перечисление строк-идентификаторов.</returns>
        public IEnumerable<string> GetIds() => configLoader.GetAllIds();
    }
}
