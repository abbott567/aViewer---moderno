using AViewer.Core.Models;
using AViewer.Core.Services;

namespace AViewer.Core.Tests;

public sealed class JsonExportServiceTests
{
    [Fact]
    public void Serialize_IncludesNodeAndProperties()
    {
        var node = new AccessibilityNode
        {
            Id = "1", Name = "Save", ControlType = "Button",
            Properties = [new("UIA", "Name", "Save")]
        };

        var json = JsonExportService.Serialize(node);

        Assert.Contains("Save", json);
        Assert.Contains("Button", json);
    }
}
