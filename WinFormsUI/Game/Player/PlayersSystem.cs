using XEngine.Core.Input;
using XEngine.Core.Scenery;

namespace WinFormsUI.Game.Player
{
    /// <summary>
    /// Система обновления логики всех игроков на сцене.
    /// Обрабатывает ввод и проверяет условия выхода за границы мира.
    /// </summary>
    internal class PlayersSystem(IInputService input) : InputSystem(input)
    {
        /// <summary>
        /// Обновляет состояние всех сущностей с компонентом GPlayer.
        /// </summary>
        /// <param name="_scene">Текущая сцена.</param>
        /// <param name="dt">Время, прошедшее с последнего кадра.</param>
        public override void Update(GScene _scene, float _dt)
        {
            foreach (var (_, player) in _scene.Query<GPlayer>())
            {
                player.ProcessInput(_scene, _dt);
                if (player.Owner.Transform.Position.Y < -15) player.Owner.MarkDelete();
            }
        }
    }
}
