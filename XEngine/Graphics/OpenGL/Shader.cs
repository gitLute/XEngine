using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace XEngine.Core.Graphics.OpenGL
{
    /// <summary>
    /// Обертка для шейдерной программы OpenGL
    /// </summary>
    public sealed class Shader : IDisposable
    {
        public int Handle { get; private set; }
        private readonly Dictionary<string, int> _uniformLocations = [];
        private bool _disposed;

        private static int _currentBoundHandle = -1;    // видно всем чтобы не менять handle когда не надо

        /// <summary>
        /// Инициализация из папки.
        /// Папка должна содержать shader.vert и shader.frag
        /// </summary>
        /// <param name="folderPath">Абсолютнвй путь к папке</param>
        public static Shader FromFolder(string folderPath)
        {
            var _vertexPath = Path.Combine(folderPath, "shader.vert");
            var _fragentPath = Path.Combine(folderPath, "shader.frag");
            return new Shader(File.ReadAllText(_vertexPath), File.ReadAllText(_fragentPath));
        }

        /// <summary>
        /// Инициализация из файлов.
        /// </summary>
        /// <param name="vPath">Абсолютнвй путь к вершинному шейдеру</param>
        /// <param name="fPath">Абсолютнвй путь к фрагментному шейдеру</param>
        public static Shader FromFiles(string vPath, string fPath)
        {
            return new Shader(File.ReadAllText(vPath), File.ReadAllText(fPath));
        }

        /// <summary>
        /// Инициализация из исходных значений
        /// </summary>
        /// <param name="vSource">Исходная строка вершинного шейдера</param>
        /// <param name="fSource">Исходная строка фрагментного шейдера</param>
        /// <returns></returns>
        public static Shader FromSource(string vSource, string fSource)
        {
            return new Shader(vSource, fSource);
        }

        private Shader(string vSource, string fSource)
        {
            Init(vSource, fSource);
        }

        private void Init(string vertexSource, string fragmentSource)
        {
            int vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
            int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

            Handle = GL.CreateProgram();
            GL.AttachShader(Handle, vertexShader);
            GL.AttachShader(Handle, fragmentShader);
            GL.LinkProgram(Handle);

            CheckProgramLog(Handle);

            GL.DetachShader(Handle, vertexShader);
            GL.DetachShader(Handle, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            CacheActiveUniforms();
        }

        private static int CompileShader(ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string info = GL.GetShaderInfoLog(shader);
                Debug.WriteLine($"Shader compile error ({type}): {info}");
            }
            return shader;
        }

        private static void CheckProgramLog(int program)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string info = GL.GetProgramInfoLog(program);
                Debug.WriteLine($"Program link error: {info}");
            }
        }

        private void CacheActiveUniforms()
        {
            GL.GetProgram(Handle, GetProgramParameterName.ActiveUniforms, out int uniformCount);
            for (int i = 0; i < uniformCount; i++)
            {
                string name = GL.GetActiveUniform(Handle, i, out _, out _);
                int location = GL.GetUniformLocation(Handle, name);
                _uniformLocations[name] = location;
            }
        }

        /// <summary>
        /// Использование шейдерной программы (см. OpenGL UseProgram)
        /// </summary>
        public void Use()
        {
            if (_currentBoundHandle != Handle)
            {
                GL.UseProgram(Handle);
                _currentBoundHandle = Handle;
            }
        }

        /// <summary>
        /// Получение индекса uniform (см. OpenGL GetUniformLocation)
        /// </summary>
        /// <param name="name">идентификатор uniform</param>
        /// <returns>индекс uniform</returns>
        public int GetUniformLocation(string name)
        {
            if (_uniformLocations.TryGetValue(name, out int location))
                return location;

            location = GL.GetUniformLocation(Handle, name);
            _uniformLocations[name] = location;
            return location;
        }

        // --- Uniform (GPU consts) Setters ---

        /// <summary>
        /// Установка uniform значений (см. OpenGL Uniform)
        /// </summary>
        /// <param name="name">назвние uniform</param>
        /// <param name="value">значение</param>
        public void SetInt(string name, int value)
        {
            int loc = GetUniformLocation(name);
            if (loc != -1) GL.Uniform1(loc, value);
        }

        /// <summary>
        /// Установка uniform значений (см. OpenGL Uniform)
        /// </summary>
        /// <param name="name">назвние uniform</param>
        /// <param name="value">значение</param>
        public void SetFloat(string name, float value)
        {
            int loc = GetUniformLocation(name);
            if (loc != -1) GL.Uniform1(loc, value);
        }

        /// <summary>
        /// Установка uniform значений (см. OpenGL Uniform)
        /// </summary>
        /// <param name="name">назвние uniform</param>
        /// <param name="value">значение</param>
        public void SetVector2(string name, Vector2 value)
        {
            int loc = GetUniformLocation(name);
            if (loc != -1) GL.Uniform2(loc, value.X, value.Y);
        }

        /// <summary>
        /// Установка uniform значений (см. OpenGL Uniform)
        /// </summary>
        /// <param name="name">назвние uniform</param>
        /// <param name="value">значение</param>
        public void SetVector3(string name, Vector3 value)
        {
            int loc = GetUniformLocation(name);
            if (loc != -1) GL.Uniform3(loc, value);
        }

        /// <summary>
        /// Установка uniform значений (см. OpenGL Uniform)
        /// </summary>
        /// <param name="name">назвние uniform</param>
        /// <param name="value">значение</param>
        public void SetVector4(string name, Vector4 value)
        {
            int loc = GetUniformLocation(name);
            if (loc != -1) GL.Uniform4(loc, value);
        }

        /// <summary>
        /// Установка uniform значений (см. OpenGL Uniform)
        /// </summary>
        /// <param name="name">назвние uniform</param>
        /// <param name="value">значение</param>
        public void SetMatrix4(string name, Matrix4 value)
        {
            int loc = GetUniformLocation(name);
            if (loc != -1) GL.UniformMatrix4(loc, false, ref value);
        }

        // --- Cleanup --
        /// <summary>
        /// Удаление шейдерной программы
        /// </summary>
        public void Dispose()
        {
            Dispose_();
            GC.SuppressFinalize(this);
        }

        private void Dispose_()
        {
            if (_disposed) return;
            _disposed = true;

            if (_currentBoundHandle == Handle)
                _currentBoundHandle = -1;
            GL.DeleteProgram(Handle);
        }

        ~Shader() => Dispose_();
    }
}
