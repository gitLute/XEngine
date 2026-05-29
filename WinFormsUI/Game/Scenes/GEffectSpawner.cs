using Box2D.NET;
using OpenTK.Mathematics;
using WinFormsUI.Game.Box2D;
using WinFormsUI.Game.Player;
using WinFormsUI.Game.Player.Stats;
using WinFormsUI.Game.Player.Stats.Effects;
using XEngine.Core.Base;
using XEngine.Core.Box2DCompat;
using XEngine.Core.Box2DCompat.Components;
using XEngine.Core.Common;
using XEngine.Core.Common.Sprite;
using XEngine.Core.Scenery;
using static XEngine.Core.Box2DCompat.B2Helpers;

namespace WinFormsUI.Game.Scenes
{
    /// <summary>
    /// Компонент спавнера эффектов. Периодически создает случайные баффы (ускорение, прыжок, защита) в точке расположения объекта.
    /// </summary>
    internal class GEffectSpawner : GameComponent, IDisposable
    {
        private static readonly string[] effects = ["jumpBoost", "speedBoost", "defenceBoost"];
        public GameTimer SpawnTimer = new(20, true);

        /// <summary>
        /// Инициализирует таймер спавна и регистрирует его в сцене.
        /// </summary>
        public GEffectSpawner Init()
        {
            Owner.Scene.RegisterTimer(SpawnTimer);
            SpawnTimer.OnComplete += SpawnRandom;
            SpawnTimer.Start();
            SpawnTimer.ForceEnd();
            return this;
        }

        /// <summary>
        /// Создает случайный эффект и размещает его на сцене.
        /// </summary>
        private void SpawnRandom()
        {
            var scene = Owner.Scene;
            scene.Schedule(() =>
            {
                var effId = effects[scene.Random.Next(effects.Length)];
                var (effect, tex) = GetEffect(effId);
                effect.Duration(GetDuration());
                var effEntity = CreateEffect(scene, Owner.Transform.Position, 1, effect);
                effEntity.AddComponent<GSprite>()
                    .SetSizingPolicy(SizingPolicy.Source)
                    .SetTexture(scene.Assets.LoadTexture(tex));
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

        private float GetIntesity() => float.Lerp(1.5f, 2.5f, Owner.Scene.GetR01());
        private float GetDuration() => float.Lerp(5f, 10f, Owner.Scene.GetR01());

        private (Effect, string) GetEffect(string type) => type switch
        {
            "jumpBoost" => (new JumpBoostEffect(GetIntesity()), "Effects\\JumpBoost.png"),
            "speedBoost" => (new SpeedBoostEffect(GetIntesity()), "Effects\\SpeedBoost.png"),
            "defenceBoost" => (new ArmorBoostEffect(GetIntesity()), "Effects\\DefenceBoost.png"),
            _ => (new SpeedBoostEffect(GetIntesity()), "Effects\\SpeedBoost.png")
        };

        private static Entity CreateEffect(GScene scene, Vector3 pos, float ColliderSize, Effect effect)
        {
            var pickupEffect = scene.SpawnEntity();
            pickupEffect.Transform.Init(pos, 0);
            pickupEffect.AddComponent<GHeldEffect>().Init(effect);
            var bodyComp = pickupEffect.AddComponent<GBox2DBody>()
                .Init(pickupEffect.Transform)
                .SetType(B2BodyType.b2_staticBody)
                .Build(scene.World.Id)
                .EnableCollisionCallback()
                .AttacShapes(bid =>
                {
                    B2ShapeDef circleSensorDef = B2Types.b2DefaultShapeDef();
                    circleSensorDef.isSensor = true;
                    circleSensorDef.enableSensorEvents = true;
                    circleSensorDef.filter.categoryBits = (ulong)ContactFlags.EFFECT;
                    circleSensorDef.filter.maskBits = (ulong)ContactFlags.PLAYER;
                    B2Shapes.b2CreateCircleShape(bid, circleSensorDef, new(new(0, 0), ColliderSize * 0.5f));
                });
            bodyComp.OnCollisionEnter = EffectTouchCB;

            return pickupEffect;
        }

        private static void EffectTouchCB(ContactWrapper ev)
        {
            if (ev.IsSensor && CheckFlag(ev.ShapeIdA, (ulong)ContactFlags.EFFECT))
            {
                Effect effect = ev.EntityA!.Get<GHeldEffect>()!.Effect;
                ev.GBodyB!.Owner.Get<GPlayer>()?.Effects.Add(effect);
                ev.EntityA?.MarkDelete();
            }
        }
    }
}
