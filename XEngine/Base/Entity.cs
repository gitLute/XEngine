using XEngine.Core.Common.Transform;
using XEngine.Core.Scenery;

namespace XEngine.Core.Base
{
    /// <summary>
    /// Игровая сущность. Контейнер для компонентов и точка входа в иерархию трансформаций.
    /// </summary>
    public sealed class Entity : IDisposable
    {
        /// <summary>
        /// Уникальный идентификатор сущности в текущей сцене.
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// Идентификатор источника (другая сущность), из которого была создана сущность.
        /// </summary>
        public int SourceId { get; private set; } = -1;

        /// <summary>
        /// Компонент трансформации, управляющий позицией, вращением и иерархией.
        /// </summary>
        public GTransform Transform { get; private set; }

        /// <summary>
        /// Сцена, к которой принадлежит данная сущность.
        /// </summary>
        public GScene Scene { get; private set; }

        internal bool _isDeleted = false;
        private bool _isSourceSet = false;
        private readonly Dictionary<Type, GameComponent> _components = [];

        internal Entity(int id, GScene scene)
        {
            Id = id;
            Scene = scene;
            Transform = AddComponent<GTransform>();
        }

        /// <summary>
        /// Добавляет новый компонент указанного типа к сущности.
        /// </summary>
        /// <typeparam name="T">Тип компонента.</typeparam>
        /// <returns>Экземпляр созданного компонента.</returns>
        public T AddComponent<T>() where T : GameComponent, new()
        {
            T component = new() { Owner = this };
            _components[typeof(T)] = component;
            return component;
        }

        /// <summary>
        /// Устанавливает идентификатор источника для сущности. Выполняется только один раз.
        /// </summary>
        /// <param name="id">Идентификатор источника.</param>
        public void SetSource(int id)
        {
            if (_isSourceSet) return;
            _isSourceSet = true;
            SourceId = id;
        }

        /// <summary>
        /// Проверяет наличие компонента указанного типа у сущности.
        /// </summary>
        /// <typeparam name="T">Тип компонента.</typeparam>
        /// <returns>True, если компонент существует.</returns>
        public bool Has<T>() where T : GameComponent => _components.ContainsKey(typeof(T));

        /// <summary>
        /// Возвращает компонент указанного типа или null, если он отсутствует.
        /// </summary>
        /// <typeparam name="T">Тип компонента.</typeparam>
        /// <returns>Экземпляр компонента или значение по умолчанию.</returns>
        public T? Get<T>() where T : GameComponent => _components.TryGetValue(typeof(T), out var c) ? (T)c : default;

        /// <summary>
        /// Пытается получить компонент указанного типа.
        /// </summary>
        /// <typeparam name="T">Тип компонента.</typeparam>
        /// <param name="comp">Выходной параметр: найденный компонент.</param>
        /// <returns>True, если компонент найден.</returns>
        public bool TryGet<T>(out T comp) where T : GameComponent
        {
            if (!Has<T>())
            {
                comp = null!;
                return false;
            }
            comp = (T)_components[typeof(T)];
            return true;
        }

        /// <summary>
        /// Помечает сущность для удаления. Опционально удаляет всех потомков или отвязывает их от родителя.
        /// </summary>
        /// <param name="RemoveChildren">Если true, удаляет дочерние сущности рекурсивно. Если false, отвязывает их.</param>
        public void MarkDelete(bool RemoveChildren = false)
        {
            _isDeleted = true;

            List<Entity> children = [];
            Entity? current = Transform.FirstChild?.Owner;
            while (current != null)
            {
                children.Add(current);
                current = current.Transform.NextSibling?.Owner;
            }

            foreach (var child in children)
            {
                if (RemoveChildren) child.MarkDelete(true);
                else
                {
                    child.Transform.Position2D = child.Transform.RelativePosition2D;
                    child.Transform.SetParent(null);
                }
            }
        }

        /// <summary>
        /// Возвращает дочернюю сущность по индексу в списке детей текущего объекта.
        /// </summary>
        /// <param name="index">Индекс дочерней сущности.</param>
        /// <returns>Дочерняя сущность или null.</returns>
        public Entity? GetChild(int index)
        {
            return Transform.GetChild(index)?.Owner;
        }

        /// <summary>
        /// Освобождает ресурсы всех компонентов сущности.
        /// </summary>
        public void Dispose()
        {
            foreach (var pair in _components) (pair.Value as IDisposable)?.Dispose();
        }
    }
}
