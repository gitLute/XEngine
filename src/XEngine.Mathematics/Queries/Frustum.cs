using System.Numerics;
using System.Runtime.CompilerServices;

namespace XEngine.Mathematics;

/// <summary>
/// Пирамида видимости: шесть плоскостей отсечения из матрицы
/// <c>view * projection</c> (10.11).
/// </summary>
/// <remarks>
/// Шесть плоскостей извлекаются один раз на кадр при смене камеры; пересчёт
/// на каждый объект запрещён, поэтому тип хранит плоскости, а проверка
/// видимости только читает их.
/// <para>
/// Проверка консервативна: объект на границе остаётся видимым, потому что
/// ложное отрицание оборачивается мигающим объектом на краю кадра, а лишний
/// отрисованный объект стоит лишь одного вызова.
/// </para>
/// </remarks>
public sealed class Frustum
{
    /// <summary>
    /// Допуск на границе плоскости в метрах: объём, выступающий за плоскость
    /// на эту величину, ещё считается видимым. Объекты у края кадра не должны
    /// мигать при движении камеры (10.11).
    /// </summary>
    /// <remarks>
    /// Порог абсолютный, и это ограничение метода, а не свойство плоскостей:
    /// на мире с характерным размером фрустума меньше примерно <c>1e-4 м</c>
    /// допуск сравним с размером самого фрустума, и отсечение перестаёт
    /// отсекать. Ложных отсечений при этом не возникает: порог расширяет
    /// область видимости, но никогда не сужает её, то есть цена ошибки —
    /// лишняя отрисовка, а не исчезновение объекта.
    /// </remarks>
    private const float ContainmentTolerance = 1e-3f;

    private readonly Plane3[] _planes;

    private Frustum(Plane3[] planes)
    {
        ArgumentNullException.ThrowIfNull(planes);

        _planes = planes;
    }

    /// <summary>
    /// Шесть плоскостей пирамиды: ближняя, дальняя, левая, правая, нижняя и
    /// верхняя. Внутренняя сторона — положительная.
    /// </summary>
    public ReadOnlySpan<Plane3> Planes => _planes;

    /// <summary>
    /// Строит пирамиду видимости из матрицы <c>view * projection</c>.
    /// </summary>
    /// <param name="viewProjection">Произведение матриц вида и проекции.</param>
    /// <returns>Пирамида видимости.</returns>
    /// <exception cref="InvalidOperationException">
    /// Из матрицы не выводится ни одна плоскость отсечения: сумма или разность
    /// её строк даёт нулевую нормаль либо нечисловые коэффициенты. Признак
    /// потерянной матрицы, а не повод молча отсекать всё.
    /// </exception>
    /// <remarks>
    /// Матрица, собранная самой библиотекой (<see cref="Matrix4x4Extensions.CreatePerspective"/>,
    /// <see cref="Matrix4x4Extensions.CreateOrthographic"/>), отвергается только
    /// при <c>far/near ≳ 1e8</c> либо при бесконечной плоскости: обе границы
    /// лежат за пределами точности <c>float</c>, а не внутри законного диапазона.
    /// </remarks>
    public static Frustum FromViewProjection(in Matrix4x4 viewProjection)
    {
        // Каждая плоскость получается сложением или вычитанием строк clip-пространства:
        // отсечение -w <= x <= w, -w <= y <= w, -w <= z <= w раскладывается на
        // шесть полупространств, каждое из которых приводится к виду a·p + b >= 0.
        Plane3[] planes =
        [
            FromRowSum(viewProjection.M13 + viewProjection.M14, viewProjection.M23 + viewProjection.M24,
                viewProjection.M33 + viewProjection.M34, viewProjection.M43 + viewProjection.M44, "ближняя"),
            FromRowSum(viewProjection.M14 - viewProjection.M13, viewProjection.M24 - viewProjection.M23,
                viewProjection.M34 - viewProjection.M33, viewProjection.M44 - viewProjection.M43, "дальняя"),
            FromRowSum(viewProjection.M11 + viewProjection.M14, viewProjection.M21 + viewProjection.M24,
                viewProjection.M31 + viewProjection.M34, viewProjection.M41 + viewProjection.M44, "левая"),
            FromRowSum(viewProjection.M14 - viewProjection.M11, viewProjection.M24 - viewProjection.M21,
                viewProjection.M34 - viewProjection.M31, viewProjection.M44 - viewProjection.M41, "правая"),
            FromRowSum(viewProjection.M12 + viewProjection.M14, viewProjection.M22 + viewProjection.M24,
                viewProjection.M32 + viewProjection.M34, viewProjection.M42 + viewProjection.M44, "нижняя"),
            FromRowSum(viewProjection.M14 - viewProjection.M12, viewProjection.M24 - viewProjection.M22,
                viewProjection.M34 - viewProjection.M32, viewProjection.M44 - viewProjection.M42, "верхняя"),
        ];

        return new Frustum(planes);
    }

    /// <summary>
    /// Считает, сколько сфер пересекают пирамиду.
    /// </summary>
    /// <param name="frustum">Пирамида видимости.</param>
    /// <param name="bounds">Ограничивающие сферы.</param>
    /// <returns>Число видимых сфер.</returns>
    /// <remarks>
    /// Равносильно вызову <see cref="Intersects(in BoundingSphere)"/> для каждой
    /// сферы. Смысл отдельного метода только в пакетной форме: шесть плоскостей
    /// читаются из массива один раз до цикла, а не на каждой итерации.
    /// Замер на миллионе проверок даёт 124 мс против 141 мс у поштучного цикла,
    /// то есть около 12 % — поэтому поштучный вызов остаётся верным и по
    /// умолчанию, а пакетный стоит применять в цикле отсечения сцены.
    /// <para>
    /// Тип намеренно оставлен классом со ссылкой на массив плоскостей. Вариант
    /// со структурой из шести полей проверялся: он оказался на 28 % медленнее
    /// поштучного вызова (184 мс против 143 мс), потому что 96 байт структуры
    /// копируются при каждом обращении. Выигрыш даёт только пакетная форма.
    /// </para>
    /// </remarks>
    public static int CountVisible(Frustum frustum, scoped ReadOnlySpan<BoundingSphere> bounds)
    {
        ArgumentNullException.ThrowIfNull(frustum);

        Plane3[] planes = frustum._planes;
        Plane3 near = planes[0];
        Plane3 far = planes[1];
        Plane3 left = planes[2];
        Plane3 right = planes[3];
        Plane3 bottom = planes[4];
        Plane3 top = planes[5];

        int visible = 0;
        foreach (BoundingSphere sphere in bounds)
        {
            Vector3 center = sphere.Center;
            float radius = sphere.Radius;

            if (IsSphereVisible(near, center, radius)
                && IsSphereVisible(far, center, radius)
                && IsSphereVisible(left, center, radius)
                && IsSphereVisible(right, center, radius)
                && IsSphereVisible(bottom, center, radius)
                && IsSphereVisible(top, center, radius))
            {
                visible++;
            }
        }

        return visible;
    }

    /// <summary>
    /// Считает, сколько сфер пересекают пирамиду, обрабатывая по несколько сфер
    /// за один шаг через <see cref="Vector{T}"/>.
    /// </summary>
    /// <param name="frustum">Пирамида видимости.</param>
    /// <param name="centersX">Координата X центров.</param>
    /// <param name="centersY">Координата Y центров.</param>
    /// <param name="centersZ">Координата Z центров.</param>
    /// <param name="radii">Радиусы сфер.</param>
    /// <returns>Число видимых сфер.</returns>
    /// <remarks>
    /// Форма выбрана не случайно. Шесть проверок плоскостей независимы, поэтому
    /// их удобно выполнять сразу над группой сфер: при ширине вектора в восемь
    /// элементов один шаг обрабатывает восемь сфер.
    /// <para>
    /// Результат совпадает с поштучной проверкой точно, а не с допуском.
    /// </para>
    /// <para>
    /// Числа в наносекундах здесь не приводятся: они зависят от платформы, и
    /// сопоставимы только отношения. Выигрыш зависит и от формы данных: список
    /// сфер против раздельных массивов даёт разные величины, поэтому одно
    /// число без указания формы и объёма замера вводит в заблуждение — именно
    /// так в доктрине появилась неверная «в четыре раза». Разбор с числами и
    /// объёмом замера — в `Problems.md`, раздел 4.
    /// </para>
    /// <para>
    /// Цена — четыре отдельных массива вместо массива структур. Если собирать
    /// вектор из четырёх соседних сфер, расход на сборку съедает весь выигрыш,
    /// поэтому такая форма бесполезна для <see cref="ReadOnlySpan{T}"/> сфер.
    /// </para>
    /// </remarks>
    public static int CountVisible(
        Frustum frustum,
        scoped ReadOnlySpan<float> centersX,
        scoped ReadOnlySpan<float> centersY,
        scoped ReadOnlySpan<float> centersZ,
        scoped ReadOnlySpan<float> radii)
    {
        ArgumentNullException.ThrowIfNull(frustum);

        int count = centersX.Length;
        if (centersY.Length != count || centersZ.Length != count || radii.Length != count)
        {
            throw new ArgumentException("Массивы центров и радиусов должны быть одной длины.");
        }

        Plane3[] planes = frustum._planes;

        // Коэффициенты шести плоскостей разворачиваются в скаляры: иначе на
        // каждом шаге пришлось бы читать элемент массива, то есть разыменовывать
        // ссылку.
        Vector<float> nearX = new(planes[0].Normal.X);
        Vector<float> nearY = new(planes[0].Normal.Y);
        Vector<float> nearZ = new(planes[0].Normal.Z);
        Vector<float> nearD = new(planes[0].Distance);
        Vector<float> farX = new(planes[1].Normal.X);
        Vector<float> farY = new(planes[1].Normal.Y);
        Vector<float> farZ = new(planes[1].Normal.Z);
        Vector<float> farD = new(planes[1].Distance);
        Vector<float> leftX = new(planes[2].Normal.X);
        Vector<float> leftY = new(planes[2].Normal.Y);
        Vector<float> leftZ = new(planes[2].Normal.Z);
        Vector<float> leftD = new(planes[2].Distance);
        Vector<float> rightX = new(planes[3].Normal.X);
        Vector<float> rightY = new(planes[3].Normal.Y);
        Vector<float> rightZ = new(planes[3].Normal.Z);
        Vector<float> rightD = new(planes[3].Distance);
        Vector<float> bottomX = new(planes[4].Normal.X);
        Vector<float> bottomY = new(planes[4].Normal.Y);
        Vector<float> bottomZ = new(planes[4].Normal.Z);
        Vector<float> bottomD = new(planes[4].Distance);
        Vector<float> topX = new(planes[5].Normal.X);
        Vector<float> topY = new(planes[5].Normal.Y);
        Vector<float> topZ = new(planes[5].Normal.Z);
        Vector<float> topD = new(planes[5].Distance);

        Vector<float> tolerance = new(-ContainmentTolerance);
        Vector<float> zero = new(0f);
        Vector<float> one = new(1f);

        int lanes = Vector<float>.Count;
        Vector<float> total = new(0f);
        for (int index = 0; index + lanes <= count; index += lanes)
        {
            Vector<float> x = new(centersX.Slice(index, lanes));
            Vector<float> y = new(centersY.Slice(index, lanes));
            Vector<float> z = new(centersZ.Slice(index, lanes));
            Vector<float> radius = new(radii.Slice(index, lanes));

            Vector<float> mask = nearX * x;
            mask += nearY * y;
            mask += nearZ * z;
            mask += nearD + radius;

            Vector<float> accumulated = farX * x;
            accumulated += farY * y;
            accumulated += farZ * z;
            accumulated += farD + radius;
            mask = Vector.Min(mask, accumulated);

            accumulated = leftX * x;
            accumulated += leftY * y;
            accumulated += leftZ * z;
            accumulated += leftD + radius;
            mask = Vector.Min(mask, accumulated);

            accumulated = rightX * x;
            accumulated += rightY * y;
            accumulated += rightZ * z;
            accumulated += rightD + radius;
            mask = Vector.Min(mask, accumulated);

            accumulated = bottomX * x;
            accumulated += bottomY * y;
            accumulated += bottomZ * z;
            accumulated += bottomD + radius;
            mask = Vector.Min(mask, accumulated);

            accumulated = topX * x;
            accumulated += topY * y;
            accumulated += topZ * z;
            accumulated += topD + radius;
            mask = Vector.Min(mask, accumulated);

            // Минимум по всем шести плоскостям: сфера отсекается, когда хотя бы
            // одна плоскость оставила её целиком за собой. При векторной
            // обработке ветвление всё равно не помогает — считать всё пришлось
            // бы целиком, поэтому выбирается минимум, а не цепочка сравнений.
            total += Vector.ConditionalSelect(Vector.LessThan(mask, tolerance), zero, one);
        }

        int visible = (int)Vector.Sum(total);

        // Хвост, когда длина не кратна ширине вектора. Проверка идёт теми же
        // шестью плоскостями напрямую, а не через BoundingSphere: её
        // конструктор отвергает нечисловой радиус, то есть одно NaN в данных
        // уровня обрывало бы пакетный отсекатель исключением в середине кадра.
        for (int index = count - (count % lanes); index < count; index++)
        {
            if (IsSphereVisible(planes[0], centersX[index], centersY[index], centersZ[index], radii[index])
                && IsSphereVisible(planes[1], centersX[index], centersY[index], centersZ[index], radii[index])
                && IsSphereVisible(planes[2], centersX[index], centersY[index], centersZ[index], radii[index])
                && IsSphereVisible(planes[3], centersX[index], centersY[index], centersZ[index], radii[index])
                && IsSphereVisible(planes[4], centersX[index], centersY[index], centersZ[index], radii[index])
                && IsSphereVisible(planes[5], centersX[index], centersY[index], centersZ[index], radii[index]))
            {
                visible++;
            }
        }

        return visible;
    }

    /// <summary>
    /// Проверяет, что параллелепипед целиком внутри пирамиды.
    /// </summary>
    /// <param name="bounds">Ограничивающий параллелепипед.</param>
    /// <returns>
    /// <c>true</c>, если объём целиком внутри пирамиды с допуском
    /// <see cref="ContainmentTolerance"/>: плоскость, выходящая за которую
    /// объём меньше допуска, пересекать объём не считается.
    /// </returns>
    /// <remarks>
    /// Для отсечения этот метод непригоден: он отсекает объект, который
    /// пересекает пирамиду лишь частично. Отсечением занимается
    /// <see cref="Intersects(in Aabb3)"/>.
    /// </remarks>
    public bool Contains(in Aabb3 bounds)
    {
        // Пустой объём не содержится ни в чём, включая себя. Без этой строки он
        // считался бы содержащимся: у пустого параллелепипеда полуразмер равен
        // (−∞,−∞,−∞), проекция полуразмера на нормаль с нулевой компонентой даёт
        // NaN, и сравнение NaN < −допуск ложно для всех шести плоскостей.
        if (bounds.IsEmpty)
        {
            return false;
        }

        Vector3 center = bounds.Center;
        Vector3 half = bounds.HalfSize;

        foreach (Plane3 plane in _planes)
        {
            float nearest = plane.DistanceTo(center) - ProjectedRadius(plane.Normal, half);
            if (nearest < -ContainmentTolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Проверяет, что сфера целиком внутри пирамиды.
    /// </summary>
    /// <param name="bounds">Ограничивающая сфера.</param>
    /// <returns>
    /// <c>true</c>, если объём целиком внутри пирамиды с допуском
    /// <see cref="ContainmentTolerance"/>: плоскость, выходящая за которую
    /// объём меньше допуска, пересекать объём не считается.
    /// </returns>
    /// <remarks>
    /// Для отсечения этот метод непригоден, как и перегрузка для
    /// параллелепипеда: используйте <see cref="Intersects(in BoundingSphere)"/>.
    /// </remarks>
    public bool Contains(in BoundingSphere bounds)
    {
        foreach (Plane3 plane in _planes)
        {
            if (plane.DistanceTo(bounds.Center) - bounds.Radius < -ContainmentTolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Проверяет, пересекается ли параллелепипед с пирамидой видимости.
    /// </summary>
    /// <param name="bounds">Ограничивающий параллелепипед.</param>
    /// <returns><c>false</c>, только если объём целиком вне пирамиды.</returns>
    /// <remarks>
    /// Именно этот метод отсекает объекты. Он возвращает <c>false</c>, когда
    /// объём целиком находится за одной из плоскостей, и <c>true</c> при любом
    /// касании: ложное отрицание оборачивается объектом, который то появляется,
    /// то пропадает на краю кадра, а лишний вызов отрисовки стоит дешевле.
    /// </remarks>
    public bool Intersects(in Aabb3 bounds)
    {
        // Пустой объём не пересекается с пирамидой: в нём нет ни одной точки.
        // Отдельная строка нужна потому, что путь через ProjectedRadius даёт для
        // него NaN вместо расстояния, а NaN не проходит ни одно сравнение, то
        // есть объём остаётся видимым. Подробнее — в Contains.
        if (bounds.IsEmpty)
        {
            return false;
        }

        Vector3 center = bounds.Center;
        Vector3 half = bounds.HalfSize;

        foreach (Plane3 plane in _planes)
        {
            // Отсекается только объём, целиком лежащий за плоскостью, то есть
            // когда даже самая дальняя его точка смотрит наружу. Взять дальний
            // угол, а не ближний: ближний наружу означает лишь, что объём
            // пересекает плоскость, и отсекать его нельзя.
            float farthest = plane.DistanceTo(center) + ProjectedRadius(plane.Normal, half);
            if (farthest < -ContainmentTolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Проверяет, пересекается ли сфера с пирамидой видимости.
    /// </summary>
    /// <param name="bounds">Ограничивающая сфера.</param>
    /// <returns><c>false</c>, только если сфера целиком вне пирамиды.</returns>
    /// <remarks>
    /// Именно этот метод отсекает объекты, по тем же правилам, что и
    /// <see cref="Intersects(in Aabb3)"/>.
    /// </remarks>
    public bool Intersects(in BoundingSphere bounds)
    {
        foreach (Plane3 plane in _planes)
        {
            if (plane.DistanceTo(bounds.Center) + bounds.Radius < -ContainmentTolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Расстояние от центра параллелепипеда до плоскости, увеличенное или
    /// уменьшенное на проекцию полуразмера на нормаль. Это точная величина:
    /// ближайшая и дальняя точки параллелепипеда вдоль нормали отличаются от
    /// центра именно на неё, поэтому углы перебирать не нужно.
    /// </summary>
    /// <param name="normal">Единичная нормаль плоскости.</param>
    /// <param name="half">Половина размера параллелепипеда.</param>
    /// <returns>Проекция полуразмера на нормаль.</returns>
    private static float ProjectedRadius(in Vector3 normal, in Vector3 half)
        => (MathF.Abs(normal.X) * half.X) + (MathF.Abs(normal.Y) * half.Y) + (MathF.Abs(normal.Z) * half.Z);

    /// <summary>
    /// Видна ли сфера относительно одной плоскости.
    /// </summary>
    /// <remarks>
    /// Отсечение записывается как «не меньше порога», а не «меньше порога».
    /// На нечисловом значении первая запись ложна и сфера остаётся видимой,
    /// вторая истинна и сфера отсекается. Выбрана первая: она совпадает с
    /// <see cref="Intersects(in BoundingSphere)"/>, документированным
    /// эквивалентом пакетных методов, и она осторожна — объект с испорченными
    /// данными лучше отрисовать, чем молча убрать.
    /// </remarks>
    /// <param name="plane">Плоскость отсечения.</param>
    /// <param name="center">Центр сферы.</param>
    /// <param name="radius">Радиус сферы.</param>
    /// <returns><c>true</c>, если сфера пересекает пирамиду.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSphereVisible(in Plane3 plane, in Vector3 center, float radius)
        => !((plane.DistanceTo(center) + radius) < -ContainmentTolerance);

    /// <summary>
    /// Та же проверка для раздельных массивов: координаты не собираются в
    /// вектор, потому что в хвосте данные могут быть нечисловыми и собирать
    /// из них <see cref="BoundingSphere"/> нельзя.
    /// </summary>
    /// <param name="plane">Плоскость отсечения.</param>
    /// <param name="x">Координата X центра.</param>
    /// <param name="y">Координата Y центра.</param>
    /// <param name="z">Координата Z центра.</param>
    /// <param name="radius">Радиус сферы.</param>
    /// <returns><c>true</c>, если сфера пересекает пирамиду.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSphereVisible(in Plane3 plane, float x, float y, float z, float radius)
    {
        Vector3 normal = plane.Normal;
        float distance = (normal.X * x) + (normal.Y * y) + ((normal.Z * z) + plane.Distance);
        return !((distance + radius) < -ContainmentTolerance);
    }

    /// <summary>
    /// Собирает плоскость отсечения из суммы или разности строк clip-матрицы.
    /// </summary>
    /// <remarks>
    /// Сторож проверяет <b>вырожденность</b> плоскости, а не длину её сырых
    /// коэффициентов. Длина суммы строк зависит от проекции и для дальней
    /// плоскости перспективы законно равна примерно <c>2·near/far</c>: при
    /// <c>far = 10 км</c> и <c>near = 1 м</c> это <c>2e-7</c>, то есть в сто раз
    /// меньше прежнего порога <c>1e-6</c>. Сравнение с абсолютной длиной
    /// отвергало законные камеры, собранные самой библиотекой, и граница
    /// проходила по <c>far/near ≈ 2e6</c>. Относительная мера здесь не годится
    /// по той же причине: элементы проекционной матрицы по порядку единицы, и
    /// любой относительный порог выше <c>2e-8</c> отвергает ту же камеру.
    /// <para>
    /// Поэтому сравнение идёт с точным нулём: у живой плоскости коэффициенты
    /// ненулевые, сколь угодно малые, а у вырожденной — нулевые, потому что
    /// матрица уже потеряла информацию (при <c>far/near ≳ 1e8</c> элементы
    /// <c>M33</c> и <c>M34</c> совпадают побитово и дальняя плоскость не
    /// выводится вовсе). Нечисловые коэффициенты отвергаются отдельно: из
    /// бесконечной дальней плоскости получается фрустум с нормалями NaN, у
    /// которого расстояние до плоскости не определено, и отсечение по глубине
    /// молча отключается.
    /// </para>
    /// </remarks>
    private static Plane3 FromRowSum(float x, float y, float z, float offset, string name)
    {
        // Квадрат длины считается один раз и служит признаком живости сразу для
        // трёх случаев: нулевая нормала (квадрат равен нулю), нечисловые
        // коэффициенты (NaN) и переполнение (бесконечность). Отдельно проверяется
        // свободный член: нормаль при этом может остаться годной.
        float lengthSquared = (x * x) + (y * y) + (z * z);
        if (!float.IsFinite(lengthSquared) || !(lengthSquared > 0f))
        {
            throw new InvalidOperationException(
                $"Плоскость отсечения «{name}» вырождена: сумма строк clip-матрицы даёт нулевую нормаль. "
                + "У перспективы это означает отношение far/near, которое float не различает; "
                + "у произвольной матрицы — что произведение вида и проекции не собрано.");
        }

        if (!float.IsFinite(offset))
        {
            throw new InvalidOperationException(
                $"Плоскость отсечения «{name}» содержит нечисловый свободный член. "
                + "Проверьте, не задана ли ближняя или дальняя плоскость бесконечностью.");
        }

        return Plane3.FromCoefficients(new Vector3(x, y, z), offset);
    }
}