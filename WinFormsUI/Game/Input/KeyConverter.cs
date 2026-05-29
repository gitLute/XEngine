using WinKeys = System.Windows.Forms.Keys;
using OtkKeys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

namespace WinFormsUI.Game.Input
{
    /// <summary>
    /// Переводчик клавишь 'System.Windows.Forms.Keys' в клавиши 'OpenTK.Windowing.GraphicsLibraryFramework.Keys'
    /// Поддерживает A-Z, Стрелки, Shift, Insert, Ctrl
    /// </summary>
    public static class KeyConverter
    {
        private static readonly Dictionary<WinKeys, OtkKeys> Map = new() {
            { WinKeys.A, OtkKeys.A },
            { WinKeys.B, OtkKeys.B },
            { WinKeys.C, OtkKeys.C },
            { WinKeys.D, OtkKeys.D },
            { WinKeys.E, OtkKeys.E },
            { WinKeys.F, OtkKeys.F },
            { WinKeys.G, OtkKeys.G },
            { WinKeys.H, OtkKeys.H },
            { WinKeys.I, OtkKeys.I },
            { WinKeys.J, OtkKeys.J },
            { WinKeys.K, OtkKeys.K },
            { WinKeys.L, OtkKeys.L },
            { WinKeys.M, OtkKeys.M },
            { WinKeys.N, OtkKeys.N },
            { WinKeys.O, OtkKeys.O },
            { WinKeys.P, OtkKeys.P },
            { WinKeys.Q, OtkKeys.Q },
            { WinKeys.R, OtkKeys.R },
            { WinKeys.S, OtkKeys.S },
            { WinKeys.T, OtkKeys.T },
            { WinKeys.U, OtkKeys.U },
            { WinKeys.V, OtkKeys.V },
            { WinKeys.W, OtkKeys.W },
            { WinKeys.X, OtkKeys.X },
            { WinKeys.Y, OtkKeys.Y },
            { WinKeys.Z, OtkKeys.Z },

            { WinKeys.Up, OtkKeys.Up },
            { WinKeys.Down, OtkKeys.Down },
            { WinKeys.Left, OtkKeys.Left },
            { WinKeys.Right, OtkKeys.Right },

            { WinKeys.ShiftKey, OtkKeys.LeftShift },
            { WinKeys.Space, OtkKeys.Space },
            { WinKeys.Insert, OtkKeys.Insert },
            { WinKeys.Control, OtkKeys.LeftControl },
        };

        /// <summary>
        /// Перевод
        /// </summary>
        /// <param name="winKey">клавиша 'System.Windows.Forms.Keys'</param>
        /// <returns>клавиша 'OpenTK.Windowing.GraphicsLibraryFramework.Keys'</returns>
        public static OtkKeys ToOpenTK(WinKeys winKey)
        {
            return Map.TryGetValue(winKey, out var otkKey) ? otkKey : OtkKeys.Unknown;
        }
    }
}
