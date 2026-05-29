using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XEngine.Core.Graphics.OpenGL;

namespace XEngine.Core.Common.Sprite.NineSlice
{
    /// <summary>
    /// Компонент спрайта с поддержкой девятичастного масштабирования (Nine-Slice).
    /// Позволяет растягивать текстуру без искажения углов и границ.
    /// </summary>
    public class GNineSlice : GSprite
    {
        public Vector4 Borders { get; protected set; } = Vector4.Zero;

        /// <summary>
        /// Устанавливает размеры отступов для каждой из четырех сторон (лево, право, верх, низ).
        /// </summary>
        /// <param name="left">Отступ слева.</param>
        /// <param name="right">Отступ справа.</param>
        /// <param name="top">Отступ сверху.</param>
        /// <param name="bottom">Отступ снизу.</param>
        public GNineSlice SetBorders(float left, float right, float top, float bottom)
        {
            Borders = new Vector4(left, right, top, bottom);
            return this;
        }

        /// <summary>
        /// Устанавливает симметричные отступы по горизонтали и вертикали.
        /// </summary>
        /// <param name="paddingX">Горизонтальный отступ.</param>
        /// <param name="paddingY">Вертикальный отступ.</param>
        public GNineSlice SetBorders(float paddingX, float paddingY)
        {
            Borders = new Vector4(paddingX, paddingX, paddingY, paddingY);
            return this;
        }

        /// <summary>
        /// Устанавливает одинаковый отступ для всех сторон.
        /// </summary>
        /// <param name="padding">Размер отступа.</param>
        public GNineSlice SetBorders(float padding)
        {
            Borders = new Vector4(padding, padding, padding, padding);
            return this;
        }

        /// <summary>
        /// Устанавливает текстуру для спрайта.
        /// </summary>
        /// <param name="texture">Текстура OpenGL.</param>
        public new GNineSlice SetTexture(Texture2D texture)
        {
            base.SetTexture(texture);
            return this;
        }

        /// <summary>
        /// Устанавливает политику изменения размера спрайта.
        /// </summary>
        /// <param name="policy">Политика масштабирования.</param>
        public new GNineSlice SetSizingPolicy(SizingPolicy policy)
        {
            base.SetSizingPolicy(policy);
            return this;
        }

        /// <summary>
        /// Устанавливает смещение текстуры относительно центра спрайта.
        /// </summary>
        /// <param name="vec">Вектор смещения.</param>
        public new GNineSlice SetTranslation(Vector2 vec)
        {
            base.SetTranslation(vec);
            return this;
        }

        /// <summary>
        /// Устанавливает угол поворота спрайта.
        /// </summary>
        /// <param name="rotation">Угол в радианах.</param>
        public new GNineSlice SetRotation(float rotation)
        {
            base.SetRotation(rotation);
            return this;
        }

        /// <summary>
        /// Устанавливает размер области рендеринга спрайта.
        /// </summary>
        /// <param name="size">Новый размер.</param>
        public new GNineSlice SetSize(Vector2 size)
        {
            base.SetSize(size);
            return this;
        }

        /// <summary>
        /// Устанавливает прозрачность спрайта.
        /// </summary>
        /// <param name="alpha">Значение альфа-канала (0..1).</param>
        public new GNineSlice SetAlpha(float alpha)
        {
            base.SetAlpha(alpha);
            return this;
        }

        public Vector2 RenderSize => _size;
    }
}
