using Com.StudentTools.MyDraw.Helpers;
using Xunit;

namespace Com.StudentTools.MyDraw.Tests;

public sealed class ProjectActivationTests
{
    [Fact]
    public void GetProjectPath_ReturnsExistingProjectArgument()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mydraw-{Guid.NewGuid():N}.mydraw");
        File.WriteAllText(path, "test");
        try
        {
            string? result = ProjectActivation.GetProjectPath(["\"" + path + "\""]);

            Assert.Equal(Path.GetFullPath(path), result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetProjectPath_IgnoresMissingOrNonProjectArguments()
    {
        Assert.Null(ProjectActivation.GetProjectPath(["C:\\missing.mydraw"]));
        Assert.Null(ProjectActivation.GetProjectPath(["C:\\document.txt"]));
    }
}
