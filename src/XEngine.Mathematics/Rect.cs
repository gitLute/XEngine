using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Прямоугольник в двумерном пространстве. Может иметь отрицательный размер:
/// в таком случае <see cref="Left"/> больше <see cref="Right"/>.
/// </summary>
public readonly struct Rect : IEquatable<Rect>
{
    /// <summary>
    /// Создаёт прямоугольник из координат левого верхнего угла и размера.
    /// </summary>
    /// <param name="x">Координата левой стороны.</param>
    /// <param name="y">Координата верхней стороны.</param>
    /// <param name="width">Ширина.</param>
    /// <param name="height">Высота.</param>
    public Rect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Создаёт прямоугольник из координат левого верхнего угла и размера.
    /// </summary>
    /// <param name="position">Положение левого верхнего угла.</param>
    /// <param name="size">Размер.</param>
    public Rect(Vector2 position, Vector2 size)
        : this(position.X, position.Y, size.X, size.Y)
    {
    }

    /// <summary>
    /// Пустой прямоугольник с нулевым размером в начале координат.
    /// </summary>
    public static Rect Zero => default;

    /// <summary>
    /// Координата левой стороны.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Координата верхней стороны.
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Ширина прямоугольника.
    /// </summary>
    public float Width { get; }

    /// <summary>
    /// Высота прямоугольника.
    /// </summary>
    public float Height { get; }

    /// <summary>
    /// Координата левой стороны с учётом нормализации размера.
    /// </summary>
    public float Left => X;

    /// <summary>
    /// Координата верхней стороны с учётом нормализации размера.
    /// </summary>
    public float Top => Y;

    /// <summary>
    /// Координата правой стороны с учётом нормализации размера.
    /// </summary>
    public float Right => X + Width;

    /// <summary>
    /// Координата нижней стороны с учётом нормализации размера.
    /// </summary>
    public float Bottom => Y + Height;

    /// <summary>
    /// Размер прямоугольника.
    /// </summary>
    public Vector2 Size => new(Width, Height);

    /// <summary>
    /// Положение левого верхнего угла.
    /// </summary>
    public Vector2 Position => new(X, Y);

    /// <summary>
    /// Центр прямоугольника.
    /// </summary>
    public Vector2 Center => new(X + Width * 0.5f, Y + Height * 0.5f);

    /// <summary>
    /// Признак пустого прямоугольника: площадь не больше нуля.
    /// </summary>
    public bool IsEmpty => Width <= 0f || Height <= 0f;

    /// <summary>
    /// Создаёт прямоугольник из двух противоположных углов в любом порядке.
    /// </summary>
    /// <param name="cornerA">Первый угол.</param>
    /// <param name="cornerB">Противоположный угол.</param>
    /// <returns>Прямоугольник с неотрицательным размером.</returns>
    public static Rect FromCorners(Vector2 cornerA, Vector2 cornerB)
    {
        Vector2 min = Vector2.Min(cornerA, cornerB);
        Vector2 max = Vector2.Max(cornerA, cornerB);
        return new Rect(min.X, min.Y, max.X - min.X, max.Y - min.Y);
    }

    /// <summary>
    /// Создаёт прямоугольник по центру и размеру.
    /// </summary>
    /// <param name="center">Центр.</param>
    /// <param name="size">Размер.</param>
    /// <returns>Прямоугольник.</returns>
    public static Rect FromCenter(Vector2 center, Vector2 size)
        => new(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f, size.X, size.Y);

    /// <summary>
    /// Проверяет, находится ли точка внутри прямоугольника.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <returns><c>true</c>, если точка внутри или на границе.</returns>
    public bool Contains(Vector2 point)
        => point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;

    /// <summary>
    /// Проверяет, находится ли точка внутри прямоугольника с допуском.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <param name="epsilon">Допуск по обеим границам.</param>
    /// <returns><c>true</c>, если точка внутри.</returns>
    public bool Contains(Vector2 point, float epsilon)
        => point.X >= Left - epsilon && point.X <= Right + epsilon
           && point.Y >= Top - epsilon && point.Y <= Bottom + epsilon;

    /// <summary>
    /// Проверяет, полностью ли лежит другой прямоугольник внутри текущего.
    /// </summary>
    /// <param name="other">Проверяемый прямоугольник.</param>
    /// <returns><c>true</c>, если прямоугольник внутри.</returns>
    public bool Contains(Rect other)
        => other.Left >= Left && other.Right <= Right && other.Top >= Top && other.Bottom <= Bottom;

    /// <summary>
    /// Проверяет пересечение прямоугольников.
    /// </summary>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns><c>true</c>, если прямоугольники пересекаются.</returns>
    public bool Intersects(Rect other)
        => other.Left < Right && other.Right > Left && other.Top < Bottom && other.Bottom > Top;

    /// <summary>
    /// Возвращает прямоугольник пересечения.
    /// </summary>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns>Общая часть или <see cref="Zero"/>, если пересечения нет.</returns>
    public Rect Intersection(Rect other)
    {
        float left = MathF.Max(Left, other.Left);
        float top = MathF.Max(Top, other.Top);
        float right = MathF.Min(Right, other.Right);
        float bottom = MathF.Min(Bottom, other.Bottom);
        return right <= left || bottom <= top
            ? Zero
            : new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Возвращает наименьший прямоугольник, содержащий оба.
    /// </summary>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns>Объединение прямоугольников.</returns>
    public Rect Union(Rect other)
        => new(
            MathF.Min(Left, other.Left),
            MathF.Min(Top, other.Top),
            MathF.Max(Right, other.Right) - MathF.Min(Left, other.Left),
            MathF.Max(Bottom, other.Bottom) - MathF.Min(Top, other.Top));

    /// <summary>
    /// Расширяет прямоугольник на заданные отступы.
    /// </summary>
    /// <param name="amount">Отступ. Отрицательное значение сжимает прямоугольник.</param>
    /// <returns>Расширенный прямоугольник.</returns>
    public Rect Inflate(Vector2 amount)
        => new(X - amount.X, Y - amount.Y, Width + amount.X * 2f, Height + amount.Y * 2f);

    /// <summary>
    /// Сжимает прямоугольник на заданные отступы.
    /// </summary>
    /// <param name="amount">Отступ. Отрицательное значение расширяет прямоугольник.</param>
    /// <returns>Сжатый прямоугольник.</returns>
    public Rect Deflate(Vector2 amount) => Inflate(-amount);

    /// <summary>
    /// Смещает прямоугольник.
    /// </summary>
    /// <param name="amount">Вектор смещения.</param>
    /// <returns>Смещённый прямоугольник.</returns>
    public Rect Offset(Vector2 amount) => new(X + amount.X, Y + amount.Y, Width, Height);

    /// <summary>
    /// Масштабирует прямоугольник относительно точки.
    /// </summary>
    /// <param name="factor">Множитель масштаба.</param>
    /// <param name="pivot">Неподвижная точка. По умолчанию левый верхний угол.</param>
    /// <returns>Масштабированный прямоугольник.</returns>
    public Rect Scale(float factor, Vector2? pivot = null)
    {
        Vector2 origin = pivot ?? Position;
        return new Rect(
            origin.X + (X - origin.X) * factor,
            origin.Y + (Y - origin.Y) * factor,
            Width * factor,
            Height * factor);
    }

    /// <summary>
    /// Поворачивает прямоугольник относительно точки.
    /// </summary>
    /// <param name="angle">Угол поворота против часовой стрелки.</param>
    /// <param name="pivot">Неподвижная точка. По умолчанию центр прямоугольника.</param>
    /// <returns>Повёрнутый прямоугольник.</returns>
    public Rect Rotate(Angle angle, Vector2? pivot = null)
    {
        Vector2 origin = pivot ?? Center;
        Vector2 rotation = angle.Rotate(new Vector2(X, Y) - origin) + origin;
        Vector2 opposite = angle.Rotate(new Vector2(Right, Bottom) - origin) + origin;
        return new Rect(rotation, opposite);
    }

    /// <summary>
    /// Преобразует прямоугольник в матрицу аффинного преобразования.
    /// </summary>
    /// <returns>Матрица, переводящая единичный квадрат в этот прямоугольник.</returns>
    public Matrix3x2 ToMatrix() => Matrix3x2.CreateScale(Width, Height) * Matrix3x2.CreateTranslation(Position);

    /// <inheritdoc/>
    public bool Equals(Rect other) => X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Rect other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    /// <summary>
    /// Сравнивает прямоугольники на равенство.
    /// </summary>
    /// <param name="left">Первый прямоугольник.</param>
    /// <param name="right">Второй прямоугольник.</param>
    /// <returns><c>true</c>, если прямоугольники равны.</returns>
    public static bool operator ==(Rect left, Rect right) => left.Equals(right);

    /// <summary>
    /// Сравнивает прямоугольники на неравенство.
    /// </summary>
    /// <param name="left">Первый прямоугольник.</param>
    /// <param name="right">Второй прямоугольник.</param>
    /// <returns><c>true</c>, если прямоугольники различаются.</returns>
    public static bool operator !=(Rect left, Rect right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Rect({X:F2}, {Y:F2}, {Width:F2}, {Height:F2})";
}
