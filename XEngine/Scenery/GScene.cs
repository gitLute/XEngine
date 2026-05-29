using Box2D.NET;
using XEngine.Core.Base;
using XEngine.Core.Box2DCompat.Components;
using XEngine.Core.Common;
using XEngine.Core.Graphics;
using XEngine.Core.Input;

namespace XEngine.Core.Scenery
{
    public class GScene
    {
        public event Action? OnEnd;

        protected int _id = 0;

        private readonly Dictionary<int, Entity> _entities = [];
        private readonly List<IGameSystem> _systems = [];
        private readonly List<Action> schedules = [];
        private readonly List<GameTimer> _timers = [];

        public bool IsDeleted { get; set; } = false;
        public readonly IInputService Input;
        public readonly IAssetLoader Assets;
        public readonly Random Random;
        public float GetR01() => (float)Random.NextDouble();
        public float GetR_11() => (float)Random.NextDouble() * 2 - 1;
        public GCamera Camera { get; }
        public GBox2DWorld World { get; }

        public GScene(GameEngine _engine)
        {
            Assets = _engine.Assets;
            Input = _engine.Input;
            Random = new Random();

            var _cam = SpawnEntity();
            Camera = _cam.AddComponent<GCamera>();
            _cam.Transform.Init(new(0, 0, 30), 0);

            var _wld = SpawnEntity();
            World = _wld.AddComponent<GBox2DWorld>().Init(pixelPerMetre: 16, gravity: new B2Vec2(0, -18));
        }

        /// <summary>
        /// Создание новой сущности
        /// </summary>
        /// <returns>Экземпляр сужности</returns>
        public Entity SpawnEntity()
        {
            int id = _id++;
            var _e = new Entity(id, this);
            _entities[id] = _e;
            return _e;
        }

        /// <summary>
        /// Записывает действие в очередь для исполнения вне основного цикла (для избежания оштбок изменения коллекции во время итерации)
        /// </summary>
        public void Schedule(Action action)
        {
            schedules.Add(action);
        }

        /// <summary>
        /// Очистка сцены от сущностей
        /// </summary>
        protected void ClearScene()
        {
            foreach (var e in _entities.Values) e.MarkDelete();
        }

        /// <summary>
        /// Вызов завершения сцены
        /// </summary>
        public void End() => OnEnd?.Invoke();

        #region System
        /// <summary>
        /// Добавление системы к сцене
        /// </summary>
        public void AddSystem(IGameSystem system)
        {
            _systems.Add(system);
            _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        /// <summary>
        /// Удалени системы из сцены (по экземрляру)
        /// </summary>
        public void RemoveSystem(IGameSystem system)
        {
            _systems.Remove(system);
        }
        #endregion

        #region Context
        /// <summary>
        /// загрузка с сцены
        /// </summary>
        public virtual void Load() { }

        /// <summary>
        /// выгрузка с сцены
        /// </summary>
        public virtual void Unload()
        {
            World.Dispose();
        }
        #endregion

        #region Timer
        /// <summary>
        /// Регистрация таймера для обновления
        /// </summary>
        public void RegisterTimer(GameTimer timer)
        {
            if (!_timers.Contains(timer)) _timers.Add(timer);
        }
        /// <summary>
        /// Исключение таймера из обновления
        /// </summary>
        /// <param name="timer"></param>
        public void UnregisterTimer(GameTimer timer)
        {
            _timers.Remove(timer);
        }
        #endregion

        #region Update
        /// <summary>
        /// Обновелие таймеров.
        /// Вызов систем.
        /// Вызов запланированных действий.
        /// удаление сущностей, помеченных на удаление.
        /// </summary>
        /// <param name="_dt"></param>
        public void Update(float _dt)
        {
            if (IsDeleted) return;
            UpdateTimers(_dt);
            InvokeSystems(_dt);
            InvokeEvents();
            RemoveDeleted();
        }
        private void UpdateTimers(float _dt)
        {
            foreach (var timer in _timers) timer.Tick(_dt);
        }
        private void InvokeSystems(float _dt)
        {
            foreach (var _s in _systems) if (_s.IsEnabled) _s.Update(this, _dt);
        }
        private void InvokeEvents()
        {
            foreach (var action in schedules) action.Invoke();
            schedules.Clear();
        }
        private void RemoveDeleted()
        {
            var _removedEntityId = _entities
                .Where(kvp => kvp.Value._isDeleted)
                .Select(kvp => kvp.Key);
            foreach (var key in _removedEntityId)
            {
                if (_entities.TryGetValue(key, out var value))
                {
                    value.Transform.SetParent(null);
                    value.Dispose();
                    _entities.Remove(key);
                }
            }
        }
        #endregion

        #region Query
        /// <summary>
        /// Получение сущности по индексу (null если нидекс пустой)
        /// </summary>
        public Entity? GetById(int id) => _entities.TryGetValue(id, out var entity) ? entity : null;

        /// <summary>
        /// получение итератора сущностей по индексам
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public IEnumerable<Entity> IterateByIds(IEnumerable<int> ids)
        {
            foreach (var id in ids) if (_entities.TryGetValue(id, out var entity)) yield return entity;
        }
        
        /// <summary>
        /// Получение сущностей.
        /// </summary>
        /// <param name="_predicate">Предикат сущности (true - включена, false - исключена)</param>
        public IEnumerable<Entity> Query(Predicate<Entity> _predicate)
        {
            foreach (var _e in _entities.Values) if (_predicate(_e)) yield return _e;
        }

        /// <summary>
        /// Получение сущностей с компонентом T1
        /// </summary>
        /// <typeparam name="T1">Компонент сущности</typeparam>
        /// <param name="_predicate">Предикат сущности (true - включена, false - исключена)</param>
        /// <returns>Кортеж вида (Entity, T1)</returns>
        public IEnumerable<(Entity, T1)> Query<T1>(Func<Entity, T1, bool>? _predicate = null)
            where T1 : GameComponent
        {
            foreach (var _e in _entities.Values)
            {
                if (_e.TryGet<T1>(out T1 comp1))
                {
                    if (_predicate != null && !_predicate(_e, comp1)) continue;
                    yield return (_e, comp1);
                }
            }
        }

        /// <summary>
        /// Получение сущностей с компонентами T1 и T2
        /// </summary>
        /// <typeparam name="T1">Компонент сущности</typeparam>
        /// <typeparam name="T2">Компонент сущности</typeparam>
        /// <param name="_predicate">Предикат сущности (true - включена, false - исключена)</param>
        /// <returns>Кортеж вида (Entity, T1, T2)</returns>
        public IEnumerable<(Entity, T1, T2)> Query<T1, T2>(Func<Entity, T1, T2, bool>? _predicate = null)
            where T1 : GameComponent
            where T2 : GameComponent
        {
            foreach (var _e in _entities.Values)
            {
                if (_e.TryGet<T1>(out T1 comp1) && _e.TryGet<T2>(out T2 comp2))
                {
                    if (_predicate != null && !_predicate(_e, comp1, comp2)) continue;
                    yield return (_e, comp1, comp2);
                }
            }
        }

        /// <summary>
        /// Получение сущностей с компонентами T1, T2 и T3
        /// </summary>
        /// <typeparam name="T1">Компонент сущности</typeparam>
        /// <typeparam name="T2">Компонент сущности</typeparam>
        /// <typeparam name="T3">Компонент сущности</typeparam>
        /// <param name="_predicate">Предикат сущности (true - включена, false - исключена)</param>
        /// <returns>Кортеж вида (Entity, T1, T2, T3)</returns>
        public IEnumerable<(Entity, T1, T2, T3)> Query<T1, T2, T3>(Func<Entity, T1, T2, T3, bool>? _predicate = null)
            where T1 : GameComponent
            where T2 : GameComponent
            where T3 : GameComponent
        {
            foreach (var _e in _entities.Values)
            {
                if (_e.TryGet<T1>(out T1 comp1) && _e.TryGet<T2>(out T2 comp2) && _e.TryGet<T3>(out T3 comp3))
                {
                    if (_predicate != null && !_predicate(_e, comp1, comp2, comp3)) continue;
                    yield return (_e, comp1, comp2, comp3);
                }
            }
        }
        #endregion
    }
}
