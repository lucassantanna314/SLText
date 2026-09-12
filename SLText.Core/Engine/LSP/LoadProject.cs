using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SLText.Core.Engine.LSP;

public partial class LspService
{
    /// <summary>
    /// Loads a project folder: resolves references, then adds every source document to the
    /// analysis workspace in a single batch.
    /// </summary>
    /// <remarks>
    /// The previous implementation called <c>TryApplyChanges</c> once per file and re-fetched the
    /// project each time, which is quadratic in the number of files - a 500-file project produced
    /// 500 full solution clones.
    /// </remarks>
    public void LoadProjectFiles(string rootPath, Action<string>? onProgress = null)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            onProgress?.Invoke($"[WARN] Not a directory: {rootPath}");
            return;
        }

        _gate.Wait();
        try
        {
            onProgress?.Invoke($"Loading project: {rootPath}");
            _projectRoot = rootPath;

            var resolved = ReferenceResolver.Resolve(rootPath, onProgress);
            ApplyReferences(resolved.References, resolved.ProjectAssemblyNames);
            foreach (var note in resolved.Notes) onProgress?.Invoke($"[WARN] {note}");

            InitProject(resolved.References, rootPath);
            var projectId = _project!.Id;

            var files = CollectSourceFiles(rootPath, onProgress);
            var solution = _workspace.CurrentSolution;
            int added = 0;

            foreach (var file in files)
            {
                try
                {
                    var text = File.ReadAllText(file).Replace("\r\n", "\n");
                    var docInfo = DocumentInfo.Create(
                        DocumentId.CreateNewId(_project!.Id),
                        Path.GetFileName(file),
                        filePath: file,
                        loader: TextLoader.From(TextAndVersion.Create(SourceText.From(text), VersionStamp.Create())));

                    solution = solution.AddDocument(docInfo);
                    added++;
                }
                catch (Exception ex)
                {
                    onProgress?.Invoke($"[WARN] Skipped {file}: {ex.Message}");
                }
            }

            if (!_workspace.TryApplyChanges(solution))
            {
                onProgress?.Invoke("[ERROR] Could not apply documents to the workspace.");
                return;
            }

            _project = _workspace.CurrentSolution.GetProject(projectId)!;
            _razorFiles.Clear();

            bool implicitUsings = HasImplicitUsingsEnabled(rootPath);
            AddImplicitUsingsDocumentAsync(implicitUsings).GetAwaiter().GetResult();

            onProgress?.Invoke($"Ready: {added} source file(s) indexed.");
        }
        catch (Exception ex)
        {
            onProgress?.Invoke($"[ERROR] Project load failed: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private static List<string> CollectSourceFiles(string rootPath, Action<string>? onProgress)
    {
        var files = new List<string>();

        foreach (var file in Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories))
        {
            bool inBin = file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
            if (inBin) continue;

            bool inObj = file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
            string name = Path.GetFileName(file);

            if (inObj)
            {
                // Generated Razor code-behind is useful; other obj artefacts are not real sources.
                if (!name.EndsWith(".razor.g.cs", StringComparison.Ordinal)) continue;
            }

            if (name.EndsWith(".AssemblyAttributes.cs", StringComparison.Ordinal) ||
                name.EndsWith(".AssemblyInfo.cs", StringComparison.Ordinal) ||
                name.EndsWith(".GlobalUsings.g.cs", StringComparison.Ordinal))
            {
                continue;
            }

            files.Add(file);
        }

        onProgress?.Invoke($"Found {files.Count} C# source file(s).");
        return files;
    }

    /// <summary>
    /// Finds the namespaces that declare <paramref name="typeName"/>, used by the "add using" quick fix.
    /// </summary>
    /// <remarks>
    /// The previous implementation also walked the entire global namespace tree of every referenced
    /// assembly recursively. With ~180 references that visits hundreds of thousands of symbols and
    /// can take seconds; <see cref="Compilation.GetSymbolsWithName"/> already covers it.
    /// </remarks>
    public async Task<List<string>> GetTypeNamespacesAsync(string typeName)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(typeName)) return new List<string>();

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_project == null) return new List<string>();

            var compilation = await _project.GetCompilationAsync().ConfigureAwait(false);
            if (compilation == null) return new List<string>();

            return compilation.GetSymbolsWithName(typeName, SymbolFilter.Type)
                .Where(s => s.DeclaredAccessibility == Accessibility.Public)
                .Select(s => s.ContainingNamespace?.ToDisplayString() ?? string.Empty)
                .Where(ns => ns.Length > 0 && ns != "<global namespace>")
                .Distinct()
                .OrderBy(ns => ns, StringComparer.Ordinal)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Ensures the workspace document for <paramref name="filePath"/> holds <paramref name="code"/>
    /// and returns it. Must be called while holding <see cref="_gate"/>.
    /// </summary>
    private Document UpdateDocument(string code, string filePath)
    {
        if (_project == null)
        {
            InitProject(_references, string.Empty);
        }

        var normalized = code.Replace("\r\n", "\n");
        string safePath = string.IsNullOrEmpty(filePath) ? "/__sltext__/untitled.cs" : filePath;

        var document = _workspace.CurrentSolution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => string.Equals(d.FilePath, safePath, StringComparison.Ordinal));

        if (document == null)
        {
            var docInfo = DocumentInfo.Create(
                DocumentId.CreateNewId(_project!.Id),
                Path.GetFileName(safePath),
                filePath: safePath,
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(normalized), VersionStamp.Create())));

            var solution = _workspace.CurrentSolution.AddDocument(docInfo);
            _workspace.TryApplyChanges(solution);
        }
        else
        {
            // Skip the workspace round-trip entirely when nothing changed - this is the common case
            // while the user is merely moving the caret.
            var current = document.GetTextAsync().GetAwaiter().GetResult();
            if (current.ToString() != normalized)
            {
                var solution = _workspace.CurrentSolution.WithDocumentText(document.Id, SourceText.From(normalized));
                _workspace.TryApplyChanges(solution);
            }
        }

        _project = _workspace.CurrentSolution.GetProject(_project!.Id)!;
        return _project.Documents.First(d => string.Equals(d.FilePath, safePath, StringComparison.Ordinal));
    }
}
