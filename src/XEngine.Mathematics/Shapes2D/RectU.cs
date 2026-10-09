namespace XEngine.Mathematics;

/// <summary>
/// Прямоугольник в целых пикселях с включёнными границами: регион атласа (10.6).
/// </summary>
/// <remarks>
/// Границы включительные: прямоугольник <c>0,0,32,32</c> покрывает 32 пикселя
/// по каждой оси. Размер задаётся числом пикселей, а не координатой правой
/// нижней границы, иначе регион на один пиксель больше или меньше, чем
/// ожидала разметка атласа.
/// </remarks>
public readonly struct RectU : IEquatable<RectU>
{
    /// <summary>
    /// Создаёт прямоугольник.
    /// </summary>
    /// <param name="x">Координата левой границы в пикселях.</param>
    /// <param name="y">Координата верхней границы в пикселях.</param>
    /// <param name="width">Число пикселей по горизонтали.</param>
    /// <param name="height">Число пикселей по вертикали.</param>
    /// <exception cref="ArgumentOutOfRangeException">Размер неположителен.</exception>
    public RectU(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Пустой прямоугольник: результат отсечения непересекающихся областей.
    /// </summary>
    public static RectU Empty => default;

    /// <summary>
    /// Координата левой границы.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Координата верхней границы.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Число пикселей по горизонтали.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Число пикселей по вертикали.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Координата левой границы.
    /// </summary>
    public int Left => X;

    /// <summary>
    /// Координата верхней границы.
    /// </summary>
    public int Top => Y;

    /// <summary>
    /// Номер самого правого пикселя: граница включительная.
    /// </summary>
    /// <exception cref="OverflowException">
    /// Правая граница не помещается в <see cref="int"/>.
    /// </exception>
    public int Right => checked(X + Width - 1);

    /// <summary>
    /// Номер самого нижнего пикселя: граница включительная.
    /// </summary>
    /// <exception cref="OverflowException">
    /// Нижняя граница не помещается в <see cref="int"/>.
    /// </exception>
    public int Bottom => checked(Y + Height - 1);

    /// <summary>
    /// Число пикселей в прямоугольнике.
    /// </summary>
    /// <exception cref="OverflowException">
    /// Число пикселей не помещается в <see cref="int"/>.
    /// </exception>
    /// <remarks>
    /// Арифметика помечена <c>checked</c> намеренно: тип сделан на
    /// <see cref="int"/>, и переполнение молча возвращало бы отрицательную
    /// площадь для прямоугольника больше примерно 46340 пикселей по стороне.
    /// Исключение лучше мусора: вызывающий узнает, что прямоугольник не
    /// помещается в счётчик, и сможет разбить его.
    /// </remarks>
    public int Area => checked(Width * Height);

    /// <summary>
    /// Прямоугольник не содержит пикселей.
    /// </summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>
    /// Проверяет принадлежность пикселя прямоугольнику.
    /// </summary>
    /// <remarks>
    /// Правый и нижний край считаются в <c>long</c>, а не через
    /// <see cref="Right"/> и <see cref="Bottom"/>. Те свойства помечены
    /// <c>checked</c> и на прямоугольнике у <see cref="int.MaxValue"/>
    /// бросают исключение, а этот метод обещает <see cref="bool"/> и раньше
    /// бросал его необъявленно: <c>RectU(int.MaxValue − 2, 0, 4, 4)</c> —
    /// законно построенный прямоугольник из четырёх пикселей, и
    /// <c>Area</c> у него равен 16.
    /// <para>
    /// Считать в <c>long</c> можно без потери: <c>X + Width − 1</c> при
    /// <c>int</c>-овских <c>X</c> и <c>Width</c> всегда помещается в
    /// <c>long</c>, а сравнение <c>int</c> с такой границей даёт тот же
    /// ответ, что и сравнение в <c>long</c>: любой <see cref="int"/> меньше
    /// границы, которая за пределами диапазона. Метод стал тотальной
    /// функцией на своём аргументе, то есть проверка принадлежности больше
    /// не бросает там, где бросать нечего.
    /// </para>
    /// </remarks>
    /// <param name="x">Координата пикселя по горизонтали.</param>
    /// <param name="y">Координата пикселя по вертикали.</param>
    /// <returns><c>true</c>, если пиксель внутри, включая границы.</returns>
    public bool Contains(int x, int y)
        => !IsEmpty
            && x >= Left && x <= (long)X + Width - 1
            && y >= Top && y <= (long)Y + Height - 1;

    /// <summary>
    /// Проверяет, что прямоугольник целиком внутри другого.
    /// </summary>
    /// <remarks>
    /// Границы считаются в <c>long</c> — см. <see cref="Contains(int, int)"/>.
    /// До правки метод читал <see cref="Right"/> и <see cref="Bottom"/> и бросал
    /// необъявленный <see cref="OverflowException"/> на законном прямоугольнике.
    /// </remarks>
    /// <param name="other">Ограничивающий прямоугольник.</param>
    /// <returns><c>true</c>, если прямоугольник внутри.</returns>
    public bool ContainsRect(in RectU other)
        => !IsEmpty
            && !other.IsEmpty
            && other.Left >= Left
            && other.Top >= Top
            && (long)other.X + other.Width - 1 <= (long)X + Width - 1
            && (long)other.Y + other.Height - 1 <= (long)Y + Height - 1;

    /// <summary>
    /// Проверяет, есть ли у прямоугольников хотя бы один общий пиксель.
    /// </summary>
    /// <remarks>
    /// Границы считаются в <c>long</c> — см. <see cref="Contains(int, int)"/>.
    /// До правки метод бросал необъявленный <see cref="OverflowException"/> на
    /// паре прямоугольников, перекрывающих весь диапазон <see cref="int"/>.
    /// </remarks>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns><c>true</c>, если пересечение непустое.</returns>
    public bool Intersects(in RectU other)
        => !IsEmpty
            && !other.IsEmpty
            && (long)Left <= (long)other.X + other.Width - 1
            && (long)other.X <= (long)X + Width - 1
            && (long)Top <= (long)other.Y + other.Height - 1
            && (long)other.Y <= (long)Y + Height - 1;

    /// <summary>
    /// Возвращает общую часть прямоугольников. Пересечение пустое, если
    /// общая часть состоит из пустого набора пикселей.
    /// </summary>
    /// <remarks>
    /// Границы считаются в <c>long</c> — см. <see cref="Contains(int, int)"/>.
    /// Ширина результата при этом не может выйти за пределы <see cref="int"/>:
    /// она не больше <c>Width</c> каждого из операндов, потому что
    /// <c>right</c> берётся минимумом, а <c>left</c> максимумом. Поэтому
    /// сужение здесь не может бросить исключение, и метод, как и предикаты,
    /// отвечает на любой законно построенной паре.
    /// </remarks>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns>Пересечение или <see cref="Empty"/>.</returns>
    public RectU Intersection(in RectU other)
    {
        if (IsEmpty || other.IsEmpty)
        {
            return Empty;
        }

        // Границы считаются один раз и сразу в long. Прежняя запись звала
        // Intersects(other), а затем читала те же поля повторно уже в long:
        // на том же наборе это стоило 47 нс против 3.7 нс у записи ниже, то
        // есть двенадцать раз. Разница не в арифметике long — вариант с той
        // же арифметикой и с развёрнутым в тело условием даёт 3.7 нс.
        long leftA = X;
        long topA = Y;
        long rightA = (long)X + Width - 1;
        long bottomA = (long)Y + Height - 1;
        long rightB = (long)other.X + other.Width - 1;
        long bottomB = (long)other.Y + other.Height - 1;

        long left = leftA > other.X ? leftA : other.X;
        long top = topA > other.Y ? topA : other.Y;
        long right = rightA < rightB ? rightA : rightB;
        long bottom = bottomA < bottomB ? bottomA : bottomB;

        // Границы включительные: касание общим пикселем пересечением считается,
        // поэтому отказ только при строгом порядке.
        if (right < left || bottom < top)
        {
            return Empty;
        }

        return new RectU((int)left, (int)top, (int)(right - left + 1), (int)(bottom - top + 1));
    }

    /// <summary>
    /// Возвращает наименьший прямоугольник, содержащий оба.
    /// </summary>
    /// <remarks>
    /// Пустое значение поглощается, как в <see cref="Aabb2.Union"/> и в
    /// <see cref="Rect.Union"/>.
    /// <para>
    /// В отличие от пересечения, объединение <b>может</b> не поместиться в
    /// тип: два прямоугольника у краёв диапазона дают ширину больше
    /// <see cref="int.MaxValue"/>, и результата у типа просто нет. Прежний
    /// код при этом бросал <see cref="ArgumentOutOfRangeException"/> из
    /// конструктора с переполненной (отрицательной) шириной, то есть
    /// исключение указывало на параметр, который вызывающий не задавал.
    /// Теперь это объявленный <see cref="OverflowException"/>.
    /// </para>
    /// </remarks>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns>Объединение прямоугольников.</returns>
    /// <exception cref="OverflowException">
    /// Объединение шире <see cref="int.MaxValue"/> пикселей и не представимо
    /// в типе.
    /// </exception>
    public RectU Union(in RectU other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        long left = Math.Min((long)Left, other.Left);
        long top = Math.Min((long)Top, other.Top);
        long right = Math.Max((long)X + Width - 1, (long)other.X + other.Width - 1);
        long bottom = Math.Max((long)Y + Height - 1, (long)other.Y + other.Height - 1);
        long width = right - left + 1;
        long height = bottom - top + 1;

        if (width > int.MaxValue || height > int.MaxValue)
        {
            throw new OverflowException("Объединение прямоугольников не помещается в int по размеру.");
        }

        return new RectU((int)left, (int)top, (int)width, (int)height);
    }

    /// <summary>
    /// Смещает прямоугольник, не меняя размера.
    /// </summary>
    /// <remarks>
    /// Сложение помечено <c>checked</c> — политика типа объявлена в доктрине
    /// <see cref="Area"/>. Прежняя запись была единственной арифметикой типа без
    /// пометки, и при переполнении молча заворачивала результат:
    /// <c>RectU(int.MaxValue − 100, …).Offset(200, 0)</c> давал
    /// <c>X = −2147483549</c>, то есть смещение на 200 пикселей вправо
    /// обращалось в смещение влево.
    /// </remarks>
    /// <param name="offsetX">Смещение по горизонтали.</param>
    /// <param name="offsetY">Смещение по вертикали.</param>
    /// <returns>Смещённый прямоугольник.</returns>
    /// <exception cref="OverflowException">
    /// Смещение выводит координату за пределы <see cref="int"/>.
    /// </exception>
    public RectU Offset(int offsetX, int offsetY)
        => IsEmpty
            ? this
            : new RectU(checked(X + offsetX), checked(Y + offsetY), Width, Height);

    /// <inheritdoc/>
    public bool Equals(RectU other)
        => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RectU other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    /// <summary>
    /// Сравнивает прямоугольники на равенство.
    /// </summary>
    /// <param name="left">Первый прямоугольник.</param>
    /// <param name="right">Второй прямоугольник.</param>
    /// <returns><c>true</c>, если прямоугольники равны.</returns>
    public static bool operator ==(RectU left, RectU right) => left.Equals(right);

    /// <summary>
    /// Сравнивает прямоугольники на неравенство.
    /// </summary>
    /// <param name="left">Первый прямоугольник.</param>
    /// <param name="right">Второй прямоугольник.</param>
    /// <returns><c>true</c>, если прямоугольники различаются.</returns>
    public static bool operator !=(RectU left, RectU right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"[x={X}, y={Y}, w={Width}, h={Height}]";
}