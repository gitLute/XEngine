    using System.Diagnostics;
using XEngine.Core.Graphics;

namespace XEngine.Core.Scenery
{
    /// <summary>
    /// Управляет жизненным циклом сцен: загрузка, выгрузка и обновление.
    /// </summary>
    public sealed class SceneManager(AssetManager assets) : IDisposable
    {
        private bool _disposed = false;
        public GScene? CurrentScene { get; private set; }
        private readonly Queue<Action<SceneManager>> schedules = [];
        public AssetManager Assets => assets;

        /// <summary>
        /// Записывает действие в очередь для исполнения вне основного цикла (для избежания оштбок изменения коллекции во время итерации)
        /// </summary>
        public void Schedule(Action<SceneManager> action) => schedules.Enqueue(action);

        /// <summary>
        /// Переход в другую сцену. Выгружает предыдущую
        /// </summary>
        /// <param name="newScene">Экземпляр сцены</param>
        public void SwitchTo(GScene newScene)
        {
            Schedule(UnloadScene);
            Schedule(LoadScene(newScene));
        }

        private static void UnloadScene(SceneManager mgr)
        {
            if (mgr.CurrentScene != null)
            {
                Debug.WriteLine("SceneUnloadStart");
                mgr.CurrentScene.Unload();
                mgr.Assets.UnloadSceneAssets();
                mgr.CurrentScene = null;
                Debug.WriteLine("SceneUnloadEnd");
            }
        }
        private static Action<SceneManager> LoadScene(GScene newScene)
        {
            return mgr =>
            {
                if (mgr.CurrentScene == null)
                {
                    Debug.WriteLine("SceneLoadStart");
                    newScene.Load();
                    mgr.CurrentScene = newScene;
                    Debug.WriteLine("SceneLoadEnd");
                }
                else Debug.WriteLine("[WARN]: Tried to load new scene when old was not unloaded");
            };
        }

        /// <summary>
        /// Обработка запланированных действий и затем обработка действующей сцены
        /// </summary>
        /// <param name="dt"></param>
        public void Update(float dt)
        {
            ProcessSchedules();
            CurrentScene?.Update(dt);
        }

        /// <summary>
        /// Обработка запланированных действий
        /// </summary>
        private void ProcessSchedules()
        {
            foreach (var schedule in schedules) schedule.Invoke(this);
            schedules.Clear();
        }

        /// <summary>
        /// Выгрузка действующей сцены
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CurrentScene?.Unload();
        }
    }
}
