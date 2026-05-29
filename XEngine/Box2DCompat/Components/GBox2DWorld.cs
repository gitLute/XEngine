using Box2D.NET;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XEngine.Core.Base;

namespace XEngine.Core.Box2DCompat.Components
{
    /// <summary>
    /// Компонент физического мира Box2D.
    /// Управляет глобальными настройками симуляции, такими как гравитация и масштаб пикселей.
    /// </summary>
    public sealed class GBox2DWorld : GameComponent, IDisposable
    {
        public B2WorldId Id;
        public int PixelPerMetre = 1;
        private bool _disposed = false;
        private Matrix4 _ppmScale = Matrix4.Identity;

        /// <summary>
        /// Инициализирует физический мир с заданным масштабом и вектором гравитации.
        /// </summary>
        /// <param name="pixelPerMetre">Количество пикселей в одном метре мира.</param>
        /// <param name="gravity">Вектор гравитации.</param>
        public GBox2DWorld Init(int pixelPerMetre, B2Vec2 gravity)
        {
            PixelPerMetre = pixelPerMetre;
            _ppmScale = Matrix4.CreateScale(1.0f / pixelPerMetre, 1.0f / pixelPerMetre, 1);
            B2WorldDef worldDef = B2Types.b2DefaultWorldDef();
            worldDef.gravity = gravity;

            Id = B2Worlds.b2CreateWorld(worldDef);
            return this;
        }

        /// <summary>
        /// Возвращает матрицу масштабирования для преобразования мировых единиц в экранные пиксели.
        /// </summary>
        public Matrix4 PPMScale => _ppmScale;

        /// <summary>
        /// Уничтожает физический мир и освобождает связанные ресурсы.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            B2Worlds.b2DestroyWorld(Id);
        }
    }
}
