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
    /// Конфигурация динамического ящика. Определяет размеры и создает физическое тело с коллайдером.
    /// </summary>
    internal class BoxLOC : BaseLOC
    {
        /// <summary>
        /// Размеры ящика.
        /// </summary>
        [JsonVector2] public Vector2 Size { get; set; } = Vector2.One;

        /// <summary>
        /// Создает динамический ящик с визуальным представлением и физикой.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        /// <returns>Сущность ящика.</returns>
        public override Entity Spawn(GScene scene)
        {
            var box = scene.SpawnEntity();
            
            box.Transform.Init(Pos, Rotation);

            box.AddComponent<GNineSlice>()
                .SetTexture(scene.Assets.LoadTexture("Environment\\Box.png"))
                .SetSizingPolicy(World)
                .SetSize(Size)
                .SetBorders(8);

            box.AddComponent<GBox2DBody>()
                .Init(box.Transform)
                .SetType(B2BodyType.b2_dynamicBody)
                .SetEnableSleep(false)
                .Build(scene.World.Id)
                .AttacShapes(bid =>
                {
                    B2Polygon boxBox = B2Geometries.b2MakeRoundedBox(Size.X / 2 - 0.1f, Size.Y / 2 - 0.1f, 0.1f);
                    B2ShapeDef boxDef = B2Types.b2DefaultShapeDef();
                    boxDef.material.friction = 1;
                    boxDef.enableSensorEvents = true;
                    boxDef.filter.categoryBits = (ulong)ContactFlags.SOLID;
                    boxDef.filter.maskBits = (ulong)ContactFlags.SOLID_MASK;
                    B2Shapes.b2CreatePolygonShape(bid, boxDef, boxBox);
                });
            return box;
        }
    }
}
