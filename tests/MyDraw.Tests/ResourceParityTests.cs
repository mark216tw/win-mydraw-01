using System.Xml.Linq;
using Xunit;

namespace Com.StudentTools.MyDraw.Tests;

public sealed class ResourceParityTests
{
    [Fact]
    public void LanguageResources_HaveMatchingKeys()
    {
        string stringsRoot = Path.Combine(FindRepositoryRoot(), "src", "MyDraw", "Strings");
        HashSet<string> zhKeys = ReadKeys(Path.Combine(stringsRoot, "zh-Hant", "Resources.resw"));
        HashSet<string> enKeys = ReadKeys(Path.Combine(stringsRoot, "en-US", "Resources.resw"));

        Assert.Empty(zhKeys.Except(enKeys));
        Assert.Empty(enKeys.Except(zhKeys));
    }

    private static HashSet<string> ReadKeys(string path) =>
        XDocument.Load(path).Root!
            .Elements("data")
            .Select(element => element.Attribute("name")!.Value)
            .ToHashSet(StringComparer.Ordinal);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "MyDraw", "Strings")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root.");
    }
}
