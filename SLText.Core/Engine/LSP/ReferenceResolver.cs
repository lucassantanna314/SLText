using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace SLText.Core.Engine.LSP;

/// <summary>
/// Resolves the metadata references of the project being edited.
///
/// The previous implementation walked the project tree looking for *.dll files. That only ever
/// finds assemblies that were physically copied to <c>bin</c>, which excludes every NuGet package
/// for Razor Class Library / Blazor WebAssembly projects - so MudBlazor.dll was never found and
/// every MudBlazor component was reported as an unknown element.
///
/// <c>obj/project.assets.json</c> is the authoritative restore output and lists the exact
/// compile-time asset of every package, so it is used as the primary source.
/// </summary>
internal static class ReferenceResolver
{
    public sealed record ResolutionResult(
        IReadOnlyList<MetadataReference> References,
        IReadOnlyList<string> ProjectAssemblyNames,
        IReadOnlyList<string> Notes);

    public static ResolutionResult Resolve(string rootPath, Action<string>? onProgress)
    {
        var notes = new List<string>();
        var byIdentity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var assemblyNames = new List<string>();

        bool hasRoot = !string.IsNullOrWhiteSpace(rootPath) && Directory.Exists(rootPath);

        if (hasRoot)
        {
            foreach (var projectDir in EnumerateProjectDirectories(rootPath))
            {
                var assetsPath = Path.Combine(projectDir, "obj", "project.assets.json");
                if (File.Exists(assetsPath))
                {
                    CollectFromAssetsFile(assetsPath, byIdentity, notes, onProgress);
                }
                else
                {
                    notes.Add($"No restore data for {Path.GetFileName(projectDir)} - run 'dotnet restore' for full IntelliSense.");
                }

                CollectFromOutputDirectory(projectDir, byIdentity, notes);

                var own = GetProjectAssemblyName(projectDir);
                if (own.Length > 0) assemblyNames.Add(own);
            }
        }

        // The BCL / shared framework comes from the runtime the editor itself runs on. Only
        // framework assemblies are taken - the editor's own third-party dependencies (SkiaSharp,
        // Silk.NET, Roslyn, ...) must not leak into the analysed project or they show up as
        // bogus completions.
        int frameworkCount = 0;
        foreach (var path in GetTrustedPlatformAssemblies())
        {
            if (!IsFrameworkAssembly(path)) continue;
            if (byIdentity.TryAdd(Path.GetFileName(path), path)) frameworkCount++;
        }

        var references = new List<MetadataReference>(byIdentity.Count);
        foreach (var path in byIdentity.Values)
        {
            try { references.Add(MetadataReference.CreateFromFile(path)); }
            catch (Exception ex) { notes.Add($"Skipped {Path.GetFileName(path)}: {ex.Message}"); }
        }

        onProgress?.Invoke($"References: {references.Count} total ({frameworkCount} framework, {byIdentity.Count - frameworkCount} project/package).");
        return new ResolutionResult(references, assemblyNames, notes);
    }

    private static IEnumerable<string> EnumerateProjectDirectories(string rootPath)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var csproj in Directory.EnumerateFiles(rootPath, "*.csproj", SearchOption.AllDirectories))
        {
            if (IsInsideBuildOutput(csproj)) continue;
            var dir = Path.GetDirectoryName(csproj);
            if (dir != null && seen.Add(dir)) yield return dir;
        }

        // A bare folder with no .csproj (loose files) still deserves the framework references.
        if (seen.Count == 0 && seen.Add(rootPath)) yield return rootPath;
    }

    private static void CollectFromAssetsFile(
        string assetsPath,
        Dictionary<string, string> byIdentity,
        List<string> notes,
        Action<string>? onProgress)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
            var root = document.RootElement;

            if (!root.TryGetProperty("packageFolders", out var foldersElement)) return;
            var packageFolders = foldersElement.EnumerateObject().Select(e => e.Name).ToList();
            if (packageFolders.Count == 0) return;

            if (!root.TryGetProperty("targets", out var targetsElement)) return;

            int found = 0;
            foreach (var targetFramework in targetsElement.EnumerateObject())
            {
                foreach (var library in targetFramework.Value.EnumerateObject())
                {
                    if (!library.Value.TryGetProperty("compile", out var compileAssets)) continue;

                    var separator = library.Name.IndexOf('/');
                    if (separator <= 0) continue;

                    // NuGet stores packages under a lower-cased id directory; Linux is case-sensitive.
                    var packageId = library.Name[..separator].ToLowerInvariant();
                    var packageVersion = library.Name[(separator + 1)..];

                    foreach (var asset in compileAssets.EnumerateObject())
                    {
                        if (asset.Name.EndsWith("/_", StringComparison.Ordinal)) continue;

                        var relative = asset.Name.Replace('/', Path.DirectorySeparatorChar);
                        foreach (var folder in packageFolders)
                        {
                            var candidate = Path.Combine(folder, packageId, packageVersion, relative);
                            if (File.Exists(candidate))
                            {
                                if (byIdentity.TryAdd(Path.GetFileName(candidate), candidate)) found++;
                                break;
                            }
                        }
                    }
                }
            }

            onProgress?.Invoke($"  {Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(assetsPath)) ?? "")}: {found} package assemblies resolved.");
        }
        catch (Exception ex)
        {
            notes.Add($"Failed to read {assetsPath}: {ex.Message}");
        }
    }

    private static void CollectFromOutputDirectory(string projectDir, Dictionary<string, string> byIdentity, List<string> notes)
    {
        var binDir = Path.Combine(projectDir, "bin");
        if (!Directory.Exists(binDir)) return;

        foreach (var dll in Directory.EnumerateFiles(binDir, "*.dll", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(dll);
            if (fileName.EndsWith(".Views.dll", StringComparison.OrdinalIgnoreCase)) continue;
            byIdentity.TryAdd(fileName, dll);
        }
    }

    private static IEnumerable<string> GetTrustedPlatformAssemblies() =>
        (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

    private static bool IsFrameworkAssembly(string path)
    {
        var fileName = Path.GetFileName(path);

        if (fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // ASP.NET Core / EFCore shared-framework assemblies ship with the runtime, not as packages.
        return fileName.StartsWith("Microsoft.AspNetCore.", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("Microsoft.JSInterop", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInsideBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    internal static string GetProjectAssemblyName(string projectDir)
    {
        try
        {
            var csproj = Directory.EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (csproj == null) return string.Empty;

            var content = File.ReadAllText(csproj);
            var match = System.Text.RegularExpressions.Regex.Match(content, @"<AssemblyName>\s*(.*?)\s*</AssemblyName>");
            if (match.Success) return match.Groups[1].Value.Trim();

            return Path.GetFileNameWithoutExtension(csproj);
        }
        catch
        {
            return string.Empty;
        }
    }
}
