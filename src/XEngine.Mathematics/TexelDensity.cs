using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Единая плотность текстуры: сколько текселей укладывается в метр поверхности.
/// </summary>
/// <remarks>
/// Требование — одинаковая плотность во всём мире, поэтому UV считаются не «на
/// глаз», а из физического размера поверхности и одной величины:
/// <code>
/// uvScale = repeatCount = (sizeMeters * texelsPerMeter) / textureSizeInTexels
/// </code>
/// <para>
/// Величина <c>texelsPerMeter</c> конфигурируется одна: сколько текселей
/// приходится на метр поверхности. Число повторов — производная величина, и в
/// структурах данных его нет (10.5).
/// </para>
/// <para>
/// Функции не хранят состояние: плотность передаётся параметром, поэтому
/// пригодны для теста с любой величиной и не зависят от состояния мира.
/// Хранение плотности живёт в конфигурации мира и передаётся по ссылке.
/// </para>
/// <para>
/// Округление не выполняется: оно изменило бы фактическую плотность. Бесшовный
/// тайлинг обеспечивается режимом повтора в шейдере, а не подгонкой числа
/// повторов; если текстуре нужно целое число повторов, это свойство текстуры,
/// а не плотности.
/// </para>
/// </remarks>
public static class TexelDensity
{
    /// <summary>
    /// Возвращает число повторов текстуры на поверхности заданного размера.
    /// </summary>
    /// <param name="sizeMeters">Размер поверхности в метрах по каждой оси.</param>
    /// <param name="texelsPerMeter">Плотность: текселей на метр.</param>
    /// <param name="textureTexels">Размер текстуры в текселях по каждой оси.</param>
    /// <returns>Число повторов по каждой оси; дробные значения сохраняются.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Размер поверхности, плотность или размер текстуры неположительны
    /// либо не конечны.
    /// </exception>
    public static Vector2 RepeatForSize(Vector2 sizeMeters, float texelsPerMeter, Vector2 textureTexels)
    {
        Validate(sizeMeters.X, nameof(sizeMeters));
        Validate(sizeMeters.Y, nameof(sizeMeters));
        Validate(texelsPerMeter, nameof(texelsPerMeter));
        Validate(textureTexels.X, nameof(textureTexels));
        Validate(textureTexels.Y, nameof(textureTexels));

        return new Vector2(
            (sizeMeters.X * texelsPerMeter) / textureTexels.X,
            (sizeMeters.Y * texelsPerMeter) / textureTexels.Y);
    }

    /// <summary>
    /// Возвращает масштаб UV для шейдера. Значение совпадает с числом повторов:
    /// различаются только названия — по смыслу это одна и та же величина, и
    /// расчёт живёт в <see cref="RepeatForSize(Vector2, float, Vector2)"/>, чтобы
    /// не разошлись два вычисления одного числа.
    /// </summary>
    /// <param name="sizeMeters">Размер поверхности в метрах по каждой оси.</param>
    /// <param name="texelsPerMeter">Плотность: текселей на метр.</param>
    /// <param name="textureTexels">Размер текстуры в текселях по каждой оси.</param>
    /// <returns>Значение для uniform-параметра шейдера.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Размер поверхности, плотность или размер текстуры неположительны
    /// либо не конечны.
    /// </exception>
    public static Vector2 UvScaleForSize(Vector2 sizeMeters, float texelsPerMeter, Vector2 textureTexels)
        => RepeatForSize(sizeMeters, texelsPerMeter, textureTexels);

    /// <summary>
    /// Возвращает число повторов текстуры по трём осям: та же формула для
    /// граней примитивов, у которых три размера.
    /// </summary>
    /// <param name="sizeMeters">Размер поверхности в метрах по трём осям.</param>
    /// <param name="texelsPerMeter">Плотность: текселей на метр.</param>
    /// <param name="textureTexels">
    /// Размер текстуры в текселях; третья компонента используется как масштаб
    /// по оси Z и для плоских поверхностей равна первой.
    /// </param>
    /// <returns>Число повторов по осям X и Y.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Размер поверхности, плотность или размер текстуры неположительны
    /// либо не конечны.
    /// </exception>
    public static Vector2 RepeatForSize(Vector3 sizeMeters, float texelsPerMeter, Vector2 textureTexels)
    {
        Validate(sizeMeters.X, nameof(sizeMeters));
        Validate(sizeMeters.Y, nameof(sizeMeters));
        Validate(texelsPerMeter, nameof(texelsPerMeter));
        Validate(textureTexels.X, nameof(textureTexels));
        Validate(textureTexels.Y, nameof(textureTexels));

        return new Vector2(
            (sizeMeters.X * texelsPerMeter) / textureTexels.X,
            (sizeMeters.Y * texelsPerMeter) / textureTexels.Y);
    }

    /// <summary>
    /// Возвращает длину окружности цилиндра: ширина развёртки его поверхности.
    /// </summary>
    /// <param name="radius">Радиус цилиндра в метрах.</param>
    /// <returns>Длина окружности в метрах.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Радиус отрицателен.</exception>
    public static float CylinderCircumference(float radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);

        return 2f * MathF.PI * radius;
    }

    private static void Validate(float value, string parameterName)
    {
        // Проверка построена на !(value > 0f), а не на value <= 0f: сравнение
        // с NaN всегда ложно, поэтому старая запись пропускала NaN и на выход
        // уходили UV-масштабы, которые нельзя ни отрисовать, ни отладить.
        // Бесконечность формально положительна, но осмысленного UV-масштаба
        // она тоже не даёт, поэтому отвергается вместе с NaN.
        if (!float.IsFinite(value) || value <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Размер поверхности, плотность и размер текстуры должны быть конечными и положительными.");
        }
    }
}