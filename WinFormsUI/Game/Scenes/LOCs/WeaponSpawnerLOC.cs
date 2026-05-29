using XEngine.Core.Base;
using XEngine.Core.Scenery;

namespace WinFormsUI.Game.Scenes.LOCs
{
    /// <summary>
    /// Конфигурация спавнера оружия.
    /// </summary>
    internal class WeaponSpawnerLOC : BaseLOC
    {
        /// <summary>
        /// Создает спавнер оружия в указанной позиции.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        /// <returns>Сущность спавнера.</returns>
        public override Entity Spawn(GScene scene)
        {
            var e = scene.SpawnEntity();
            e.Transform.Init(Pos, 0);
            e.AddComponent<GWeaponSpawner>().Init();
            return e;
        }
    }
}
