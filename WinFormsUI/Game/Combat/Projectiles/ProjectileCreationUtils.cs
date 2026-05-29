using Box2D.NET;
using OpenTK.Mathematics;
using System.Diagnostics;
using WinFormsUI.Game.Box2D;
using WinFormsUI.Game.Player;
using XEngine.Core.Base;
using XEngine.Core.Box2DCompat;
using XEngine.Core.Box2DCompat.Components;
using XEngine.Core.Common.Health;
using XEngine.Core.Common.LifeTime;
using XEngine.Core.Common.Sprite;
using XEngine.Core.Common.Trace;
using XEngine.Core.Scenery;
using static Box2D.NET.B2Shapes;
using static XEngine.Core.Box2DCompat.B2Helpers;

namespace WinFormsUI.Game.Combat.Projectiles
{
    /// <summary>
    /// Утилита для создания экземпляров снарядов на основе конфигурации.
    /// </summary>
    internal static class ProjectileCreationUtils
    {
        /// <summary>
        /// Создает и размещает снаряд на сцене.
        /// </summary>
        /// <param name="projectileId">Идентификатор типа снаряда из конфигурации.</param>
        /// <param name="scene">Сцена, на которой создается снаряд.</param>
        /// <param name="pos">Начальная позиция.</param>
        /// <param name="vel">Начальная скорость.</param>
        /// <returns>Созданная сущность или null, если конфигурация не найдена.</returns>
        public static Entity? CreateProjectile(string projectileId, GScene scene, Vector2 pos, Vector2 vel)
        {
            if (!ProjectileConfigLoader.Instance.TryGetConfig(projectileId, out var projConfig))
            {
                Debug.WriteLine($"[Warn]: Could not spawn projectile, {projectileId} is undefined");
                return null;
            }

            var e = scene.SpawnEntity();

            e.Transform.Init(new(pos.X, pos.Y, 0), MathF.Atan2(vel.Y, vel.X));

            var eTrace = scene.SpawnEntity();
            eTrace.AddComponent<GTrace>().Init(new(1, 0.5f, 0), 10).IsAggressive = true;
            eTrace.Transform.SetParent(e.Transform);

            e.AddComponent<GSprite>()
                .SetTexture(scene.Assets.LoadTexture(projConfig.TexturePath))
                .SetSizingPolicy(SizingPolicy.Source)
                .SetSize(Vector2.One * projConfig.Size);

            e.AddComponent<GProjectile>().Init(projConfig.Damage);

            e.AddComponent<GLifeTime>().Init(projConfig.MaxLifetime);

            var bodyComp = e.AddComponent<GBox2DBody>().Init(e.Transform)
                .SetType(B2BodyType.b2_dynamicBody)
                .SetBulletFlag(true)
                .Build(scene.World.Id)
                .EnableCollisionCallback()
                .AttacShapes(CreateCollider(projConfig));

            bodyComp.GravityScale = 0.1f;
            bodyComp.LinearVelocity = new(vel.X, vel.Y);
            bodyComp.OnCollisionEnter = BulletCollisionCallback;

            return e;
        }

        private static Action<B2BodyId> CreateCollider(ProjectileConfig config)
        {
            return bid =>
            {
                B2ShapeDef segSensorDef = B2Types.b2DefaultShapeDef();
                segSensorDef.isSensor = true;
                segSensorDef.enableSensorEvents = true;
                segSensorDef.filter.categoryBits = (ulong)ContactFlags.PROJECTILE;
                segSensorDef.filter.maskBits = (ulong)(ContactFlags.SOLID | ContactFlags.PLAYER);
                B2Segment seg = new(new(-config.Size * 0.5f, 0), new(config.Size * 0.5f, 0));
                b2CreateSegmentShape(bid, segSensorDef, seg);
            };
        }

        private const float MIN_DAMAGE = 0.1f;
        private static void BulletCollisionCallback(ContactWrapper ev)
        {
            if (!CheckFlag(ev.ShapeIdA, (ulong)ContactFlags.PROJECTILE)) return;
            if (!ev.IsSensor) return;
            if (ev.EntityA!.SourceId == ev.EntityB?.Id) return;

            var projectile = ev.EntityA!.Get<GProjectile>()!;
            if (ev.EntityB?.TryGet<GHealth>(out var health) == true)
            {
                float armor = ev.EntityB.Get<GPlayer>()?.Stats.Armor ?? 0;
                float damage = MathF.Max(MIN_DAMAGE, projectile.Damage - armor);
                health.DealDamage(damage);
            }
            projectile.Owner.MarkDelete();
        }
    }
}
