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
    /// Конфигурация башни (статического препятствия). Определяет размеры и текстуру.
    /// </summary>
    public class TowerLOC : BaseLOC
    {
        /// <summary>
        /// Размеры башни.
        /// </summary>
        [JsonVector2] public Vector2 Size { get; set; } = Vector2.One;

        /// <summary>
        /// Создает статическую башню с коллайдером и девятичастным спрайтом.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        /// <returns>Сущность башни.</returns>
        public override Entity Spawn(GScene scene)
        {
            var _towerTex = scene.Assets.LoadTexture("Environment\\GroundOrange.png");

            var tower = scene.SpawnEntity();
            tower.Transform.Init(Pos, Rotation);
            tower.AddComponent<GNineSlice>()
                .SetTexture(_towerTex)
                .SetSizingPolicy(World)
                .SetSize(Size)
                .SetBorders(16)
                .SetTranslation(new(0, 0.1f));

            tower.AddComponent<GBox2DBody>()
                .Init(tower.Transform)
                .SetType(B2BodyType.b2_staticBody)
                .Build(scene.World.Id)
                .AttacShapes(bid =>
                {
                    B2Polygon groundBox = B2Geometries.b2MakeBox(Size.X / 2, Size.Y / 2);
                    B2ShapeDef groundDef = B2Types.b2DefaultShapeDef();
                    groundDef.material.friction = 1f;
                    groundDef.enableSensorEvents = true;
                    groundDef.filter.categoryBits = (ulong)ContactFlags.SOLID;
                    groundDef.filter.maskBits = (ulong)ContactFlags.SOLID_MASK;
                    B2Shapes.b2CreatePolygonShape(bid, groundDef, groundBox);
                });
            return tower;
        }
    }
}
