using Box2D.NET;
using OpenTK.Mathematics;
using WinFormsUI.Game.Combat.Weapons;
using XEngine.Core.Base;
using XEngine.Core.Common;
using XEngine.Core.Common.Sprite;
using XEngine.Core.Graphics.OpenGL;
using XEngine.Core.Utils;

namespace WinFormsUI.Game.Player.Components
{
    /// <summary>
    /// Компонент визуальной модели игрока.
    /// Управляет анимацией, отображением оружия и частей тела.
    /// </summary>
    public sealed class GPlayerModel : GameComponent, IDisposable
    {
        private const float depth = 0.3f;
        private const float pocketLayer = 0.05f;
        public static readonly B2Vec2 PLAYER_SIZE = new(1f, 2f);
        public Vector2 PivotPoint { get; private set; } = new(PLAYER_SIZE.X * depth, PLAYER_SIZE.Y * 0.8f);
        public Vector2 WeaponPosition { get; private set; } = Vector2.Zero;
        public Vector2 HeldWeaponMuzzleOffset { get; private set; } = Vector2.Zero;

        public Entity HeadEntity { get; private set; } = null!;
        public Entity WeaponEntity { get; private set; } = null!;
        public Entity PocketLeft { get; private set; } = null!;
        public Entity PocketRight { get; private set; } = null!;

        private Texture2D NoneTexture = null!;
        public string CharacterSpriteName { get; set; } = "";
        private string Base => $"Characters/{CharacterSpriteName}";

        public readonly GameTimer WalkTimer = new(0.15f, true);
        private int Frame = 0;

        /// <summary>
        /// Инициализирует модель игрока: создает дочерние сущности для головы, оружия и карманов, настраивает анимацию ходьбы.
        /// </summary>
        /// <param name="config">Конфигурация игрока.</param>
        /// <returns>Текущий экземпляр компонента.</returns>
        public GPlayerModel Init(PlayerConfig config)
        {
            Owner.AddComponent<GSprite>()
                .SetSizingPolicy(SizingPolicy.Source)
                .SetTranslation(new(0, 0.75f));

            HeadEntity = AddBodyPart(Owner);
            WeaponEntity = AddBodyPart(Owner);

            PocketLeft = AddBodyPart(Owner);
            PocketLeft.Transform.Position2D = new(-PLAYER_SIZE.X * depth, PLAYER_SIZE.Y * 0.3f);
            PocketLeft.Transform.Rotation = -MathF.PI / 2;

            PocketRight = AddBodyPart(Owner);
            PocketRight.Transform.Position2D = new(PLAYER_SIZE.X * depth, PLAYER_SIZE.Y * 0.3f);
            PocketRight.Transform.Rotation = -MathF.PI / 2;

            WeaponEntity.Transform.Layer = -0.01f;
            CharacterSpriteName = config.TextureName;
            NoneTexture = Owner.Scene.Assets.LoadTexture("None.png");

            WalkTimer.OnComplete += WalkAnim;
            Owner.Scene.RegisterTimer(WalkTimer);
            return this;
        }

        #region Texture Set
        private void WalkAnim()
        {
            Frame++;
            SetBodyTexture((Frame % 4) switch
            {
                0 => $"{Base}/BodyMoveL.png",
                1 => $"{Base}/BodyMoveM.png",
                2 => $"{Base}/BodyMoveR.png",
                3 => $"{Base}/BodyMoveM.png",
                _ => ""
            });
        }

        private void StopWalkAnim()
        {
            WalkTimer.Reset();
            Frame = 0;
        }

        /// <summary>
        /// Переключает состояние модели на движение. Запускает таймер анимации ходьбы.
        /// </summary>
        public void SetMoving()
        {
            SetHeadTexture($"{Base}/HeadMove.png");
            if (!WalkTimer.IsRunning) WalkTimer.Start();
        }

        /// <summary>
        /// Переключает состояние модели на ожидание (idle). Останавливает анимацию ходьбы.
        /// </summary>
        public void SetIdling()
        {
            SetHeadTexture($"{Base}/HeadIdle.png");
            SetBodyTexture($"{Base}/BodyIdle.png");
            StopWalkAnim();
        }

        /// <summary>
        /// Переключает состояние модели на падение/прыжок. Останавливает анимацию ходьбы.
        /// </summary>
        public void SetFalling()
        {
            SetHeadTexture($"{Base}/HeadIdle.png");
            SetBodyTexture($"{Base}/BodyJump.png");
            StopWalkAnim();
        }

        /// <summary>
        /// Переключает состояние модели на прицеливание. Останавливает анимацию ходьбы.
        /// </summary>
        public void SetAiming()
        {
            SetHeadTexture($"{Base}/HeadAiming.png");
            SetBodyTexture($"{Base}/BodyAiming.png");
            StopWalkAnim();
        }
        #endregion

        private void SetBodyTexture(string path) => SetTextureOnEntity(Owner, path);
        private void SetHeadTexture(string path) => SetTextureOnEntity(HeadEntity, path);

        /// <summary>
        /// Устанавливает текстуру текущего экипированного оружия.
        /// </summary>
        /// <param name="item">Экземпляр оружия.</param>
        public void SetWeaponTexture(WeaponItem item)
        {
            if (WeaponEntity.TryGet<GSprite>(out var sprite)) sprite
                    .SetTexture(item.SavedTexture)
                    .SetSize(item.TexSize);
        }

        /// <summary>
        /// Сбрасывает текстуру оружия на стандартную (None.png).
        /// </summary>
        public void ResetWeaponTexture()
        {
            if (WeaponEntity.TryGet<GSprite>(out var sprite)) sprite.SetTexture(NoneTexture);
        }

        /// <summary>
        /// Обновляет отображение оружия в карманах на основе состояния компонента GWeaponry.
        /// </summary>
        /// <param name="weaponry">Компонент управления оружием.</param>
        public void UpdatePockets(GWeaponry weaponry)
        {
            if (PocketLeft.TryGet<GSprite>(out var lSprite))
                if (weaponry.HeldWeapon is WeaponItem lItme) lSprite
                    .SetTexture(lItme.SavedTexture)
                    .SetSize(lItme.TexSize);
                else lSprite.SetTexture(NoneTexture);

            if (PocketRight.TryGet<GSprite>(out var rSprite))
                if (weaponry.AuxWeapon is WeaponItem rItme) rSprite
                    .SetTexture(rItme.SavedTexture)
                    .SetSize(rItme.TexSize);
                else rSprite.SetTexture(NoneTexture);
        }

        private static void SetTextureOnEntity(Entity entity, string path)
        {
            var tex = entity.Scene.Assets.LoadTexture(path);
            if (entity.TryGet<GSprite>(out var sprite)) sprite.SetTexture(tex);
        }

        /// <summary>
        /// Устанавливает направление взгляда игрока и поворачивает части тела соответственно.
        /// </summary>
        /// <param name="x">Координата X направления.</param>
        /// <param name="y">Координата Y направления.</param>
        /// <returns>True, если игрок смотрит вправо; иначе false.</returns>
        public bool SetFacingDiecration(float x, float y = 0)
        {
            bool isLeft = x < 0;
            Owner.Get<GSprite>()!.FlipX = isLeft;
            PocketLeft.Get<GSprite>()!.FlipY = isLeft;
            PocketRight.Get<GSprite>()!.FlipY = isLeft;
            float layer = isLeft ? -pocketLayer : pocketLayer;
            PocketLeft.Transform.Layer = layer;
            PocketRight.Transform.Layer = -layer;

            float side = isLeft ? 1 : -1;
            float angle = MathF.Atan2(y, x);
            PivotPoint = new(PLAYER_SIZE.X * depth * side, PivotPoint.Y);

            HeadEntity.Transform.Rotation = angle;
            HeadEntity.Transform.Position2D = PivotPoint;
            HeadEntity.Get<GSprite>()!
                .SetTranslation(side * new Vector2(PivotPoint.X, 0))
                .FlipY = isLeft;

            var weaponSpriteDisp = side * new Vector2(PivotPoint.X * 4, 0.1f);
            WeaponEntity.Transform.Rotation = angle;
            WeaponEntity.Transform.Position2D = PivotPoint;
            WeaponEntity.Get<GSprite>()!
                .SetTranslation(weaponSpriteDisp)
                .FlipY = isLeft;

            WeaponPosition = PivotPoint + MathUtils.Rotate(weaponSpriteDisp, -angle);
            return x > 0;
        }

        private static Entity AddBodyPart(Entity body)
        {
            var e = body.Scene.SpawnEntity();
            e.AddComponent<GSprite>().SetSizingPolicy(SizingPolicy.Source);
            e.Transform.SetParent(body.Transform);
            return e;
        }

        /// <summary>
        /// Освобождает ресурсы, снимает таймер анимации.
        /// </summary>
        public void Dispose()
        {
            Owner.Scene.UnregisterTimer(WalkTimer);
        }
    }
}
