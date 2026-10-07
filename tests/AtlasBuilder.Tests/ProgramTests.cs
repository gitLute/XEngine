using Xunit;

namespace AtlasBuilder.Tests;

/// <summary>
/// Тесты командной строки: инструмент запускается на этапе сборки, поэтому
/// важны код возврата и созданные файлы, а не только вывод.
/// </summary>
public sealed class ProgramTests
{
    [Fact]
    public void Main_BuildsAtlasFromRepositorySources()
    {
        string repositoryRoot = RepositoryRoot.Find();
        string sources = Path.Combine(repositoryRoot, "assets", "textures", "source");

        using TestDirectory directory = new(nameof(Main_BuildsAtlasFromRepositorySources));
        string output = directory.CreateSubdirectory("out");

        int exitCode = Program.Main(["--source", sources, "--output", output]);

        Assert.Equal(0, exitCode);

        string imagePath = Path.Combine(output, AtlasFileWriter.ImageFileName);
        string descriptionPath = Path.Combine(output, AtlasFileWriter.DescriptionFileName);
        Assert.True(File.Exists(imagePath), $"Нет файла {imagePath}");
        Assert.True(File.Exists(descriptionPath), $"Нет файла {descriptionPath}");

        AtlasJsonDescription description = AtlasJson.ReadBuilt(descriptionPath);
        Assert.Equal("world", description.Name);
        Assert.Equal(2, description.Regions.Count);

        IntSize imageSize = new PngSourceReader().ReadSize(imagePath);
        Assert.Equal(new IntSize(description.Width, description.Height), imageSize);
    }

    [Fact]
    public void Main_ReportsBuildErrorWithDedicatedExitCode()
    {
        using TestDirectory directory = new(nameof(Main_ReportsBuildErrorWithDedicatedExitCode));
        string source = directory.CreateSubdirectory("source");
        string output = directory.CreateSubdirectory("out");
        File.WriteAllText(Path.Combine(source, "world.json"), """
            {
              "name": "world",
              "width": 16,
              "height": 16,
              "bleedingPixels": 2,
              "regions": [
                {
                  "name": "stone",
                  "source": "absent.png",
                  "rect": { "x": 0, "y": 0, "width": 8, "height": 8 },
                  "sizeTexels": { "x": 8, "y": 8 },
                  "texelsPerMeter": 16
                }
              ]
            }

            """);

        int exitCode = Program.Main(["--source", source, "--output", output]);

        Assert.Equal(Program.ExitBuildFailed, exitCode);
        Assert.False(File.Exists(Path.Combine(output, AtlasFileWriter.ImageFileName)));
    }

    [Fact]
    public void Main_ReportsUsageErrorWithoutArguments()
    {
        Assert.Equal(Program.ExitUsageError, Program.Main([]));
    }

    [Fact]
    public void Main_ReportsUsageErrorOnUnknownArgument()
    {
        using TestDirectory directory = new(nameof(Main_ReportsUsageErrorOnUnknownArgument));

        Assert.Equal(Program.ExitUsageError, Program.Main(["--unknown", directory.Root]));
    }

    [Fact]
    public void Main_PrintsHelpWithoutBuilding()
    {
        using TestDirectory directory = new(nameof(Main_PrintsHelpWithoutBuilding));
        StringWriter output = new();
        TextWriter previous = Console.Out;
        try
        {
            Console.SetOut(output);

            int exitCode = Program.Main(["--help"]);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Console.SetOut(previous);
        }

        Assert.Contains("--source", output.ToString(), StringComparison.Ordinal);
    }
}