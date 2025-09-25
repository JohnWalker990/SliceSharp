using System;
using System.Collections.Generic;
using System.IO;

namespace SliceSharp.Cli
{
    /// <summary>
    /// Strongly-typed CLI options.
    /// </summary>
    internal sealed class CliOptions
    {
        #region PUBLIC-PROPERTIES

        /// <summary>
        /// Full path to the .sln file.
        /// </summary>
        public string SolutionPath { get; init; } = string.Empty;

        /// <summary>
        /// Root spec: 
        /// - "relative/or/absolute/path/to/File.cs#MethodName" (signature optional), or
        /// - "route:GET:/api/orders/{id}"
        /// </summary>
        public string RootSpec { get; init; } = string.Empty;

        /// <summary>
        /// Output directory for Slice.md and graph.dot.
        /// </summary>
        public string OutputDir { get; init; } = "./slice-output";

        /// <summary>
        /// Token budget (estimated). Default: 32k.
        /// </summary>
        public int TokenBudget { get; init; } = 32000;

        /// <summary>
        /// Max BFS depth (hops) from root. Default: 20.
        /// </summary>
        public int MaxDepth { get; init; } = 20;

        /// <summary>
        /// Whether to embed full file contents until budget is exhausted.
        /// </summary>
        public bool EmbedFullCode { get; init; } = true;

        /// <summary>
        /// Average characters per token (rough estimate).
        /// </summary>
        public double AvgCharsPerToken { get; init; } = 4.0;

        /// <summary>
        /// Optional override for MSBuild path (directory containing MSBuild.dll or the msbuild.exe path).
        /// </summary>
        public string? MSBuildPath { get; init; }

        /// <summary>
        /// If true (default), exporter strips 'using' directives, namespaces and blank lines to save tokens.
        /// </summary>
        public bool StripBoilerplate { get; init; } = true;

        /// <summary>
        /// Exclusion patterns for files or folders (regex). Prepopulated with defaults.
        /// </summary>
        public List<string> ExcludePatterns { get; } = new List<string>
        {
            // Folders
            @"[\\/](bin|obj)[\\/]",
            @"[\\/]Migrations[\\/]",
            @"[\\/]Test(s)?[\\/]",     // ...\Test\ oder ...\Tests\
            @"[\\/.]Tests?[\\/]",      // ...\.Test(s)\ (z. B. Foo.Tests\)

            // Generated files
            @"\.g\.cs$",
            @"\.g\.i\.cs$",
            @"\.designer\.cs$",
            @"\.generated\.cs$",
            @"AssemblyInfo\.cs$"
        };

        #endregion

        #region PUBLIC-METHODS

        /// <summary>
        /// Simple manual arg parsing.
        /// </summary>
        public static CliOptions Parse(string[] args)
        {
            var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a.StartsWith("--"))
                {
                    var key = a.Substring(2);
                    string val = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "true";
                    opts[key] = val;
                }
            }

            var solution = opts.TryGetValue("sln", out var sln) ? sln : string.Empty;
            var root = opts.TryGetValue("root", out var r) ? r : string.Empty;
            var outDir = opts.TryGetValue("out", out var o) ? o : "./slice-output";
            var budget = opts.TryGetValue("budgetTokens", out var b) && int.TryParse(b, out var bt) ? bt : 32000;
            var depth = opts.TryGetValue("maxDepth", out var d) && int.TryParse(d, out var md) ? md : 20;
            var full = !opts.TryGetValue("embedFullCode", out var efc) || efc.Equals("true", StringComparison.OrdinalIgnoreCase);
            var avg = opts.TryGetValue("avgCharsPerToken", out var acpt) && double.TryParse(acpt, out var avgVal) ? avgVal : 4.0;
            var msbuildPath = opts.TryGetValue("msbuildPath", out var mp) ? mp : null;
            var strip = !opts.TryGetValue("strip", out var st) || st.Equals("true", StringComparison.OrdinalIgnoreCase);

            return new CliOptions
            {
                SolutionPath = solution,
                RootSpec = root,
                OutputDir = outDir,
                TokenBudget = budget,
                MaxDepth = depth,
                EmbedFullCode = full,
                AvgCharsPerToken = avg,
                MSBuildPath = msbuildPath,
                StripBoilerplate = strip
            };
        }

        /// <summary>
        /// Validates user input.
        /// </summary>
        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(SolutionPath) || !File.Exists(SolutionPath))
            {
                error = "Missing or invalid --sln <path-to-solution.sln>.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(RootSpec))
            {
                error = "Missing --root. Use \"path/to/File.cs#MethodName\" or \"route:GET:/path\".";
                return false;
            }

            error = "";
            return true;
        }

        /// <summary>
        /// Prints usage instructions.
        /// </summary>
        public static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  SliceSharp.Cli --sln <path-to-solution.sln> --root <file.cs#MethodName | route:METHOD:/path> [--out ./slice-output] [--budgetTokens 32000] [--maxDepth 20] [--strip true]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine(@"  SliceSharp.Cli --sln C:\\src\\MyApp\\MyApp.sln --root ""src\\Api\\Controllers\\OrdersController.cs#GetById"" --budgetTokens 32000 --out .\\slices\\orders");
            Console.WriteLine(@"  SliceSharp.Cli --sln C:\\src\\MyApp\\MyApp.sln --root ""route:GET:/api/orders/42"" --budgetTokens 32000 --out .\\slices\\orders");
            Console.WriteLine("  Optional: --msbuildPath \"C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Current\\Bin\"");
        }
        #endregion
    }
}
