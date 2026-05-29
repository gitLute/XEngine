using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XEngine.Core.Common.Sprite
{
    /// <summary>
    /// Политика масштабирования спрайта. Определяет, как размер текстуры соотносится с размером объекта в мире.
    /// </summary>
    public enum SizingPolicy
    {
        /// <summary>
        /// Использует исходный размер текстуры без масштабирования.
        /// </summary>
        Identity,

        /// <summary>
        /// Масштабирует спрайт относительно размеров мира (с учетом PixelPerMetre).
        /// </summary>
        Source,

        /// <summary>
        /// Масштабирует спрайт относительно исходного размера текстуры.
        /// </summary>
        World,
    }
}
