using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Семантика значения для всех типов-структур библиотеки.
/// </summary>
/// <remarks>
/// Ни у одного типа-структуры раньше не проверялись <c>Equals(object)</c>,
/// <c>GetHashCode</c>, операторы сравнения и <c>ToString</c>. Это отдельный
/// класс дефектов, а не набор мелочей: без работающего хеша тип нельзя
/// положить в <see cref="HashSet{T}"/> или в словарь, а <c>ToString</c>
/// попадает в журнал и в сообщения об ошибках.
/// <para>
/// Здесь не выписываются ожидаемые строки по одной на тип: вместо этого
/// проверяются свойства, которые обязаны выполняться. Состав строки
/// проверяется по признакам — имя типа и разделители, — чтобы тест не
/// ломался от перестановки полей внутри.
/// </para>
/// </remarks>
public class ValueSemanticsTests
{
    /// <summary>
    /// Пары «равные значения» и «различающиеся значения» для каждого типа.
    /// Генерируются здесь, чтобы проверка контракта шла по одному коду на все
    /// типы, а не по копии на каждый тип.
    /// </summary>
    /// <returns>Набор проверок.</returns>
    /// <summary>
    /// Три равных экземпляра и два отличающихся для каждого типа, плюс признак,
    /// обязательный в строковом представлении.
    /// </summary>
    /// <remarks>
    /// Отличающихся экземпляра два, и различаются они разными полями: первый
    /// отличается первым полем, второй последним. Одного было бы
    /// недостаточно — реализация, сравнивающая только первое поле, прошла бы
    /// проверку на единственном различии, оставаясь сломанной для второго.
    /// </remarks>
    /// <remarks>
    /// Три равных экземпляра нужны для проверки транзитивности: третий нельзя
    /// получить через <c>Activator.CreateInstance</c>, он создал бы значение по
    /// умолчанию, то есть нулевое, а не равное исходному.
    /// </remarks>
    /// <returns>Набор проверок.</returns>
    public static TheoryData<string, string, object, object, object, object, object> Cases()
    {
        TheoryData<string, string, object, object, object, object, object> data = new();

        // Aabb2: отличие по Min и по Max.
        Add(
            "Aabb2", "Aabb2",
            new Aabb2(new Vector2(1, 2), new Vector2(3, 4)),
            new Aabb2(new Vector2(1, 2), new Vector2(3, 4)),
            new Aabb2(new Vector2(1, 2), new Vector2(3, 4)),
            new Aabb2(new Vector2(0, 0), new Vector2(3, 4)),
            new Aabb2(new Vector2(1, 2), new Vector2(9, 4)));

        // Aabb3: отличие по Min и по Max.
        Add(
            "Aabb3", "Aabb3",
            new Aabb3(new Vector3(1, 2, 3), new Vector3(4, 5, 6)),
            new Aabb3(new Vector3(1, 2, 3), new Vector3(4, 5, 6)),
            new Aabb3(new Vector3(1, 2, 3), new Vector3(4, 5, 6)),
            new Aabb3(new Vector3(0, 2, 3), new Vector3(4, 5, 6)),
            new Aabb3(new Vector3(1, 2, 3), new Vector3(4, 9, 6)));

        // Angle: у типа одно поле, поэтому второй отличающийся экземпляр
        // проверяет ту же границу с другой стороны. Различие взято в одну
        // тысячную градуса: равенство обязано различать и такое, независимо
        // от того, что строковое представление печатает сотые. Точность
        // сравнения не снижается под формат вывода — см.
        // AngleToString_ResolvesHundredthsOfDegree.
        Add(
            "Angle", "deg",
            Angle.FromDegrees(45f),
            Angle.FromDegrees(45f),
            Angle.FromDegrees(45f),
            Angle.FromDegrees(90f),
            Angle.FromDegrees(45.001f));

        // Circle2: отличие по центру и по радиусу.
        Add(
            "Circle2", "Circle2",
            new Circle2(new Vector2(1, 2), 3f),
            new Circle2(new Vector2(1, 2), 3f),
            new Circle2(new Vector2(1, 2), 3f),
            new Circle2(new Vector2(7, 2), 3f),
            new Circle2(new Vector2(1, 2), 3.5f));

        // Segment2: отличие по первому концу и по второму.
        Add(
            "Segment2", "Segment2",
            new Segment2(new Vector2(0, 0), new Vector2(1, 1)),
            new Segment2(new Vector2(0, 0), new Vector2(1, 1)),
            new Segment2(new Vector2(0, 0), new Vector2(1, 1)),
            new Segment2(new Vector2(2, 0), new Vector2(1, 1)),
            new Segment2(new Vector2(0, 0), new Vector2(1, 2.5f)));

        // Capsule2: отличие по сегменту и по радиусу.
        Add(
            "Capsule2", "Capsule2",
            new Capsule2(new Segment2(new Vector2(0, 0), new Vector2(1, 0)), 0.5f),
            new Capsule2(new Segment2(new Vector2(0, 0), new Vector2(1, 0)), 0.5f),
            new Capsule2(new Segment2(new Vector2(0, 0), new Vector2(1, 0)), 0.5f),
            new Capsule2(new Segment2(new Vector2(5, 0), new Vector2(1, 0)), 0.5f),
            new Capsule2(new Segment2(new Vector2(0, 0), new Vector2(1, 0)), 0.75f));

        // Capsule3: отличие по концу оси и по радиусу.
        Add(
            "Capsule3", "Capsule3",
            new Capsule3(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 0.5f),
            new Capsule3(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 0.5f),
            new Capsule3(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 0.5f),
            new Capsule3(new Vector3(0, 5, 0), new Vector3(1, 0, 0), 0.5f),
            new Capsule3(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 0.75f));

        // Ray2: отличие по началу и по направлению.
        Add(
            "Ray2", "Ray2",
            new Ray2(new Vector2(1, 2), new Vector2(0, 1)),
            new Ray2(new Vector2(1, 2), new Vector2(0, 1)),
            new Ray2(new Vector2(1, 2), new Vector2(0, 1)),
            new Ray2(new Vector2(8, 2), new Vector2(0, 1)),
            new Ray2(new Vector2(1, 2), new Vector2(0.5f, 1)));

        // Ray3: отличие по началу и по направлению.
        Add(
            "Ray3", "Ray3",
            new Ray3(new Vector3(1, 2, 3), new Vector3(0, 1, 0)),
            new Ray3(new Vector3(1, 2, 3), new Vector3(0, 1, 0)),
            new Ray3(new Vector3(1, 2, 3), new Vector3(0, 1, 0)),
            new Ray3(new Vector3(9, 2, 3), new Vector3(0, 1, 0)),
            new Ray3(new Vector3(1, 2, 3), new Vector3(0, 1, 0.25f)));

        // Plane3: отличие по нормали и по смещению.
        Add(
            "Plane3", "Plane3",
            new Plane3(Vector3.UnitY, 5f),
            new Plane3(Vector3.UnitY, 5f),
            new Plane3(Vector3.UnitY, 5f),
            new Plane3(Vector3.UnitZ, 5f),
            new Plane3(Vector3.UnitY, 5.5f));

        // BoundingSphere: отличие по центру и по радиусу.
        Add(
            "BoundingSphere", "BoundingSphere",
            new BoundingSphere(new Vector3(1, 2, 3), 4f),
            new BoundingSphere(new Vector3(1, 2, 3), 4f),
            new BoundingSphere(new Vector3(1, 2, 3), 4f),
            new BoundingSphere(new Vector3(9, 2, 3), 4f),
            new BoundingSphere(new Vector3(1, 2, 3), 4.5f));

        // Rect: отличие по положению и по размеру.
        Add(
            "Rect", "Rect",
            new Rect(1f, 2f, 3f, 4f),
            new Rect(1f, 2f, 3f, 4f),
            new Rect(1f, 2f, 3f, 4f),
            new Rect(8f, 2f, 3f, 4f),
            new Rect(1f, 2f, 3f, 4.5f));

        // RectU и Rgba32 в строковом представлении не называют свой тип:
        // первый печатается как параметры окна, второй как hex-код.
        Add(
            "RectU", "w=",
            new RectU(1, 2, 3, 4),
            new RectU(1, 2, 3, 4),
            new RectU(1, 2, 3, 4),
            new RectU(9, 2, 3, 4),
            new RectU(1, 2, 3, 9));

        // У цвета каналы квантуются до байта, поэтому отличие подбирается
        // заведомо больше половины шага, иначе строки совпали бы.
        Add(
            "Rgba32", "#",
            new Rgba32(0.1f, 0.2f, 0.3f, 0.4f),
            new Rgba32(0.1f, 0.2f, 0.3f, 0.4f),
            new Rgba32(0.1f, 0.2f, 0.3f, 0.4f),
            new Rgba32(0.7f, 0.2f, 0.3f, 0.4f),
            new Rgba32(0.1f, 0.2f, 0.3f, 0.9f));

        return data;

        void Add(string typeName, string marker, object same, object copy, object third, object other, object otherLast)
        {
            _ = typeName;
            data.Add(marker, typeName, same, copy, third, other, otherLast);
        }
    }

    /// <summary>
    /// Экземпляры одного типа: равные значения равны, различающиеся — нет,
    /// хеши согласованы с равенством.
    /// </summary>
    /// <param name="marker">Обязательный признак в строковом представлении.</param>
    /// <param name="name">Имя типа для сообщения.</param>
    /// <param name="same">Первый экземпляр.</param>
    /// <param name="sameCopy">Второй равный экземпляр.</param>
    /// <param name="third">Третий равный экземпляр.</param>
    /// <param name="other">Экземпляр, отличающийся первым полем.</param>
    /// <param name="otherLast">Экземпляр, отличающийся последним полем.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void EqualsAndHashCode_FollowContract(
        string marker,
        string name,
        object same,
        object sameCopy,
        object third,
        object other,
        object otherLast)
    {
        ArgumentNullException.ThrowIfNull(same);
        ArgumentNullException.ThrowIfNull(sameCopy);
        ArgumentNullException.ThrowIfNull(third);
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(otherLast);

        _ = marker;
        System.Type type = same.GetType();
        System.Reflection.MethodInfo typed = type.GetMethod("Equals", [type])!;
        System.Reflection.MethodInfo boxed = type.GetMethod("Equals", [typeof(object)])!;

        Assert.True(typed.Invoke(same, [same]) is true, $"{name}: значение не равно самому себе.");
        Assert.True(typed.Invoke(same, [sameCopy]) is true, $"{name}: два равных экземпляра не равны.");
        Assert.True(typed.Invoke(sameCopy, [third]) is true, $"{name}: транзитивность нарушена.");
        Assert.True(typed.Invoke(same, [other]) is false, $"{name}: разные значения признаны равными.");
        Assert.True(typed.Invoke(other, [same]) is false, $"{name}: равенство несимметрично.");

        Assert.True(boxed.Invoke(same, [sameCopy]) is true, $"{name}: Equals(object) расходится с Equals(T).");
        Assert.True(boxed.Invoke(same, [other]) is false, $"{name}: Equals(object) расходится с Equals(T).");
        Assert.True(boxed.Invoke(same, [null]) is false, $"{name}: значение признано равным null.");
        Assert.True(boxed.Invoke(same, ["не тот тип"]) is false, $"{name}: значение признано равным чужому типу.");

        Assert.Equal(same.GetHashCode(), sameCopy.GetHashCode());
        Assert.Equal(same.GetHashCode(), third.GetHashCode());

        Assert.True(type.GetMethod("op_Equality")!.Invoke(null, [same, sameCopy]) is true, $"{name}: оператор == расходится с Equals.");
        Assert.True(type.GetMethod("op_Inequality")!.Invoke(null, [same, sameCopy]) is false, $"{name}: оператор != расходится с Equals.");
        Assert.True(type.GetMethod("op_Equality")!.Invoke(null, [same, other]) is false, $"{name}: оператор == признал разные значения равными.");
        Assert.True(type.GetMethod("op_Inequality")!.Invoke(null, [same, other]) is true, $"{name}: оператор != расходится с Equals.");

        // Отличие по последнему полю обязано обнаруживаться так же: реализация,
        // сравнивающая не всё, прошла бы проверку на одном различии.
        Assert.True(typed.Invoke(same, [otherLast]) is false, $"{name}: не различиено отличие по последнему полю.");
        Assert.True(boxed.Invoke(same, [otherLast]) is false, $"{name}: Equals(object) не различило отличие по последнему полю.");
        Assert.True(type.GetMethod("op_Equality")!.Invoke(null, [same, otherLast]) is false, $"{name}: оператор == не различил отличие по последнему полю.");
        Assert.False(Equals(same, otherLast), $"{name}: отличие по последнему полю не обнаружено.");
    }

    /// <summary>
    /// Строковое представление содержит признак типа и различает значения.
    /// </summary>
    /// <param name="marker">Обязательный признак в строковом представлении.</param>
    /// <param name="name">Имя типа для сообщения.</param>
    /// <param name="same">Первый экземпляр.</param>
    /// <param name="sameCopy">Второй равный экземпляр.</param>
    /// <param name="third">Третий равный экземпляр.</param>
    /// <param name="other">Экземпляр, отличающийся первым полем.</param>
    /// <param name="otherLast">Экземпляр, отличающийся последним полем.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ToString_ContainsTypeMarkerAndDistinguishes(
        string marker,
        string name,
        object same,
        object sameCopy,
        object third,
        object other,
        object otherLast)
    {
        ArgumentNullException.ThrowIfNull(same);
        ArgumentNullException.ThrowIfNull(sameCopy);
        ArgumentNullException.ThrowIfNull(third);
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(otherLast);

        string text = same.ToString()!;

        Assert.False(string.IsNullOrWhiteSpace(text), $"{name}: строковое представление пустое.");
        Assert.Contains(marker, text, StringComparison.Ordinal);

        Assert.Equal(text, sameCopy.ToString());
        Assert.Equal(text, third.ToString());

        // Отличие по первому полю обязано попадать в строку, иначе два разных
        // значения выглядели бы в журнале одинаково.
        Assert.NotEqual(text, other.ToString());
        _ = otherLast;
    }

    /// <summary>
    /// Строковое представление угла печатает сотые доли градуса и потому не
    /// различает углы, отличающиеся меньше чем на половину сотой.
    /// </summary>
    /// <remarks>
    /// Это свойство формата, а не дефект: два знака после запятой для
    /// диагностического вывода достаточно. Но оно обязано быть зафиксировано
    /// явно, иначе при попытке различить углы тоньше сотой пришлось бы
    /// молча ослаблять проверку равенства. Здесь утверждается именно граница
    /// формата, а в <see cref="EqualsAndHashCode_FollowContract"/> равенство
    /// проверяется на различии в 0.001° независимо от формата.
    /// </remarks>
    [Fact]
    public void AngleToString_ResolvesHundredthsOfDegree()
    {
        Angle baseAngle = Angle.FromDegrees(45f);

        // Различие в сотую градуса формат разрешает.
        Assert.NotEqual(baseAngle.ToString(), Angle.FromDegrees(45.01f).ToString());
        Assert.NotEqual(baseAngle.ToString(), Angle.FromDegrees(45.02f).ToString());

        // Различие в тысячную формат не разрешает, и это ожидаемо.
        Assert.Equal(baseAngle.ToString(), Angle.FromDegrees(45.001f).ToString());
        Assert.Equal(baseAngle.ToString(), Angle.FromDegrees(45.004f).ToString());

        // Но равенство такие углы различает: сравнение идёт по радианам.
        Assert.NotEqual(baseAngle, Angle.FromDegrees(45.001f));
        Assert.NotEqual(baseAngle.GetHashCode(), Angle.FromDegrees(45.001f).GetHashCode());
    }

    /// <summary>
    /// Структуры обязаны работать в наборах и словарях: если Equals и
    /// GetHashCode согласованы, значение найдётся.
    /// </summary>
    /// <param name="marker">Признак типа, не используется.</param>
    /// <param name="name">Имя типа для сообщения.</param>
    /// <param name="same">Первый экземпляр.</param>
    /// <param name="sameCopy">Второй равный экземпляр.</param>
    /// <param name="third">Третий равный экземпляр.</param>
    /// <param name="other">Экземпляр, отличающийся первым полем.</param>
    /// <param name="otherLast">Экземпляр, отличающийся последним полем.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void UsableAsDictionaryKey(
        string marker,
        string name,
        object same,
        object sameCopy,
        object third,
        object other,
        object otherLast)
    {
        ArgumentNullException.ThrowIfNull(same);
        ArgumentNullException.ThrowIfNull(sameCopy);
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(otherLast);
        _ = marker;
        _ = third;

        Dictionary<object, int> map = new() { [same] = 1, [other] = 2, [otherLast] = 3 };

        Assert.Equal(1, map[sameCopy]);
        Assert.Equal(2, map[other]);
        Assert.Equal(3, map[otherLast]);
        Assert.False(map.ContainsKey("не тот тип"), $"{name}: в словарь попал посторонний ключ.");

        HashSet<object> set = new() { same };
        Assert.True(set.Contains(sameCopy), $"{name}: равное значение не найдено в наборе.");
        Assert.False(set.Contains(other), $"{name}: разное значение найдено в наборе.");
        Assert.False(set.Contains(otherLast), $"{name}: разное значение по последнему полю найдено в наборе.");
    }

    /// <summary>
    /// Копирование через контейнер даёт независимый экземпляр: изменение копии
    /// не обязано менять оригинал, но обязано давать равное значение.
    /// </summary>
    [Fact]
    public void CopyingProducesIndependentValues()
    {
        var source = new List<Capsule3> { new(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 0.5f) };
        List<Capsule3> copy = new(source);

        Assert.Equal(source[0], copy[0]);

        copy[0] = new Capsule3(new Vector3(9, 9, 9), new Vector3(9, 9, 9), 1f);
        Assert.NotEqual(source[0], copy[0]);
    }
}
