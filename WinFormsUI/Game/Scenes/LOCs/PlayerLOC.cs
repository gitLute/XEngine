using WinFormsUI.Game.Scenes.PlayerSpawner;
using XEngine.Core.Base;
using XEngine.Core.Scenery;

namespace WinFormsUI.Game.Scenes.LOCs
{
    /// <summary>
    /// Конфигурация точки спавна игрока. Содержит имя персонажа.
    /// </summary>
    internal class PlayerLOC : BaseLOC
    {
        /// <summary>
        /// Имя игрока, который будет создан в этой точке.
        /// </summary>
        public string Name { get; set; } = "NoneChar";

        /// <summary>
        /// Создает точку спавна игрока с заданным именем.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        /// <returns>Сущность спавнера.</returns>
        public override Entity Spawn(GScene scene)
        {
            var spawner = scene.SpawnEntity();
            spawner.Transform.Init(Pos, 0);
            spawner.AddComponent<GPlayerSpawner>().Init(Name);
            return spawner;
        }
    }
}
