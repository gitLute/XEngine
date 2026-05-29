using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XEngine.Core.Input.InputAxis
{
    public class AxisSettings
    {
        /// <summary>
        /// Идентификатор действия для смещения зачения в положительном направлении
        /// </summary>
        public string Positive { get; set; } = "";
        /// <summary>
        /// Идентификатор действия для смещения зачения в отрияательном направлении
        /// </summary>
        public string Negative { get; set; } = "";

        /// <summary>
        /// отзывчивость оси (чем выше, тем быстрее ось реагирует на нажатия)
        /// </summary>
        public float Sensitivity { get; set; } = 3f;

        /// <summary>
        /// Резистивность оси (чем выше, тем бвстрее ось возвращается к начальному значению)
        /// </summary>
        public float Gravity { get; set; } = 3f;
    }
}
