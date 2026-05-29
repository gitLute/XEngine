using XEngine.Core.Input;

namespace WinFormsUI.Game.Player.Contol
{
    /// <summary>
    /// Обертка для сервиса ввода, предоставляющая доступ к осям и действиям игрока.
    /// Позволяет динамически формировать имена действий на основе имени игрока.
    /// </summary>
    public partial class PlayerControl(IInputService input)
    {
        public string Name = "";

        /// <summary>
        /// Возвращает значение горизонтальной оси ввода.
        /// </summary>
        /// <returns>Значение от -1 до 1.</returns>
        public float HorizotnalInput()
        {
            return input.GetAxis($"Horizontal{Name}");
        }

        /// <summary>
        /// Возвращает значение вертикальной оси ввода.
        /// </summary>
        /// <returns>Значение от -1 до 1.</returns>
        public float VerticalInput()
        {
            return input.GetAxis($"Vertical{Name}");
        }

        /// <summary>
        /// Проверяет состояние указанного действия в зависимости от типа события.
        /// </summary>
        /// <param name="action">Базовое имя действия.</param>
        /// <param name="type">Тип события действия.</param>
        /// <returns>True, если действие активно в заданном режиме; иначе false.</returns>
        public bool Fetch(string action, ActionType type)
        {
            return type switch
            {
                ActionType.ActionStart => input.IsActionJustPressed(action + Name),
                ActionType.ActionActive => input.IsActionActive(action + Name),
                ActionType.ActionEnd => input.IsActionJustReleased(action + Name),
                ActionType.ActionInactive => input.IsActionInactive(action + Name),
                _ => false
            };
        }
    }
}
