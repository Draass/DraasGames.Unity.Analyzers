using System.Text;
using System.Xml.Linq;
using DraasGames.Unity.Analyzers.Editor;
using UnityEditor.PackageManager;
using Xunit;

namespace DraasGames.Unity.Analyzers.Tests;

public sealed class IdeIntegrationTests
{
    [Fact]
    public void IdeIntegration_AddsCodeFixOnlyToAnalyzedProjects()
    {
        using var package = PackageFixture.Create(withCodeFixAssembly: true);
        const string noAnalyzerProject = "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"Game.cs\" /></ItemGroup></Project>";
        const string analyzerProject = "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Analyzer Include=\"Packages/com.draasgames.unity-analyzers/Analyzers/DraasGames.Unity.Analyzers.dll\" /></ItemGroup></Project>";
        const string referenceOnlyProject = "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Reference Include=\"DraasGames.Unity.Analyzers.dll\" /></ItemGroup></Project>";

        var noAnalyzerResult = CodeFixProjectPostprocessor.OnGeneratedCSProject("NoAnalyzer.csproj", noAnalyzerProject);
        var analyzerResult = CodeFixProjectPostprocessor.OnGeneratedCSProject("Analyzer.csproj", analyzerProject);
        var referenceOnlyResult = CodeFixProjectPostprocessor.OnGeneratedCSProject("Reference.csproj", referenceOnlyProject);

        Assert.Equal(noAnalyzerProject, noAnalyzerResult);
        Assert.Contains("DraasGames.Unity.Analyzers.CodeFixes.dll", analyzerResult, StringComparison.Ordinal);
        Assert.Contains("<Analyzer Include=\"", analyzerResult, StringComparison.Ordinal);
        Assert.DoesNotContain("DraasGames.Unity.Analyzers.CodeFixes.dll", referenceOnlyResult, StringComparison.Ordinal);

        using var missingAssembly = PackageFixture.Create(withCodeFixAssembly: false);
        var missingResult = CodeFixProjectPostprocessor.OnGeneratedCSProject("Missing.csproj", analyzerProject);
        Assert.Equal(analyzerProject, missingResult);
    }

    [Fact]
    public void IdeIntegration_PreservesProjectAndAvoidsDuplicates()
    {
        using var package = PackageFixture.Create(withCodeFixAssembly: true);
        const string project = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
            "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\" Sdk=\"Microsoft.NET.Sdk\">\r\n" +
            "  <PropertyGroup><RootNamespace>Игровой&amp;Проект</RootNamespace></PropertyGroup>\r\n" +
            "  <ItemGroup>\r\n" +
            "    <Compile Include=\"Scripts/Ünicode.cs\" />\r\n" +
            "    <Analyzer Include=\"Packages/com.draasgames.unity-analyzers/Analyzers/DraasGames.Unity.Analyzers.dll\" />\r\n" +
            "    <Reference Include=\"DraasGames.Unity.Analyzers.CodeFixes.dll\" />\r\n" +
            "  </ItemGroup>\r\n" +
            "</Project>";

        var first = CodeFixProjectPostprocessor.OnGeneratedCSProject("Namespaced.csproj", project);
        var second = CodeFixProjectPostprocessor.OnGeneratedCSProject("Namespaced.csproj", first);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", first, StringComparison.Ordinal);
        Assert.Contains("Игровой&amp;Проект", first, StringComparison.Ordinal);
        Assert.Contains("Scripts/Ünicode.cs", first, StringComparison.Ordinal);
        var parsed = XDocument.Parse(first, LoadOptions.PreserveWhitespace);
        var projectNamespace = parsed.Root!.Name.Namespace;
        Assert.Equal(
            "http://schemas.microsoft.com/developer/msbuild/2003",
            projectNamespace.NamespaceName);
        var analyzerIncludes = parsed
            .Descendants(projectNamespace + "Analyzer")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value != null)
            .ToArray();
        var codeFixAnalyzerIncludes = analyzerIncludes
            .Where(value => value!.EndsWith("DraasGames.Unity.Analyzers.CodeFixes.dll", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Single(codeFixAnalyzerIncludes);
        Assert.Contains(
            package.Path.Replace('\\', '/'),
            codeFixAnalyzerIncludes[0],
            StringComparison.Ordinal);
        Assert.Contains("DraasGames.Unity.Analyzers.CodeFixes.dll", first, StringComparison.Ordinal);
        Assert.Equal(first, second);
        var secondParsed = XDocument.Parse(second, LoadOptions.PreserveWhitespace);
        Assert.Single(
            secondParsed.Descendants(projectNamespace + "Analyzer"),
            element => element.Attribute("Include")?.Value
                .EndsWith("DraasGames.Unity.Analyzers.CodeFixes.dll", StringComparison.OrdinalIgnoreCase) == true);

        const string malformed = "<Project><ItemGroup>";
        Assert.Equal(
            malformed,
            CodeFixProjectPostprocessor.OnGeneratedCSProject("Malformed.csproj", malformed));
    }

    private sealed class PackageFixture : IDisposable
    {
        private PackageFixture(string path)
        {
            Path = path;
            PackageInfo.TestResolvedPath = path;
        }

        internal string Path { get; }

        internal static PackageFixture Create(bool withCodeFixAssembly)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "dgua-codefix-tests-Игры&Stuff-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(System.IO.Path.Combine(path, "Analyzers"));
            if (withCodeFixAssembly)
            {
                File.WriteAllText(
                    System.IO.Path.Combine(path, "Analyzers", "DraasGames.Unity.Analyzers.CodeFixes.dll"),
                    "test assembly placeholder",
                    Encoding.UTF8);
            }

            return new PackageFixture(path);
        }

        public void Dispose()
        {
            if (string.Equals(PackageInfo.TestResolvedPath, Path, StringComparison.Ordinal))
            {
                PackageInfo.TestResolvedPath = null;
            }

            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
