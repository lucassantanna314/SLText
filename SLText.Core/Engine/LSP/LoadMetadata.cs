using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SLText.Core.Engine.LSP;

public partial class LspService
{
    private void LoadMetadataReferences()
    {
        var loadedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);        
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrEmpty(trustedAssemblies))
        {
            foreach (var path in trustedAssemblies.Split(Path.PathSeparator))
            {
                if (AddReference(path)) loadedPaths.Add(Path.GetFileName(path));
            }
        }
        
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                {
                    if (AddReference(assembly.Location)) loadedPaths.Add(Path.GetFileName(assembly.Location));
                }
            }
            catch { }
        }
        
        if (!loadedPaths.Contains("System.Private.CoreLib.dll"))
        {
            var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            if (Directory.Exists(runtimeDir))
            {
                foreach (var dll in Directory.GetFiles(runtimeDir, "*.dll"))
                {
                    AddReference(dll);
                }
            }
        }
        
        LoadReferencesFromDirectory(AppDomain.CurrentDomain.BaseDirectory);
    }
    
    private bool AddReference(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
    
        var fileName = Path.GetFileName(path);
        if (!_referencesMap.ContainsKey(fileName))
        {
            try 
            {
                _referencesMap[fileName] = MetadataReference.CreateFromFile(path);
                return true;
            }
            catch { return false; }
        }
        return true;
    }

    private string GetProjectAssemblyName(string rootPath)
    {
        try
        {
            var csprojPath = Directory.GetFiles(rootPath, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (csprojPath == null) 
                return string.Empty; 

            var csprojContent = File.ReadAllText(csprojPath);
        
            var match = System.Text.RegularExpressions.Regex.Match(csprojContent, @"<AssemblyName>(.*?)</AssemblyName>");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim() + ".dll";
            }

            return Path.GetFileNameWithoutExtension(csprojPath) + ".dll";
        }
        catch
        {
            return string.Empty;
        }
    }
    
    private void LoadReferencesFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        foreach (var file in Directory.GetFiles(directoryPath, "*.dll"))
        {
            var fileName = Path.GetFileName(file);
            
            if (!_referencesMap.ContainsKey(fileName))
            {
                try
                {
                    var reference = MetadataReference.CreateFromFile(file);
                    _referencesMap.Add(fileName, reference);
                }
                catch 
                { 
                    //
                }
            }
        }
    }
    
    private void InitProject(IEnumerable<MetadataReference> refs, string rootPath)
    {
        var projectId = ProjectId.CreateNewId();
        
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            
        var projectInfo = ProjectInfo.Create(
                projectId, 
                VersionStamp.Create(), 
                "VirtualProject", 
                "VirtualProject", 
                LanguageNames.CSharp)
            .WithMetadataReferences(refs)
            .WithCompilationOptions(compilationOptions)
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Latest));
        
        if (_project != null)
        {
            var newSolution = _workspace.CurrentSolution.RemoveProject(_project.Id).AddProject(projectInfo);
            _workspace.TryApplyChanges(newSolution);
            _project = _workspace.CurrentSolution.GetProject(projectId)!;
        }
        else
        {
            _project = _workspace.AddProject(projectInfo);
        }
        
        const string baseGlobalUsings = @"global using System;
              global using System.Collections.Generic;
              global using System.IO;
              global using System.Linq;
              global using System.Net.Http;
              global using System.Threading;
              global using System.Threading.Tasks;
              global using Microsoft.AspNetCore.Builder;
              global using Microsoft.AspNetCore.Hosting;
              global using Microsoft.AspNetCore.Http;
              global using Microsoft.AspNetCore.Routing;
              global using Microsoft.Extensions.Configuration;
              global using Microsoft.Extensions.DependencyInjection;
              global using Microsoft.Extensions.Hosting;
              global using Microsoft.Extensions.Logging;
         
              global using Microsoft.AspNetCore.Components;
              global using Microsoft.AspNetCore.Components.Web;
              global using Microsoft.AspNetCore.Components.Forms;
              global using Microsoft.JSInterop;
              global using MudBlazor; 
              global using MudBlazor.Services;";
        
        var projectFolderUsings = GenerateProjectNamespaces(rootPath);
        var finalGlobalUsings = baseGlobalUsings + Environment.NewLine + projectFolderUsings;
        
        var docInfo = DocumentInfo.Create(
            DocumentId.CreateNewId(_project.Id),
            "GlobalUsings.g.cs", 
            filePath: "GlobalUsings.g.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(finalGlobalUsings), VersionStamp.Create()))
        );
        
        var solution = _workspace.CurrentSolution.AddDocument(docInfo);
        _workspace.TryApplyChanges(solution);
        
        _project = _workspace.CurrentSolution.GetProject(_project.Id)!;
    }
    
    private string GenerateProjectNamespaces(string rootPath)
    {
        var sb = new StringBuilder();
        var rootNamespace = GetProjectAssemblyName(rootPath).Replace(".dll", ""); 

        if (string.IsNullOrEmpty(rootNamespace)) return "";

        sb.AppendLine("global using " + rootNamespace + ";");

        var directories = Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories);
    
        foreach (var dir in directories)
        {
            if (dir.Contains($"{Path.DirectorySeparatorChar}obj") || 
                dir.Contains($"{Path.DirectorySeparatorChar}bin") || 
                dir.Contains(".git"))
                continue;

            if (Directory.GetFiles(dir, "*.cs").Any() || Directory.GetFiles(dir, "*.razor").Any())
            {
                var relativePath = Path.GetRelativePath(rootPath, dir);
            
                var namespaceSuffix = relativePath
                    .Replace(Path.DirectorySeparatorChar, '.')
                    .Replace(" ", "_")
                    .Replace("-", "_");

                if (!string.IsNullOrEmpty(namespaceSuffix))
                {
                    sb.AppendLine("global using " + rootNamespace + "." + namespaceSuffix + ";");
                }
            }
        }
    
        sb.AppendLine("global using " + rootNamespace + ".Shared;");
        // Console.WriteLine("[AUTO-IMPORT] Namespaces gerados:\n" + sb.ToString());
    
        return sb.ToString();
    }
}