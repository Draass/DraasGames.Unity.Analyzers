#nullable disable

using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace DraasGames.Unity.Analyzers.Editor
{
    /// <summary>
    /// Adds the IDE-only code-fix assembly to generated projects that already load the compiler analyzer.
    /// </summary>
    public sealed class CodeFixProjectPostprocessor : AssetPostprocessor
    {
        private const string PackageAssetPath =
            "Packages/com.draasgames.unity-analyzers/Editor/DraasGames.Unity.Analyzers.Editor.asmdef";
        private const string BaseAnalyzerAssemblyName = "DraasGames.Unity.Analyzers.dll";
        private const string CodeFixAssemblyName = "DraasGames.Unity.Analyzers.CodeFixes.dll";

        /// <summary>
        /// Unity and the Rider project generator invoke this callback for every generated C# project.
        /// </summary>
        public static string OnGeneratedCSProject(string path, string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return content;
            }

            try
            {
                var codeFixAssemblyPath = GetCodeFixAssemblyPath();
                if (string.IsNullOrWhiteSpace(codeFixAssemblyPath) || !File.Exists(codeFixAssemblyPath))
                {
                    return content;
                }

                var document = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
                var project = document.Root;
                if (project == null || !ContainsAnalyzerReference(project, BaseAnalyzerAssemblyName))
                {
                    return content;
                }

                // Re-generation can invoke this callback repeatedly. Only analyzer items participate here;
                // ordinary assembly references must remain untouched and must not suppress this IDE analyzer.
                if (ContainsAnalyzerReference(project, CodeFixAssemblyName))
                {
                    return content;
                }

                var projectNamespace = project.Name.Namespace;
                var analyzerGroup = project.Elements(projectNamespace + "ItemGroup")
                    .FirstOrDefault(group => group.Elements()
                        .Any(item => string.Equals(item.Name.LocalName, "Analyzer", StringComparison.OrdinalIgnoreCase)));

                if (analyzerGroup == null)
                {
                    analyzerGroup = new XElement(projectNamespace + "ItemGroup");
                    var firstImport = project.Elements(projectNamespace + "Import").FirstOrDefault();
                    if (firstImport == null)
                    {
                        project.Add(analyzerGroup);
                    }
                    else
                    {
                        firstImport.AddBeforeSelf(analyzerGroup);
                    }
                }

                AddAnalyzerItem(analyzerGroup, projectNamespace, NormalizePath(codeFixAssemblyPath), content);

                return Serialize(document, content);
            }
            catch (XmlException)
            {
                return content;
            }
            catch (IOException)
            {
                return content;
            }
            catch (UnauthorizedAccessException)
            {
                return content;
            }
            catch (InvalidOperationException)
            {
                return content;
            }
        }

        private static string GetCodeFixAssemblyPath()
        {
            var package = PackageInfo.FindForAssetPath(PackageAssetPath);
            if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath))
            {
                return null;
            }

            return Path.Combine(package.resolvedPath, "Analyzers", CodeFixAssemblyName);
        }

        private static bool ContainsAnalyzerReference(XElement project, string assemblyFileName)
        {
            foreach (var item in project.Descendants())
            {
                if (!string.Equals(item.Name.LocalName, "Analyzer", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (PathEndsWith(item.Attribute("Include")?.Value, assemblyFileName) ||
                    PathEndsWith(item.Attribute("Update")?.Value, assemblyFileName))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddAnalyzerItem(XElement group, XNamespace projectNamespace, string path, string originalContent)
        {
            var analyzer = new XElement(
                projectNamespace + "Analyzer",
                new XAttribute("Include", path));
            var trailingWhitespace = group.Nodes()
                .OfType<XText>()
                .LastOrDefault(node => string.IsNullOrWhiteSpace(node.Value));

            if (trailingWhitespace == null || group.Nodes().LastOrDefault() != trailingWhitespace)
            {
                group.Add(analyzer);
                return;
            }

            var trailingValue = trailingWhitespace.Value;
            var newline = trailingValue.IndexOf("\r\n", StringComparison.Ordinal) >= 0
                ? "\r\n"
                : trailingValue.IndexOf('\n') >= 0 ? "\n" : GetNewline(originalContent);
            var groupIndent = GetIndentation(trailingValue);
            var childIndent = group.Nodes()
                .OfType<XText>()
                .Select(node => GetIndentation(node.Value))
                .FirstOrDefault(indent => indent.Length > groupIndent.Length);

            trailingWhitespace.Remove();
            group.Add(new XText(newline + (childIndent ?? groupIndent + "  ")));
            group.Add(analyzer);
            group.Add(new XText(newline + groupIndent));
        }

        private static string Serialize(XDocument document, string originalContent)
        {
            var serialized = document.ToString(SaveOptions.DisableFormatting);
            if (document.Declaration != null &&
                !serialized.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            {
                serialized = document.Declaration + GetNewline(originalContent) + serialized;
            }

            return serialized;
        }

        private static string GetIndentation(string whitespace)
        {
            if (string.IsNullOrEmpty(whitespace))
            {
                return string.Empty;
            }

            var newline = whitespace.LastIndexOf('\n');
            return newline >= 0 ? whitespace.Substring(newline + 1) : whitespace;
        }

        private static string GetNewline(string content)
        {
            return content.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
        }

        private static bool PathEndsWith(string value, string fileName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().Replace('\\', '/');
            var separator = normalized.LastIndexOf('/');
            var candidate = separator >= 0 ? normalized.Substring(separator + 1) : normalized;
            return string.Equals(candidate, fileName, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
