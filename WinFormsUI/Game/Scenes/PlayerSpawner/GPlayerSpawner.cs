using Box2D.NET;
using WinFormsUI.Game.Player;
using XEngine.Core.Base;

namespace WinFormsUI.Game.Scenes.PlayerSpawner
{
    /// <summary>
    /// Компонент точки спавна игрока. Хранит имя игрока и создает его сущность в указанной позиции.
    /// </summary>
    public class GPlayerSpawner : GameComponent
    {
        public string Name { get; private set; } = "";

        /// <summary>
        /// Инициализирует спавнер именем будущего игрока.
        /// </summary>
        /// <param name="name">Имя игрока.</param>
        public GPlayerSpawner Init(string name)
        {
            Name = name;
            return this;
        }

        /// <summary>
        /// Создает сущность игрока, удаляет точку спавна и возвращает созданного игрока.
        /// </summary>
        /// <param name="playerId">Уникальный идентификатор игрока.</param>
        /// <returns>Созданная сущность игрока.</returns>
        public Entity Spawn(string playerId)
        {
            Owner.MarkDelete();
            B2Vec2 pos = new(Owner.Transform.Position2D.X, Owner.Transform.Position2D.Y);
            return PlayerUtil.Instance.CreatePlayer(Owner.Scene, pos, Name, playerId);
        }
    }
}
