using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XEngine.Core.Input
{
    public interface IInputService
    {
        /// <summary>
        /// Проверка является ли действие неактивным
        /// </summary>
        /// <param name="actionName">Индентификатор действия</param>
        public bool IsActionInactive(string actionName);

        /// <summary>
        /// Проверка является ли действие активным
        /// </summary>
        /// <param name="actionName">Индентификатор действия</param>
        public bool IsActionActive(string actionName);

        /// <summary>
        /// Проверка является ли действие только что активированным
        /// </summary>
        /// <param name="actionName">Индентификатор действия</param>
        public bool IsActionJustPressed(string actionName);

        /// <summary>
        /// Проверка является ли действие только что деактивированным
        /// </summary>
        /// <param name="actionName">Индентификатор действия</param>
        public bool IsActionJustReleased(string actionName);

        /// <summary>
        /// Получение значения оси ввода по названию
        /// </summary>
        /// <param name="axisName">название оси ввода</param>
        public float GetAxis(string axisName);
    }
}
