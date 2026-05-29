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
    /// Конфигурация платформы. Определяет размеры и текстуру.
    /// </summary>
    public class PlatformLOC : BaseLOC
    {
        /// <summary>
        /// Размеры платформы.
        /// </summary>
        [JsonVector2] public Vector2 Size { get; set; } = Vector2.One;

        /// <summary>
        /// Создает статическую платформу с коллайдером и девятичастным спрайтом.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        /// <returns>Сущность платформы.</returns>
        public override Entity Spawn(GScene scene)
        {
            var _platformTex = scene.Assets.LoadTexture("Environment\\Box.png");

            var platform = scene.SpawnEntity();
            platform.Transform.Init(Pos, Rotation);
            platform.AddComponent<GNineSlice>()
                .SetTexture(_platformTex)
                .SetSizingPolicy(World)
                .SetSize(Size)
                .SetBorders(8);

            platform.AddComponent<GBox2DBody>()
                .Init(platform.Transform)
                .SetType(B2BodyType.b2_staticBody)
                .Build(scene.World.Id)
                .AttacShapes(bid =>
                {
                    B2Polygon platformBox = B2Geometries.b2MakeBox(Size.X / 2, Size.Y / 2);
                    B2ShapeDef platformDef = B2Types.b2DefaultShapeDef();
                    platformDef.material.friction = 1f;
                    platformDef.enableSensorEvents = true;
                    platformDef.filter.categoryBits = (ulong)ContactFlags.SOLID;
                    platformDef.filter.maskBits = (ulong)ContactFlags.SOLID_MASK;
                    B2Shapes.b2CreatePolygonShape(bid, platformDef, platformBox);
                });
            return platform;
        }
    }
}
