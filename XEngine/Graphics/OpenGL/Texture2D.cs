using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbImageSharp;

namespace XEngine.Core.Graphics.OpenGL
{
    /// <summary>
    /// Обертка для дескриптора текстуры OpenGL
    /// </summary>
    public class Texture2D
    {
        public int Handle { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public Vector2 Size => new(Width, Height);
        private bool _disposed;

        public bool IsValid => !_disposed;

        /// <summary>
        /// Загрузка текстуры из файла
        /// </summary>
        /// <param name="path">Абсолютный путь</param>
        public static Texture2D FromPath(string path)
        {
            using FileStream stream = File.OpenRead(path);
            StbImage.stbi_set_flip_vertically_on_load(1);
            ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            int texHandle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texHandle);

            GL.TexImage2D(
                TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                image.Width, image.Height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, image.Data
            );

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            return new Texture2D { Handle = texHandle, Width = image.Width, Height = image.Height };
        }

        /// <summary>
        /// Создание текстуры гапрямую из байтов
        /// </summary>
        /// <param name="width">Ширина</param>
        /// <param name="height">Высота</param>
        /// <param name="data">Массив Байтов [ RGBA x width x height ]</param>
        /// <returns></returns>
        public static Texture2D FromBytes(int width, int height, byte[] data)
        {
            int texHandle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texHandle);

            GL.TexImage2D(
                TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                width, height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, data
            );

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            return new Texture2D { Handle = texHandle, Width = width, Height = height };
        }

        /// <summary>
        /// Привязка текстуры (см. OpenGl BindTexture)
        /// </summary>
        public void Use() => GL.BindTexture(TextureTarget.Texture2D, Handle);

        /// <summary>
        /// Удаления дескриптора
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            GL.DeleteTexture(Handle);
        }
    }
}
