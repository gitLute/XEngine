using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XEngine.Core.Utils;

namespace XEngine.Core.Input.InputAxis
{
    public class Axis(AxisSettings settings)
    {
        public string Positive = settings.Positive;
        public string Negative = settings.Negative;
        public float Gravity = settings.Gravity;
        public float Sensitivity = settings.Sensitivity;
        public float Value { get; private set; } = 0;

        /// <summary>
        /// Обновление оси 
        /// </summary>
        public void Update(IInputService input, float dt)
        {
            float target = 0;
            float current = Value;

            if (input.IsActionActive(Positive)) target += 1;
            if (input.IsActionActive(Negative)) target -= 1;

            if (target != 0) current = MathUtils.MoveToward(current, target, Sensitivity * dt);
            else current = MathUtils.MoveToward(current, 0, Gravity * dt);

            Value = current;
        }
    }
}
