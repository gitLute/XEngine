using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;
using XEngine.Core.Config;
using XEngine.Core.Input.InputAxis;

namespace XEngine.Core.Input
{
    public class InputManager : IInputService, IInputConfig
    {
        private readonly Dictionary<string, List<Keys>> _actionBindings = [];
        private readonly HashSet<Keys> _downKeys = [];
        private readonly HashSet<Keys> _previousDownKeys = [];
        private readonly Dictionary<string, Axis> _axes = [];

        // --- Service ---
        public void Update(float dt)
        {
            foreach (var axis in _axes.Values) axis.Update(this, dt);

            _previousDownKeys.Clear();
            foreach (var key in _downKeys) _previousDownKeys.Add(key);
        }

        public bool IsActionInactive(string actionName)
        {
            if (!_actionBindings.TryGetValue(actionName, out var keys)) return false;
            foreach (var key in keys) if (_downKeys.Contains(key) || _previousDownKeys.Contains(key)) return false;
            return true;
        }

        public bool IsActionActive(string actionName)
        {
            if (!_actionBindings.TryGetValue(actionName, out var keys)) return false;
            foreach (var key in keys) if (_downKeys.Contains(key) && _previousDownKeys.Contains(key)) return true;
            return false;
        }

        public bool IsActionJustPressed(string actionName)
        {
            if (!_actionBindings.TryGetValue(actionName, out var keys)) return false;
            foreach (var key in keys) if (_downKeys.Contains(key) && !_previousDownKeys.Contains(key)) return true;
            return false;
        }

        public bool IsActionJustReleased(string actionName)
        {
            if (!_actionBindings.TryGetValue(actionName, out var keys)) return false;
            foreach (var key in keys) if (!_downKeys.Contains(key) && _previousDownKeys.Contains(key)) return true;
            return false;
        }

        public float GetAxis(string axisName) => _axes.TryGetValue(axisName, out var a) ? a.Value : 0f;

        // --- Config ---
        public void LoadBindingsFromConfig(GameConfig config)
        {
            foreach (var keyBinding in config.KeyMap) BindAction(keyBinding.Key, keyBinding.Value);
            foreach (var axisBinding in config.Axes) _axes[axisBinding.Key] = new Axis(axisBinding.Value);
        }

        public void BindAction(string actionName, Keys key)
        {
            if (!_actionBindings.ContainsKey(actionName))
                _actionBindings[actionName] = [];

            _actionBindings[actionName].Add(key);
        }

        public void SetKeyDown(Keys key) => _downKeys.Add(key);
        public void SetKeyUp(Keys key) => _downKeys.Remove(key);
        public void ClearStates()
        {
            _downKeys.Clear();
            _previousDownKeys.Clear();
        }
    }
}