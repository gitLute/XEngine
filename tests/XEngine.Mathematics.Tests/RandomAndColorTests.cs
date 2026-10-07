using System.Numerics;
using System.Text.Json;
using XEngine.Mathematics;
using XEngine.Mathematics.Serialization;
using Xunit;

namespace XEngine.Mathematics.Tests;

public sealed class RandomAndColorTests
{
    [Fact]
    public void XorShift_ProducesSameSequenceForSameSeed()
    {
        XorShift64Star first = new(12345);
        XorShift64Star second = new(12345);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(first.NextUInt64(), second.NextUInt64());
        }
    }

    [Fact]
    public void XorShift_ResetRestoresSequence()
    {
        XorShift64Star generator = new(999);
        ulong[] firstRun = [generator.NextUInt64(), generator.NextUInt64(), generator.NextUInt64()];

        generator.Reset();
        ulong[] secondRun = [generator.NextUInt64(), generator.NextUInt64(), generator.NextUInt64()];

        Assert.Equal(firstRun, secondRun);
    }

    [Fact]
    public void XorShift_DifferentSeedsDiverge()
    {
        XorShift64Star first = new(1);
        XorShift64Star second = new(2);

        Assert.NotEqual(first.NextUInt64(), second.NextUInt64());
    }

    [Fact]
    public void XorShift_SurvivesZeroSeed()
    {
        XorShift64Star generator = new(0);

        Assert.NotEqual(0UL, generator.NextUInt64());
        Assert.NotEqual(generator.NextUInt64(), generator.NextUInt64());
    }

    [Fact]
    public void NextFloat_StaysInUnitRange()
    {
        XorShift64Star generator = new(7);

        for (int i = 0; i < 1000; i++)
        {
            float value = generator.NextFloat();
            Assert.True(value >= 0f && value < 1f);
        }
    }

    [Fact]
    public void NextInt_RespectsBounds()
    {
        XorShift64Star generator = new(11);

        for (int i = 0; i < 1000; i++)
        {
            int value = generator.NextInt(5, 9);
            Assert.InRange(value, 5, 8);
        }
    }

    [Fact]
    public void NextInt_RejectsInvertedBounds()
    {
        XorShift64Star generator = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.NextInt(10, 10));
    }

    [Fact]
    public void NextRange_StaysInBounds()
    {
        XorShift64Star generator = new(3);

        for (int i = 0; i < 500; i++)
        {
            float value = generator.NextRange(-2f, 3f);
            Assert.InRange(value, -2f, 3f);
        }
    }

    [Fact]
    public void NextRange_RejectsInvertedBounds()
    {
        XorShift64Star generator = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.NextRange(5f, 1f));
    }

    [Fact]
    public void NextDirection_ReturnsUnitVector()
    {
        XorShift64Star generator = new(21);

        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(1f, generator.NextDirection().Length(), 1e-4f);
        }
    }

    [Fact]
    public void NextInsideUnitCircle_StaysInside()
    {
        XorShift64Star generator = new(33);

        for (int i = 0; i < 200; i++)
        {
            Assert.True(generator.NextInsideUnitCircle().Length() <= 1f + 1e-4f);
        }
    }

    [Fact]
    public void NextInside_StaysInBounds()
    {
        XorShift64Star generator = new(41);
        Aabb bounds = new(new Vector2(-1f, -1f), new Vector2(2f, 3f));

        for (int i = 0; i < 200; i++)
        {
            Vector2 point = generator.NextInside(bounds);
            Assert.True(bounds.Contains(point) || MathF.Abs(point.X - bounds.Min.X) < 1e-3f || MathF.Abs(point.X - bounds.Max.X) < 1e-3f);
        }
    }

    [Fact]
    public void NextWeightedIndex_RespectsWeights()
    {
        XorShift64Star generator = new(5);
        float[] weights = [0f, 10f];

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(1, generator.NextWeightedIndex(weights));
        }
    }

    [Fact]
    public void NextWeightedIndex_RejectsAllZeroWeights()
    {
        XorShift64Star generator = new();

        Assert.Throws<ArgumentException>(() => generator.NextWeightedIndex([0f, 0f]));
    }

    [Fact]
    public void NextItem_ReturnsElementOfArray()
    {
        XorShift64Star generator = new(13);
        int[] items = [1, 2, 3];

        for (int i = 0; i < 100; i++)
        {
            Assert.Contains(generator.NextItem(items), items);
        }
    }

    [Fact]
    public void NextItem_RejectsEmptyArray()
    {
        XorShift64Star generator = new();

        Assert.Throws<ArgumentException>(() => generator.NextItem(Array.Empty<int>()));
    }

    [Fact]
    public void NextAngle_CoversFullTurn()
    {
        XorShift64Star generator = new(17);

        float minimum = float.MaxValue;
        float maximum = float.MinValue;
        for (int i = 0; i < 500; i++)
        {
            float degrees = (float)generator.NextAngle().Degrees;
            minimum = MathF.Min(minimum, degrees);
            maximum = MathF.Max(maximum, degrees);
        }

        Assert.True(minimum < -90f);
        Assert.True(maximum > 90f);
    }

    [Fact]
    public void Rgba32_FromHex_ParsesSixAndEightDigits()
    {
        Assert.Equal(new Rgba32(1f, 0f, 0f, 1f), Rgba32.FromHex("#FF0000"));
        Assert.Equal(0x80 / 255f, Rgba32.FromHex("#00FF0080").A, 1e-6f);
    }

    [Fact]
    public void Rgba32_FromHex_AcceptsMissingHashPrefix()
    {
        Assert.Equal(Rgba32.FromHex("#FF0000"), Rgba32.FromHex("FF0000"));
        Assert.Equal(Rgba32.FromHex("#FF0000"), Rgba32.FromHex("  #ff0000 "));
    }

    [Fact]
    public void Rgba32_FromHex_RejectsMalformedInput()
    {
        Assert.Throws<FormatException>(() => Rgba32.FromHex("#FFF"));
        Assert.Throws<FormatException>(() => Rgba32.FromHex("not-a-color"));
    }

    [Fact]
    public void Rgba32_ClampsChannels()
    {
        Rgba32 color = new(2f, -1f, 0.5f, 3f);

        Assert.Equal(1f, color.R, 1e-6f);
        Assert.Equal(0f, color.G, 1e-6f);
        Assert.Equal(1f, color.A, 1e-6f);
    }

    [Fact]
    public void Rgba32_ToVector4_MatchesChannels()
    {
        Vector4 vector = Rgba32.FromHex("#11223344").ToVector4();

        Assert.Equal(0x11 / 255f, vector.X, 1e-4f);
        Assert.Equal(0x44 / 255f, vector.W, 1e-4f);
    }

    [Fact]
    public void Rgba32_Lerp_InterpolatesChannels()
    {
        Rgba32 result = Rgba32.Lerp(Rgba32.Black, Rgba32.White, 0.5f);

        Assert.Equal(0.5f, result.R, 1e-4f);
        Assert.Equal(0.5f, result.G, 1e-4f);
        Assert.Equal(0.5f, result.B, 1e-4f);
        Assert.Equal(1f, result.A, 1e-4f);
    }

    [Fact]
    public void Rgba32_Lerp_InterpolatesAlphaWhenBothEndpointsDiffer()
    {
        Rgba32 from = Rgba32.Transparent;
        Rgba32 to = new Rgba32(1f, 1f, 1f, 1f);

        Rgba32 result = Rgba32.Lerp(from, to, 0.25f);

        Assert.Equal(0.25f, result.A, 1e-4f);
    }

    [Fact]
    public void Rgba32_WithBrightnessKeepsAlpha()
    {
        Rgba32 result = Rgba32.FromHex("#FFFFFF80").WithBrightness(0.5f);

        Assert.Equal(0.5f, result.R, 1e-3f);
        Assert.Equal(128 / 255f, result.A, 1e-3f);
    }

    [Fact]
    public void Rgba32_RoundTripsThroughHex()
    {
        Rgba32 original = Rgba32.FromHex("#204080FF");

        Assert.Equal(original, Rgba32.FromHex(original.ToString()));
    }

    [Fact]
    public void JsonConverters_RoundTripVectors()
    {
        JsonSerializerOptions options = new()
        {
            Converters = { new Vector2JsonConverter(), new Vector3JsonConverter(), new Vector4JsonConverter() },
        };

        string json = JsonSerializer.Serialize(new Vector3(1f, 2f, 3f), options);
        Vector3 result = JsonSerializer.Deserialize<Vector3>(json, options);

        Assert.Equal("[1,2,3]", json);
        Assert.Equal(1f, result.X, 1e-6f);
        Assert.Equal(3f, result.Z, 1e-6f);
    }

    [Fact]
    public void JsonConverters_ReadNamedVectorFields()
    {
        Vector2 result = JsonSerializer.Deserialize<Vector2>("{\"x\":4,\"y\":5}", new JsonSerializerOptions
        {
            Converters = { new Vector2JsonConverter() },
        });

        Assert.Equal(4f, result.X, 1e-6f);
        Assert.Equal(5f, result.Y, 1e-6f);
    }

    [Fact]
    public void JsonConverters_RoundTripAngle()
    {
        JsonSerializerOptions options = new() { Converters = { new AngleJsonConverter() } };

        string json = JsonSerializer.Serialize(Angle.FromDegrees(45), options);
        Angle result = JsonSerializer.Deserialize<Angle>(json, options);

        Assert.Equal(45, result.Degrees, 1e-3);
    }

    [Fact]
    public void JsonConverters_ReadAngleFromDegrees()
    {
        Angle result = JsonSerializer.Deserialize<Angle>("{\"degrees\":90}", new JsonSerializerOptions
        {
            Converters = { new AngleJsonConverter() },
        });

        Assert.Equal(90, result.Degrees, 1e-6);
    }

    [Fact]
    public void JsonConverters_RoundTripColor()
    {
        JsonSerializerOptions options = new() { Converters = { new Rgba32JsonConverter() } };

        Rgba32 original = Rgba32.FromHex("#336699FF");
        string json = JsonSerializer.Serialize(original, options);

        Assert.Equal("\"#336699FF\"", json);
        Assert.Equal(original, JsonSerializer.Deserialize<Rgba32>(json, options));
    }

    [Fact]
    public void JsonConverters_ReadColorFromChannelArray()
    {
        Rgba32 result = JsonSerializer.Deserialize<Rgba32>("[0.5,0.25,1,0.5]", new JsonSerializerOptions
        {
            Converters = { new Rgba32JsonConverter() },
        });

        Assert.Equal(0.5f, result.R, 1e-6f);
        Assert.Equal(0.25f, result.G, 1e-6f);
        Assert.Equal(1f, result.B, 1e-6f);
        Assert.Equal(0.5f, result.A, 1e-6f);
    }

    [Fact]
    public void JsonConverters_RejectMalformedVector()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Vector2>("\"oops\"", new JsonSerializerOptions
        {
            Converters = { new Vector2JsonConverter() },
        }));
    }
}
