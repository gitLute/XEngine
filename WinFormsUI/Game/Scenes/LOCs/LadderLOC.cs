using Box2D.NET;
using OpenTK.Mathematics;
using WinFormsUI.Game.Box2D;
using XEngine.Core.Base;
using XEngine.Core.Box2DCompat.Components;
using XEngine.Core.Common.Sprite.NineSlice;
using XEngine.Core.Scenery;
using XEngine.Core.Utils.JSONConverters;
using static XEngine.Core.Common.Sprite.SizingPolicy;

namespace WinFormsUI.Game.Scenes.LOCs
{
    /// <summary>
    /// Конфигурация лестницы. Определяет размеры сенсорной зоны для лазания.
    /// </summary>
    public class LadderLOC : BaseLOC
    {
        /// <summary>
        /// Размеры зоны лестницы.
        /// </summary>
        [JsonVector2] public Vector2 Size { get; set; } = Vector2.One;

        /// <summary>
        /// Создает объект лестницы с визуальным представлением и сенсорным коллайдером.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        /// <returns>Сущность лестницы.</returns>
        public override Entity Spawn(GScene scene)
        {
            var ladder = scene.SpawnEntity();

            ladder.Transform.Init(Pos, Rotation);

            ladder.AddComponent<GNineSlice>()
                .SetTexture(scene.Assets.LoadTexture("Environment\\Ladder.png"))
                .SetSizingPolicy(World)
                .SetSize(Size)
                .SetBorders(5, 2);
            var body = ladder.AddComponent<GBox2DBody>()
                .Init(ladder.Transform)
                .SetType(B2BodyType.b2_staticBody)
                .Build(scene.World.Id)
                .AttacShapes(bid =>
                {
                    B2Polygon ladderBox = B2Geometries.b2MakeBox(Size.X / 2, Size.Y / 2);
                    B2ShapeDef ladderDef = B2Types.b2DefaultShapeDef();
                    ladderDef.isSensor = true;
                    ladderDef.enableSensorEvents = true;
                    ladderDef.filter.categoryBits = (ulong)ContactFlags.LADDER;
                    ladderDef.filter.maskBits = (ulong)ContactFlags.PLAYER;
                    B2Shapes.b2CreatePolygonShape(bid, ladderDef, ladderBox);
                });

            return ladder;
        }
    }
}
