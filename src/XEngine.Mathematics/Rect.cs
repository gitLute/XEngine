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
    public float Left => MathF.Min(X, X + Width);

    /// <summary>
    /// Координата верхней стороны с учётом нормализации размера.
    /// </summary>
    public float Top => MathF.Min(Y, Y + Height);

    /// <summary>
    /// Координата правой стороны с учётом нормализации размера.
    /// </summary>
    public float Right => MathF.Max(X, X + Width);

    /// <summary>
    /// Координата нижней стороны с учётом нормализации размера.
    /// </summary>
    public float Bottom => MathF.Max(Y, Y + Height);

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
    /// <remarks>
    /// Пустой прямоугольник не содержится ни в чём, включая другой пустой:
    /// <see cref="Intersection"/> возвращает для непересекающихся областей
    /// именно пустой прямоугольник, и такой результат обязан отвечать «не
    /// содержится». То же правило и у <see cref="RectU.ContainsRect"/>, и
    /// разойтись здесь нельзя было: два типа прямоугольников отвечают на
    /// один и тот же вопрос по-разному.
    /// </remarks>
    public bool Contains(Rect other)
        => !other.IsEmpty
            && other.Left >= Left && other.Right <= Right && other.Top >= Top && other.Bottom <= Bottom;

    /// <summary>
    /// Проверяет пересечение прямоугольников. Границы включительные: касание
    /// ребром считается пересечением, как в <see cref="Aabb2"/> и
    /// <see cref="RectU"/>.
    /// </summary>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns><c>true</c>, если прямоугольники пересекаются.</returns>
    public bool Intersects(Rect other)
        => other.Left <= Right && other.Right >= Left && other.Top <= Bottom && other.Bottom >= Top;

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
    /// Поворачивает прямоугольник относительно точки и возвращает ограничивающий
    /// прямоугольник результата.
    /// </summary>
    /// <param name="angle">Угол поворота против часовой стрелки.</param>
    /// <param name="pivot">Неподвижная точка. По умолчанию центр прямоугольника.</param>
    /// <returns>Осевой прямоугольник, содержащий повёрнутый.</returns>
    /// <remarks>
    /// Повёрнутый прямоугольник осевым не является, поэтому результат — это
    /// <em>объемлющий</em> осевой прямоугольник: поворачиваются все четыре
    /// угла, затем берутся минимум и максимум по каждой оси, как это делает
    /// <see cref="Aabb3.Transform"/>. Возвращать сами два повёрнутых угла
    /// нельзя: тип <see cref="Rect"/> задаёт угол и размер, а второй повёрнутый
    /// угол — это абсолютная координата, которая стала бы шириной и высотой.
    /// Поворот на угол, не кратный 90°, всегда увеличивает результат.
    /// </remarks>
    public Rect Rotate(Angle angle, Vector2? pivot = null)
    {
        Vector2 origin = pivot ?? Center;

        Span<Vector2> corners =
        [
            new Vector2(Left, Top),
            new Vector2(Right, Top),
            new Vector2(Right, Bottom),
            new Vector2(Left, Bottom),
        ];

        // Синус и косинус считаются один раз на все четыре угла, а не по паре
        // на угол: Angle.Rotate заново вызывает математическую библиотеку, и
        // четыре вызова вместо одного — самая дорогая часть поворота.
        (float sin, float cos) = angle.SinCos();

        Vector2 min = new(
            (corners[0].X - origin.X) * cos - (corners[0].Y - origin.Y) * sin + origin.X,
            (corners[0].X - origin.X) * sin + (corners[0].Y - origin.Y) * cos + origin.Y);
        Vector2 max = min;

        for (int index = 1; index < corners.Length; index++)
        {
            float x = corners[index].X - origin.X;
            float y = corners[index].Y - origin.Y;
            Vector2 rotated = new(x * cos - y * sin + origin.X, x * sin + y * cos + origin.Y);
            min = Vector2.Min(min, rotated);
            max = Vector2.Max(max, rotated);
        }

        return new Rect(min.X, min.Y, max.X - min.X, max.Y - min.Y);
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
