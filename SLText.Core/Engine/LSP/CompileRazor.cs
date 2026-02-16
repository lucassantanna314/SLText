using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;

namespace SLText.Core.Engine.LSP;

public partial class LspService
{
    public (string code, RazorCSharpDocument? doc, IEnumerable<RazorDiagnostic> razorErrors) CompileRazorToCSharp(
        string razorCode, string fileName)
    {
        try
        {
            var fileSystem = new VirtualRazorFileSystem();
            var virtualPath = fileName.StartsWith("/") ? fileName : "/" + fileName;

            // Identifica o nome do assembly local (R-Hex)
            string assemblyName = GetProjectAssemblyName(_projectRoot).Replace(".dll", "").Trim();

            var sbImports = new StringBuilder();
            sbImports.AppendLine("@using System");
            sbImports.AppendLine("@using System.Collections.Generic");
            sbImports.AppendLine("@using Microsoft.AspNetCore.Components");
            sbImports.AppendLine("@using Microsoft.AspNetCore.Components.Web");
            sbImports.AppendLine("@using MudBlazor");
            sbImports.AppendLine("@using MudBlazor.Services");

            // Ativa o reconhecimento de componentes
            sbImports.AppendLine("@addTagHelper *, MudBlazor");
            if (!string.IsNullOrEmpty(assemblyName))
            {
                sbImports.AppendLine(string.Format("@addTagHelper *, {0}", assemblyName));
            }

            sbImports.AppendLine("@inherits Microsoft.AspNetCore.Components.ComponentBase");

            fileSystem.Add(new VirtualProjectItem("/", "/_Imports.razor", sbImports.ToString(), "component"));
            var projectItem = new VirtualProjectItem("/", virtualPath, razorCode, "component");
            fileSystem.Add(projectItem);

            var config = RazorConfiguration.Create(RazorLanguageVersion.Latest, "Blazor",
                Enumerable.Empty<RazorExtension>());

            var engine = RazorProjectEngine.Create(config, fileSystem, builder =>
            {
                builder.SetNamespace("SLText.Generated");

                var refs = _referencesMap.Values.ToList();
                var referenceFeature = new VirtualMetadataReferenceFeature();
                foreach (var r in refs) referenceFeature.References.Add(r);
                builder.Features.Add(referenceFeature);

                builder.Features.Add(new CompilationTagHelperFeature());
                builder.Features.Add(new DefaultTagHelperDescriptorProvider());
    
                builder.Features.Add(new DefaultMetadataReferenceFeature 
                { 
                    References = refs 
                });
            });

            var codeDocument = engine.Process(projectItem);

            try
            {
                var feature = engine.Engine.Features.OfType<ITagHelperFeature>().FirstOrDefault();
                if (feature != null)
                {
                    var prop = feature.GetType().GetProperty("TagHelpers") ??
                               feature.GetType().GetProperty("Descriptors");
                    if (prop != null)
                    {
                        var tagHelpers = prop.GetValue(feature) as System.Collections.IEnumerable;
                        int total = 0, mud = 0;
                        if (tagHelpers != null)
                        {
                            foreach (var item in tagHelpers)
                            {
                                total++;
                                dynamic dItem = item;
                                if (dItem.AssemblyName == "MudBlazor") mud++;
                            }
                        }

                        Console.WriteLine(string.Format("[RAZOR DEBUG] Total Tags: {0} | MudBlazor: {1}", total, mud));
                    }
                }
            }
            catch
            {
                /*  */
            }

            var csharpDocument = codeDocument.GetCSharpDocument();
            return (csharpDocument.GeneratedCode, csharpDocument, csharpDocument.Diagnostics);
        }
        catch (Exception ex)
        {
            Console.WriteLine(("[RAZOR FATAL] {0}", ex.Message));
            return (string.Empty, null, Enumerable.Empty<RazorDiagnostic>());
        }
    }

    public int MapGeneratedLineToSource(RazorCSharpDocument generatedDoc, int generatedLine)
    {
        var mapping = generatedDoc.SourceMappings.FirstOrDefault(m => 
            m.GeneratedSpan.LineIndex == generatedLine);

        return mapping != null ? mapping.OriginalSpan.LineIndex + 1 : generatedLine;
    }
    
    private class VirtualMetadataReferenceFeature : IMetadataReferenceFeature
    {
        public RazorEngine Engine { get; set; } = null!;
        public List<MetadataReference> References { get; } = new();
        IReadOnlyList<MetadataReference> IMetadataReferenceFeature.References => References;
    }

    private class VirtualRazorFileSystem : RazorProjectFileSystem
    {
        private readonly Dictionary<string, RazorProjectItem> _items = new();
        public void Add(RazorProjectItem item) => _items[item.FilePath] = item;
        public override IEnumerable<RazorProjectItem> EnumerateItems(string basePath) => _items.Values;
        public override RazorProjectItem GetItem(string path)
        {
            return GetItem(path, fileKind: null);
        }

        public override RazorProjectItem GetItem(string path, string? fileKind)
        {
            if (_items.TryGetValue(path, out var item)) return item;
            return new VirtualProjectItem(Path.GetDirectoryName(path) ?? "/", path, "");
        }
    }
    
    private class VirtualProjectItem : RazorProjectItem
    {
        private readonly byte[] _content;

        public VirtualProjectItem(string basePath, string filePath, string content, string fileKind = "component")
        {
            BasePath = basePath;
            FilePath = filePath; 
            FileKind = fileKind; 
            _content = Encoding.UTF8.GetBytes(content);
        }

        public override string BasePath { get; }
        public override string FilePath { get; }
        public override string FileKind { get; } 
        public override bool Exists => true;
        public override string? PhysicalPath => null;
        public string RelativePhysicalPath => FilePath.TrimStart('/');
    
        public override Stream Read() => new MemoryStream(_content);
    }
}