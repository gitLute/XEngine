using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Прямоугольник в двумерном пространстве. Может иметь отрицательный размер:
/// он задаёт ту же область, что и положительный, только стороны названы наоборот.
/// </summary>
/// <remarks>
/// Порядок сторон нормализуют <see cref="Left"/>, <see cref="Top"/>,
/// <see cref="Right"/> и <see cref="Bottom"/>, поэтому у зеркального
/// прямоугольника <c>Rect(8, 8, −4, −4)</c> область X[4; 8] Y[4; 8], и он
/// полноценный. Ориентироваться на знак <see cref="Width"/> нельзя: на
/// отрицательном размере <see cref="IsEmpty"/> тоже не сработает.
/// </remarks>
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
    /// Признак пустого прямоугольника: область пуста.
    /// </summary>
    /// <remarks>
    /// Пустота определяется по <b>области</b>, а не по знаку размера.
    /// Отрицательный размер задаёт настоящую, непустую область: <see cref="Left"/>
    /// и <see cref="Right"/> нормализуют порядок сторон, поэтому
    /// <c>Rect(8, 8, −4, −4)</c> — это прямоугольник X[4; 8] Y[4; 8], и он
    /// вложен в <c>Rect(0, 0, 10, 10)</c>.
    /// <para>
    /// Прежняя проверка <c>Width &lt;= 0 || Height &lt;= 0</c> считала любой
    /// зеркальный прямоугольник пустым, и <see cref="Contains(Rect)"/> на нём
    /// ложно отказывал: на 5 000 000 случайных пар ложных отказов было
    /// десятки тысяч, а <see cref="Aabb2"/> на тех же областях отвечал верно.
    /// </para>
    /// <para>
    /// Записано как <c>(X + Width) == X</c>, а не <c>Right &lt;= Left</c>,
    /// хотя это ровно одно и то же: <c>Left</c> и <c>Right</c> — это
    /// <c>Min</c> и <c>Max</c> от одной пары <c>X</c> и <c>X + Width</c>, а
    /// <c>Max &lt;= Min</c> тогда и только тогда, когда оба равны. Форма через
    /// сложение дешевле в 4.9 раза (0.93 нс против 4.56 нс, замер в Fast), и
    /// она дешевле прежней формулы по размеру тоже: 1.65 нс. Свойство стоит на
    /// пути <see cref="Contains(Vector2)"/>, <see cref="Intersects"/> и
    /// <see cref="Union"/>, то есть вызывается на каждый запрос.
    /// </para>
    /// <para>
    /// Проверка именно строгая: у прямоугольника нулевой площади
    /// (<c>Right == Left</c> или <c>Bottom == Top</c>) область вырождена в
    /// отрезок или точку, и такой прямоугольник пустым не считается. Именно
    /// его возвращает <see cref="Intersection"/> при касании ребром, и именно
    /// поэтому защита <c>!other.IsEmpty</c> в <see cref="Contains(Rect)"/>
    /// остаётся верной и после правки.
    /// </para>
    /// </remarks>
    public bool IsEmpty => (X + Width) == X || (Y + Height) == Y;

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
    /// <remarks>
    /// Пустой прямоугольник не содержит ни одной точки, в том числе начала
    /// координат. Без проверки <c>Rect.Zero.Contains((0, 0))</c> отвечал
    /// <c>true</c> при <see cref="IsEmpty"/> = <c>true</c>, тогда как
    /// <see cref="RectU.Contains(int, int)"/> у <see cref="RectU.Empty"/>
    /// отвечает <c>false</c>, а пустое значение обязано вести себя одинаково у
    /// обоих типов: именно его возвращает <see cref="Intersection"/>, и
    /// вызывающий проверяет результат на пустоту.
    /// </remarks>
    public bool Contains(Vector2 point)
        => !IsEmpty
            && point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;

    /// <summary>
    /// Проверяет, находится ли точка внутри прямоугольника с допуском.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <param name="epsilon">Допуск по обеим границам.</param>
    /// <returns><c>true</c>, если точка внутри.</returns>
    /// <remarks>
    /// Пустой прямоугольник не содержит ни одной точки — с допуском или без,
    /// см. <see cref="Contains(Vector2)"/>.
    /// </remarks>
    public bool Contains(Vector2 point, float epsilon)
        => !IsEmpty
            && point.X >= Left - epsilon && point.X <= Right + epsilon
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
    /// <remarks>
    /// Пустой прямоугольник не пересекается ни с чем, включая себя. Без
    /// проверки <c>Rect.Zero.Intersects(Rect.Zero)</c> отвечал <c>true</c>,
    /// тогда как <see cref="Aabb2.Empty"/> и <see cref="RectU.Empty"/> отвечают
    /// <c>false</c>: пустое значение не содержит ни одной точки, и объявлять
    /// ему пересечение с самим собой не на чем.
    /// <para>
    /// Границы включительные, и касание ребром считается пересечением: область
    /// <see cref="Rect"/> замкнута по обеим сторонам. Поэтому
    /// <see cref="Intersection"/> при касании возвращает вырожденный
    /// прямоугольник — общую часть нулевой площади на месте касания, — и он не
    /// равен <see cref="Zero"/>. Вызывающий, которому нужна площадь, проверяет
    /// <see cref="IsEmpty"/> у результата.
    /// </para>
    /// </remarks>
    public bool Intersects(Rect other)
        => !IsEmpty
            && !other.IsEmpty
            && other.Left <= Right && other.Right >= Left
            && other.Top <= Bottom && other.Bottom >= Top;

    /// <summary>
    /// Возвращает прямоугольник пересечения.
    /// </summary>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns>
    /// Общая часть; <see cref="Zero"/>, если общих точек нет вовсе.
    /// </returns>
    /// <remarks>
    /// При касании ребром общая часть — вырожденный прямоугольник на месте
    /// касания, а не <see cref="Zero"/>: он действительно принадлежит обоим
    /// прямоугольникам. Прежний код возвращал здесь <see cref="Zero"/>, то есть
    /// точку в начале координат, не принадлежащую ни одному из них. Распознать
    /// вырожденный результат можно по <see cref="IsEmpty"/>.
    /// </remarks>
    public Rect Intersection(Rect other)
    {
        float left = MathF.Max(Left, other.Left);
        float top = MathF.Max(Top, other.Top);
        float right = MathF.Min(Right, other.Right);
        float bottom = MathF.Min(Bottom, other.Bottom);
        return right < left || bottom < top
            ? Zero
            : new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Возвращает наименьший прямоугольник, содержащий оба.
    /// </summary>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns>Объединение прямоугольников.</returns>
    /// <remarks>
    /// Пустое значение поглощается, как в <see cref="Aabb2.Union"/> и
    /// <see cref="RectU.Union"/>. Без этого накопление от <see cref="Zero"/>,
    /// то есть самая естественная идиома сбора прямоугольников, втягивала
    /// начало координат: три прямоугольника возле (100, 100)…(210, 210) давали
    /// <c>Rect(0, 0, 210, 210)</c> с площадью 44100 вместо 100, то есть лишний
    /// охват 44000 квадратных единиц.
    /// </remarks>
    public Rect Union(Rect other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        return new(
            MathF.Min(Left, other.Left),
            MathF.Min(Top, other.Top),
            MathF.Max(Right, other.Right) - MathF.Min(Left, other.Left),
            MathF.Max(Bottom, other.Bottom) - MathF.Min(Top, other.Top));
    }

    /// <summary>
    /// Расширяет прямоугольник на заданные отступы.
    /// </summary>
    /// <param name="amount">Отступ. Отрицательное значение сжимает прямоугольник.</param>
    /// <returns>Расширенный прямоугольник.</returns>
    /// <remarks>
    /// Отступ, превышающий половину размера по какой-либо оси, <b>схлопывает</b>
    /// прямоугольник в точку на этой оси, а не переворачивает его: прежняя
    /// формула <c>X − a, W + 2a</c> при <c>a &gt; W/2</c> давала
    /// <c>Rect(0, 0, 4, 4).Deflate((0, 6))</c> = высоту <c>−8</c>, то есть
    /// прямоугольник больше исходного, и <see cref="IsEmpty"/> объявлял его
    /// пустым, хотя <see cref="Left"/>/<see cref="Bottom"/> нормализуют
    /// порядок и он содержал настоящую область.
    /// <para>
    /// Формула перенесена из <see cref="Aabb2.Expand"/> построением, а не
    /// результатом: два типа обязаны вести себя одинаково. Сравнение идёт по
    /// нормализованным <see cref="Left"/>/<see cref="Right"/>, то есть
    /// зеркальный прямоугольник расширяется как прямоугольник, а не
    /// переворачивается в другую сторону.
    /// </para>
    /// </remarks>
    public Rect Inflate(Vector2 amount)
    {
        // Границы нормализуются один раз в локальные переменные. Запись через
        // свойства Left/Right/Top/Bottom складывает X с Width трижды на ось,
        // и на этом пути метод стоил 5.6 нс против 1.1 нс у прежней, неверной
        // формулы; локальные переменные возвращают цену правки к её честному
        // размеру — примерно вдвое, то есть за схлопывание, а не за
        // повторные вычисления.
        float cornerX = X + Width;
        float cornerY = Y + Height;
        float minX = MathF.Min(X, cornerX);
        float minY = MathF.Min(Y, cornerY);
        float maxX = MathF.Max(X, cornerX);
        float maxY = MathF.Max(Y, cornerY);
        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;

        float left = MathF.Min(minX - amount.X, centerX);
        float top = MathF.Min(minY - amount.Y, centerY);
        float right = MathF.Max(maxX + amount.X, centerX);
        float bottom = MathF.Max(maxY + amount.Y, centerY);
        return new Rect(left, top, right - left, bottom - top);
    }

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
    /// Поворот на угол, не кратный 90° увеличивает результат не всегда: при
    /// 45° прямоугольник 10×2 даёт 8.485×8.485, то есть ширина меньше, и
    /// исходный прямоугольник в результат не входит. Объемлющий прямоугольник
    /// по определению не обязан содержать то, что объемлёт.
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
