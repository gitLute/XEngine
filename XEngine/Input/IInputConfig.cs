using OpenTK.Windowing.GraphicsLibraryFramework;
using XEngine.Core.Config;

namespace XEngine.Core.Input
{
    public interface IInputConfig
    {
        /// <summary>
        /// Обновление Ввода 
        /// </summary>
        /// <param name="dt">Время с прошлого обновления</param>
        public void Update(float dt);

        /// <summary>
        /// Загрузка привязок действий из конфигурации игры
        /// </summary>
        /// <param name="config">конфигурация игры</param>
        public void LoadBindingsFromConfig(GameConfig config);

        /// <summary>
        /// Привязка действия к клавише
        /// </summary>
        /// <param name="actionName">идентификатор действия</param>
        /// <param name="key">клавиша к оторой привязывается действие</param>
        public void BindAction(string actionName, Keys key);

        /// <summary>
        /// Нажатие клавши
        /// </summary>
        /// <param name="key">нажатая клавиша</param>
        public void SetKeyDown(Keys key);

        /// <summary>
        /// Отпук клавиши
        /// </summary>
        /// <param name="key">отпущенная клавиша</param>
        public void SetKeyUp(Keys key);

        /// <summary>
        /// Очистка значений буферов клавишь
        /// </summary>
        public void ClearStates();
    }
}
