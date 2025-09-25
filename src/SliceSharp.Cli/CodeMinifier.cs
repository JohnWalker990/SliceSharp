using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SliceSharp.Cli;



/// <summary>
/// Produces a token-lean view of a C# document:
/// - strips using directives
/// - unwraps namespaces (file-scoped and block-scoped)
/// - removes blank lines
/// This is only for exporting to Slice.md (does not modify source files).
/// </summary>
internal static class CodeMinifier
{
    public static async Task<string> MinifyDocumentAsync(Document doc, bool stripBoilerplate = true, bool stripBlankLines = true)
    {
        var root = await doc.GetSyntaxRootAsync().ConfigureAwait(false);
        if (root is not CompilationUnitSyntax cu)
        {
            var raw = await doc.GetTextAsync().ConfigureAwait(false);
            return raw.ToString();
        }

        if (!stripBoilerplate && !stripBlankLines)
        {
            var txt = await doc.GetTextAsync().ConfigureAwait(false);
            return txt.ToString();
        }

        CompilationUnitSyntax cu2 = cu;

        if (stripBoilerplate)
        {
            // Remove 'using' directives
            cu2 = cu2.WithUsings(new SyntaxList<UsingDirectiveSyntax>());

            // Unwrap namespaces to the compilation unit
            var newMembers = SyntaxFactory.List<MemberDeclarationSyntax>();
            foreach (var m in cu2.Members)
            {
                switch (m)
                {
                    case FileScopedNamespaceDeclarationSyntax fns:
                        newMembers = newMembers.AddRange(fns.Members);
                        break;
                    case NamespaceDeclarationSyntax nsd:
                        newMembers = newMembers.AddRange(nsd.Members);
                        break;
                    default:
                        newMembers = newMembers.Add(m);
                        break;
                }
            }
            cu2 = cu2.WithMembers(newMembers);
        }

        // Normalize then remove blank lines entirely
        var text = cu2.NormalizeWhitespace().ToFullString();

        if (stripBlankLines)
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                sb.AppendLine(line.TrimEnd());
            }
            text = sb.ToString();
        }

        return text;
    }
}