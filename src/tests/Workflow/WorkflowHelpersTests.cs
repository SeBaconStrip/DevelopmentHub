using DevelopmentHub.Workflow;
using FluentAssertions;

namespace DevelopmentHub.Tests.Workflow;

public sealed class WorkflowHelpersTests
{
    // ── Render — basic replacement ────────────────────────────────────────────

    [Fact]
    public void Render_ReplacesSimplePlaceholder()
    {
        var result = WorkflowHelpers.Render("Hello {{name}}!", new Dictionary<string, string> { ["name"] = "World" });
        result.Should().Be("Hello World!");
    }

    [Fact]
    public void Render_ReplacesCamelCasePlaceholder()
    {
        var result = WorkflowHelpers.Render(
            "{{applicationPath}}",
            new Dictionary<string, string> { ["applicationPath"] = @"C:\apps\myapp.exe" });
        result.Should().Be(@"C:\apps\myapp.exe");
    }

    [Fact]
    public void Render_ReplacesCamelCasePlaceholder_CaseInsensitively()
    {
        // Template written with different casing than the input key — should still match.
        var result = WorkflowHelpers.Render(
            "{{ApplicationPath}}",
            new Dictionary<string, string> { ["applicationPath"] = @"C:\apps\myapp.exe" });
        result.Should().Be(@"C:\apps\myapp.exe");
    }

    [Fact]
    public void Render_ReplacesMultiplePlaceholders()
    {
        var inputs = new Dictionary<string, string>
        {
            ["applicationPath"] = @"C:\apps\myapp.exe",
            ["targetDir"] = @"C:\deploy",
        };
        var result = WorkflowHelpers.Render("copy {{applicationPath}} to {{targetDir}}", inputs);
        result.Should().Be(@"copy C:\apps\myapp.exe to C:\deploy");
    }

    [Fact]
    public void Render_ReplacesPlaceholder_MultipleTimes()
    {
        var result = WorkflowHelpers.Render(
            "{{version}} and {{version}}",
            new Dictionary<string, string> { ["version"] = "1.0" });
        result.Should().Be("1.0 and 1.0");
    }

    [Fact]
    public void Render_LeavesUnknownPlaceholder_Unchanged()
    {
        var result = WorkflowHelpers.Render(
            "{{unknown}}",
            new Dictionary<string, string> { ["other"] = "value" });
        result.Should().Be("{{unknown}}");
    }

    [Fact]
    public void Render_ReturnsEmptyString_WhenTemplateIsNull()
    {
        var result = WorkflowHelpers.Render(null!, new Dictionary<string, string> { ["x"] = "y" });
        result.Should().BeEmpty();
    }

    [Fact]
    public void Render_ReturnsTemplate_WhenInputsAreEmpty()
    {
        var result = WorkflowHelpers.Render("no placeholders here", new Dictionary<string, string>());
        result.Should().Be("no placeholders here");
    }

    [Fact]
    public void Render_ReplacesPlaceholder_WithEmptyString_WhenValueIsEmpty()
    {
        var result = WorkflowHelpers.Render(
            "prefix-{{suffix}}-end",
            new Dictionary<string, string> { ["suffix"] = "" });
        result.Should().Be("prefix--end");
    }
}
