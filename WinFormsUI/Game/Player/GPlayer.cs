using OpenTK.Mathematics;
using WinFormsUI.Game.Combat.Projectiles;
using WinFormsUI.Game.Combat.Weapons;
using WinFormsUI.Game.Drop;
using WinFormsUI.Game.Player.Components;
using WinFormsUI.Game.Player.Contol;
using WinFormsUI.Game.Player.PlayerStates;
using WinFormsUI.Game.Player.Stats;
using XEngine.Core.Base;
using XEngine.Core.Box2DCompat.Components;
using XEngine.Core.Common;
using XEngine.Core.Common.Transform;
using XEngine.Core.Scenery;
using XEngine.Core.Utils;

namespace WinFormsUI.Game.Player
{
    public sealed class GPlayer : GameComponent, IDisposable
    {
        public bool IsRightFacing { get; set; } = true;

        public readonly GameTimer JumpTimer = new(0.3f);

        private IPlayerState? _state = new GroundedState();
        public GWeaponry Weaponry { get; private set; } = null!;
        public GPlayerModel Model { get; private set; } = null!;
        public GEffects Effects { get; private set; } = null!;
        public GContacts Contacts { get; private set; } = null!;
        public GBox2DBody Body { get; private set; } = null!;

        public PlayerControl Control { get; private set; } = null!;

        /// <summary>
        /// Инициализирует все подкомпоненты игрока на основе конфигурации.
        /// </summary>
        /// <param name="config">Конфигурация игрока.</param>
        /// <returns>Текущий экземпляр компонента.</returns>
        public GPlayer Init(PlayerConfig config)
        {
            Body = Owner.Get<GBox2DBody>()!;

            Weaponry = Owner.AddComponent<GWeaponry>().Init(config.StartWeaponId);

            Model = Owner.AddComponent<GPlayerModel>().Init(config);
            Model.UpdatePockets(Weaponry);

            Effects = Owner.AddComponent<GEffects>().SetSource(new PlayerStats(config));

            Contacts = Owner.AddComponent<GContacts>()
                .Register(Box2D.ContactFlags.SOLID)
                .Register(Box2D.ContactFlags.LADDER)
                .Register(Box2D.ContactFlags.ITEM_HITBOX, true);

            Control = new PlayerControl(Owner.Scene.Input);
            IsRightFacing = Model.SetFacingDiecration(1);

            Owner.Scene.RegisterTimer(JumpTimer);
            return this;
        }

        /// <summary>
        /// Передает ввод текущему активному состоянию машины состояний.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        /// <param name="dt">Время, прошедшее с последнего кадра.</param>
        public void ProcessInput(GScene scene, float dt) => _state?.ProcessInput(this, scene, dt);


        /// <summary>
        /// Устанавливает имя игрока и обновляет имя в контроллере ввода.
        /// </summary>
        /// <param name="name">Новое имя игрока.</param>
        /// <returns>Текущий экземпляр компонента.</returns>
        public GPlayer SetName(string name)
        {
            Control.Name = name;
            return this;
        }

        /// <summary>
        /// Выполняет выстрел из указанного оружия, создавая снаряды на сцене.
        /// </summary>
        /// <param name="weapon">Оружие, из которого производится выстрел.</param>
        public void Shoot(WeaponItem weapon)
        {
            if (weapon == null || weapon.CurrentAmmo <= 0 || !weapon.FireTimer.IsFinished) return;
            weapon.FireTimer.Start();
            weapon.CurrentAmmo--;

            var scene = Owner.Scene;
            float angle = Model.WeaponEntity.Transform.Rotation;
            var MuzzleOffset = weapon.MuzzleOffset;
            if (MathF.Abs(angle) > MathF.PI / 2) MuzzleOffset.Y *= -1;
            MuzzleOffset = MathUtils.Rotate(MuzzleOffset, -angle);

            Vector2 MuzzlePoint = Owner.Transform.RelativePosition2D + Model.WeaponPosition + MuzzleOffset;

            for (int i = 0; i < weapon.Shots; i++)
            {
                Vector2 spread = MathUtils.FromPolar(scene.GetR01() * weapon.Spread, scene.GetR01() * MathF.Tau);
                Vector2 vel = MathUtils.FromPolar(weapon.InitialVelocity, angle);
                scene.Schedule(() => ProjectileCreationUtils.CreateProjectile(weapon.ProjectileId, scene, MuzzlePoint, vel + spread)?
                    .SetSource(Owner.Id));
            }
        }

        /// <summary>
        /// Выбрасывает указанное оружие на сцену как подбираемый предмет.
        /// </summary>
        /// <param name="weapon">Оружие для выброса.</param>
        /// <param name="expirationTime">Время жизни предмета на сцене.</param>
        /// <param name="canPickUp">Флаг возможности подбора.</param>
        public void Drop(WeaponItem weapon, float expirationTime = float.MaxValue, bool canPickUp = false)
        {
            if (weapon == null) return;

            var scene = Owner.Scene;
            float dir = Model.WeaponEntity.Transform.Rotation;
            var vel = MathUtils.FromPolar(3, dir);

            var Drop = DropBuilder
                .Init(weapon)
                .CanPickup(canPickUp)
                .SetExpirationTime(expirationTime)
                .SetVelocity(vel, Owner.Scene.GetR_11() * 10);
            var pos = Owner.Transform.RelativePosition2D + Model.WeaponPosition;
            scene.Schedule(() => Drop.Spawn(scene, new TransformValues() { Position = (pos.X, pos.Y, 0), Rotation = dir }));
        }

        /// <summary>
        /// Применяет импульс к физическому телу для горизонтального движения.
        /// </summary>
        /// <param name="direction">Направление движения (-1 или 1).</param>
        /// <param name="dt">Время, прошедшее с последнего кадра.</param>
        /// <param name="speedscale">Множитель скорости.</param>
        public void Move(float direction, float dt, float speedscale = 1.0f)
        {
            var delta = MathUtils.LimitedStep(Body.LinearVelocity.X, Stats.TopSpeed * direction, Stats.Acceleration * dt);
            Body.ApplyImpulse(x: delta * speedscale);
        }

        public IPlayerStats Stats => Effects.GetStats();

        /// <summary>
        /// Переключает машину состояний игрока на новый тип состояния.
        /// </summary>
        /// <typeparam name="T">Тип нового состояния.</typeparam>
        /// <param name="scene">Текущая сцена.</param>
        public void SwitchTo<T>(GScene scene) where T : IPlayerState, new()
        {
            _state?.Exit(this, scene);
            _state = new T();
            _state.Enter(this, scene);
        }

        /// <summary>
        /// Освобождает ресурсы, unregistering таймеры при удалении сущности.
        /// </summary>
        public void Dispose()
        {
            Owner.Scene.UnregisterTimer(JumpTimer);
        }
    }
}
