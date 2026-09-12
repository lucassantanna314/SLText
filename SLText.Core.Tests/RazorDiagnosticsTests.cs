using SLText.Core.Engine.LSP;
using Xunit;

namespace SLText.Core.Tests;

/// <summary>
/// Regression coverage for the in-process Razor pipeline.
///
/// Each test pins one way the engine used to disagree with <c>dotnet build</c>: the synthesized
/// <c>_Imports.razor</c> was written to a file system the engine never consulted; runtime code
/// generation carried so few source mappings that most Roslyn errors fell outside every mapping and
/// were dropped; the generated class was mangled to <c>__GeneratedComponent.AspNetCore_&lt;hash&gt;</c>
/// so it never merged with its <c>.razor.cs</c> code-behind; and no component tag-helper provider was
/// registered, so every component degraded to an unknown markup element and its
/// <c>RenderFragment&lt;T&gt;</c> child templates lost <c>context</c>.
/// </summary>
public sealed class RazorDiagnosticsTests : IClassFixture<RazorDiagnosticsTests.BlazorProjectFixture>
{
    private readonly BlazorProjectFixture _project;

    public RazorDiagnosticsTests(BlazorProjectFixture project) => _project = project;

    [Fact]
    public async Task ImportsFile_IsAppliedToTheGeneratedCode()
    {
        // CultureInfo comes from the fixture's _Imports.razor and from nowhere else.
        var diagnostics = await _project.DiagnoseAsync("@CultureInfo.InvariantCulture", "ImportsProbe.razor");

        Assert.DoesNotContain(diagnostics, d => d.Id == "CS0103" && d.Message.Contains("CultureInfo"));
    }

    [Fact]
    public async Task ExpressionError_IsReportedOnItsOwnRazorLine()
    {
        var diagnostics = await _project.DiagnoseAsync("<p>fine</p>\n@undefinedSymbol", "ExpressionProbe.razor");

        var error = Assert.Single(diagnostics, d => d.Id == "CS0103");
        Assert.Contains("undefinedSymbol", error.Message);
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public async Task CodeBehindPartial_MergesWithTheGeneratedClass()
    {
        // Thing.razor.cs declares `partial class Thing` in namespace Widget; Message lives only there.
        var diagnostics = await _project.DiagnoseAsync("<p>@Message</p>", "Thing.razor");

        Assert.DoesNotContain(diagnostics, d => d.Id == "CS0103" && d.Message.Contains("Message"));
    }

    [Fact]
    public async Task UnknownMarkupElement_IsStillReported()
    {
        var diagnostics = await _project.DiagnoseAsync("<DefinitelyNotAComponent />", "UnknownProbe.razor");

        Assert.Contains(diagnostics, d => d.Id == "RZ10012");
    }

    [Fact]
    public async Task ComponentThatIsNotInScope_IsReportedEvenThoughTheTypeExists()
    {
        // Virtualize is a real component in a referenced assembly, so the old name-based RZ10012
        // filter dropped this. Without the matching @using the build fails, and so must the editor.
        var diagnostics = await _project.DiagnoseAsync("<Virtualize />", "OutOfScopeProbe.razor");

        Assert.Contains(diagnostics, d => d.Id == "RZ10012" && d.Message.Contains("Virtualize"));
    }

    [Fact]
    public async Task ChildContentTemplate_BindsContextToTheItemType()
    {
        const string razor =
            "@using Microsoft.AspNetCore.Components.Web.Virtualization\n" +
            "<Virtualize TItem=\"string\" Items=\"_items\">\n" +
            "    <ItemContent>\n" +
            "        <p>@context.ToUpper()</p>\n" +
            "    </ItemContent>\n" +
            "</Virtualize>\n" +
            "@code { string[] _items = { \"a\" }; }";

        var diagnostics = await _project.DiagnoseAsync(razor, "ChildContentProbe.razor");

        Assert.DoesNotContain(diagnostics, d => d.Id == "CS0103" && d.Message.Contains("context"));
    }

    [Fact]
    public async Task ChildContentTemplate_ReportsAMemberMissingOnTheItemType()
    {
        const string razor =
            "@using Microsoft.AspNetCore.Components.Web.Virtualization\n" +
            "<Virtualize TItem=\"string\" Items=\"_items\">\n" +
            "    <ItemContent>\n" +
            "        <p>@context.NopeNotAMember</p>\n" +
            "    </ItemContent>\n" +
            "</Virtualize>\n" +
            "@code { string[] _items = { \"a\" }; }";

        var diagnostics = await _project.DiagnoseAsync(razor, "ChildContentTypoProbe.razor");

        Assert.Contains(diagnostics, d => d.Id == "CS1061" && d.Message.Contains("NopeNotAMember"));
    }

    /// <summary>
    /// A minimal Blazor-shaped project on disk. Only the .csproj, <c>_Imports.razor</c> and one
    /// code-behind are real files: <see cref="LspService.GetDiagnosticsAsync"/> takes the Razor
    /// source as a string and uses the path purely for project and namespace resolution, so each
    /// test can point at a file it never writes.
    /// </summary>
    public sealed class BlazorProjectFixture : IDisposable
    {
        private readonly string _root;
        private readonly LspService _lsp;

        public BlazorProjectFixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "sltext-razor-" + Guid.NewGuid().ToString("N"));
            var projectDir = Path.Combine(_root, "Widget");
            Directory.CreateDirectory(projectDir);

            File.WriteAllText(Path.Combine(projectDir, "Widget.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
                "  <PropertyGroup>\n" +
                "    <TargetFramework>net10.0</TargetFramework>\n" +
                "    <AssemblyName>Widget</AssemblyName>\n" +
                "  </PropertyGroup>\n" +
                "</Project>\n");

            File.WriteAllText(Path.Combine(projectDir, "_Imports.razor"), "@using System.Globalization\n");

            File.WriteAllText(Path.Combine(projectDir, "Thing.razor.cs"),
                "namespace Widget;\n\npublic partial class Thing\n{\n    protected string Message = \"hi\";\n}\n");

            _lsp = new LspService();
            _lsp.LoadProjectFiles(_root);
        }

        public Task<List<LspService.MappedDiagnostic>> DiagnoseAsync(string razor, string fileName) =>
            _lsp.GetDiagnosticsAsync(razor, Path.Combine(_root, "Widget", fileName));

        public void Dispose()
        {
            _lsp.Dispose();
            try { Directory.Delete(_root, recursive: true); }
            catch (IOException) { /* a transient handle in the test host must not fail the run */ }
        }
    }
}
