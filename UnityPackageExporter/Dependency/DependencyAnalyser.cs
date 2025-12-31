// System
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Microsoft
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace GUPS.UnityPackageExporter.Dependency
{
    /// <summary>
    /// Orchestrates dependency analysis for Unity projects.
    /// Combines asset analysis (YAML references) and script analysis (C# symbol references).
    /// </summary>
    class DependencyAnalyser : IDisposable
    {
        /// <summary>
        /// The root path of the project.
        /// </summary>
        public string RootPath { get; }

        /// <summary>
        /// Analyser for script (.cs) dependencies.
        /// </summary>
        private ScriptAnalyser scriptAnalyser;

        /// <summary>
        /// Analyser for asset (prefab, mat, etc.) dependencies.
        /// </summary>
        private AssetAnalyser assetAnalyser;

        /// <summary>
        /// Private constructor. Use CreateAsync to instantiate.
        /// </summary>
        /// <param name="rootPath">Project root path.</param>
        /// <param name="loggerFactory">Logger factory for creating sub-loggers.</param>
        private DependencyAnalyser(string rootPath, ILoggerFactory loggerFactory)
        {
            RootPath = rootPath;
            scriptAnalyser = new ScriptAnalyser(rootPath, loggerFactory.CreateLogger("ScriptAnalyser"));
            assetAnalyser = new AssetAnalyser(rootPath, loggerFactory.CreateLogger("AssetAnalyser"));
        }

        /// <summary>
        /// Creates a new DependencyAnalyser with default patterns.
        /// </summary>
        /// <param name="rootPath">Root path to look for assets in.</param>
        /// <param name="excludePatterns">Patterns to exclude from all results.</param>
        /// <param name="loggerFactory">Logger Factory.</param>
        /// <returns>Initialized DependencyAnalyser.</returns>
        public static Task<DependencyAnalyser> CreateAsync(string rootPath, IEnumerable<string> excludePatterns, ILoggerFactory loggerFactory)
            => CreateAsync(rootPath, new string[] { "**/*.meta" }, new string[] { "**/*.cs" }, excludePatterns, loggerFactory);

        /// <summary>
        /// Creates a new DependencyAnalyser with custom patterns.
        /// </summary>
        /// <param name="rootPath">Root path to look for assets in.</param>
        /// <param name="assetPatterns">Pattern to find assets. Recommended to scan only .meta files.</param>
        /// <param name="scriptPatterns">Pattern to find scripts. Recommended to scan only .cs files.</param>
        /// <param name="excludePatterns">Patterns to exclude from all results.</param>
        /// <param name="loggerFactory">Logger Factory.</param>
        /// <returns>Initialized DependencyAnalyser.</returns>
        public static async Task<DependencyAnalyser> CreateAsync(string rootPath, IEnumerable<string> assetPatterns, IEnumerable<string> scriptPatterns, IEnumerable<string> excludePatterns, ILoggerFactory loggerFactory)
        {
            DependencyAnalyser analyser = new DependencyAnalyser(rootPath, loggerFactory);

            // Build file maps. We dont build code map unless we need it (we might not).
            Matcher assetMatcher = new Matcher();
            assetMatcher.AddIncludePatterns(assetPatterns);
            assetMatcher.AddExcludePatterns(excludePatterns);
            var assetFiles = assetMatcher.GetResultsInFullPath(analyser.RootPath);
            
            // The asset dependency doesnt need this as it finds its own meta files
            Matcher scriptsMatcher = new Matcher();
            scriptsMatcher.AddIncludePatterns(scriptPatterns);
            scriptsMatcher.AddExcludePatterns(excludePatterns);
            var scriptFiles = scriptsMatcher.GetResultsInFullPath(analyser.RootPath);

            await Task.WhenAll(
                analyser.assetAnalyser.AddFilesAsync(assetFiles), 
                analyser.scriptAnalyser.AddFilesAsync(scriptFiles)
            );

            return analyser;
        }

        /// <summary>
        /// Finds all dependencies for the given set of files.
        /// Recursively checks both asset references and script code references.
        /// </summary>
        /// <param name="files">Initial files to check.</param>
        /// <returns>Collection of all dependent files.</returns>
        public async Task<IReadOnlyCollection<string>> FindDependenciesAsync(IEnumerable<string> files)
        {
            // Find a list of all assets that we need
            var assets = (await assetAnalyser.FindAllDependenciesAsync(files)).ToArray();

            // Find all the script assets from this list
            var scripts = await scriptAnalyser.FindAllDependenciesAsync(assets.Where(assetFile => Path.GetExtension(assetFile) == ".cs"));

            // Merge the lists
            HashSet<string> results = new HashSet<string>();
            foreach (var asset in assets) results.Add(asset);
            foreach (var script in scripts) results.Add(script);
            return results;
        }

        /// <summary>
        /// Disposes the underlying analysers.
        /// </summary>
        public void Dispose()
        {
            scriptAnalyser?.Dispose();
            scriptAnalyser = null;      // Setting to null isn't nessary but doing it to feel better.
            assetAnalyser = null;       // Setting to null isn't nessary but doing it to feel better.
        }
    }
}
