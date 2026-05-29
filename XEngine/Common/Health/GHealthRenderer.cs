using OpenTK.Mathematics;
using XEngine.Core.Base;

namespace XEngine.Core.Common.Health
{
    /// <summary>
    /// Компонент визуального отображения полоски здоровья. Хранит настройки позиции и цветов для рендеринга индикатора HP.
    /// </summary>
    public class GHealthRenderer : GameComponent
    {
        public Vector2 Position { get; private set; }
        public Color4 ForeGround { get; private set; } = Color4.Green;
        public Color4 BackGround { get; private set; } = Color4.Red;

        /// <summary>
        /// Устанавливает локальное смещение полоски здоровья относительно центра объекта.
        /// </summary>
        /// <param name="pos">Вектор смещения.</param>
        public GHealthRenderer SetPosition(Vector2 pos)
        {
            Position = pos;
            return this;
        }

        /// <summary>
        /// Настраивает цвета переднего плана (текущее здоровье) и фона (потерянное здоровье) индикатора.
        /// </summary>
        /// <param name="foreground">Цвет заполненной части (опционально).</param>
        /// <param name="background">Цвет пустой части (опционально).</param>
        public GHealthRenderer SetColor(Color4? foreground = null, Color4? background = null)
        {
            ForeGround = foreground == null ? ForeGround : foreground.Value;
            BackGround = background == null ? BackGround : background.Value;
            return this;
        }
    }
}
