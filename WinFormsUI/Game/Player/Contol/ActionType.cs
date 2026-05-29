namespace WinFormsUI.Game.Player.Contol
{
    /// <summary>
    /// Перечисление типов событий ввода для действий.
    /// </summary>
    public enum ActionType
    {
        /// <summary>
        /// Действие только что началось (одиночное нажатие).
        /// </summary>
        ActionStart,

        /// <summary>
        /// Действие активно (удерживается).
        /// </summary>
        ActionActive,

        /// <summary>
        /// Действие только что завершилось (отпускание кнопки).
        /// </summary>
        ActionEnd,

        /// <summary>
        /// Действие неактивно.
        /// </summary>
        ActionInactive
    }
}
