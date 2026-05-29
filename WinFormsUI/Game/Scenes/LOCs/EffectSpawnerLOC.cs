using XEngine.Core.Base;
using XEngine.Core.Scenery;

namespace WinFormsUI.Game.Scenes.LOCs
{
    /// <summary>
    /// Конфигурация спавнера эффектов.
    /// </summary>
    internal class EffectSpawnerLOC : BaseLOC
    {
        /// <summary>
        /// Создает спавнер эффектов в указанной позиции.
        /// </summary>
        /// <param name="scene">Текущая сцена.</param>
        /// <returns>Сущность спавнера.</returns>
        public override Entity Spawn(GScene scene)
        {
            var e = scene.SpawnEntity();
            e.Transform.Init(Pos, 0);
            e.AddComponent<GEffectSpawner>().Init();
            return e;
        }
    }
}
