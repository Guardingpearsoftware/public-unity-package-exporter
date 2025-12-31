// System
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

// Microsoft
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

// GuardingPearSoftware
using GUPS.UnityPackageExporter.Dependency;
using GUPS.UnityPackageExporter.Package;

namespace GUPS.UnityPackageExporter.Export
{
    /// <summary>
    /// Service responsible for orchestrating the export process.
    /// Handles directory setup, dependency analysis (if enabled), and packing assets.
    /// </summary>
    public class ExportService
    {
        /// <summary>
        /// Factory for creating other loggers.
        /// </summary>
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Logger instance.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the ExportService class.
        /// </summary>
        /// <param name="loggerFactory">Logger factory.</param>
        public ExportService(ILoggerFactory loggerFactory)
        {
            this._loggerFactory = loggerFactory;
            this._logger = this._loggerFactory.CreateLogger("Export");
        }

        /// <summary>
        /// Executes the export process based on the provided settings.
        /// </summary>
        /// <param name="settings">Export configuration settings.</param>
        public async Task ExportAsync(ExportSettings settings)
        {
            try
            {
                _logger.LogInformation("Packing {0}", settings.Source.FullName);

                // Make the output file (touch it) so we can exclude
                if (!settings.Output.Directory.Exists)
                {
                    settings.Output.Directory.Create();
                }
                
                await File.WriteAllBytesAsync(settings.Output.FullName, new byte[0]);

                Stopwatch timer = Stopwatch.StartNew();
                
                using DependencyAnalyser analyser = !settings.SkipDependencyCheck 
                    ? await DependencyAnalyser.CreateAsync(Path.Combine(settings.Source.FullName, settings.AssetRoot), settings.Excludes, _loggerFactory) 
                    : null;
                
                using Packer packer = new Packer(settings.Source.FullName, settings.Output.FullName, _loggerFactory.CreateLogger("Packer"))
                {
                    SubFolder = settings.SubFolder
                };

                // Match all the assets we need
                Matcher assetMatcher = new Matcher();
                assetMatcher.AddIncludePatterns(settings.Assets);
                assetMatcher.AddExcludePatterns(settings.Excludes);
                // Exclude the output file itself if it's inside the source directory
                assetMatcher.AddExclude(settings.Output.Name);

                var matchedAssets = assetMatcher.GetResultsInFullPath(settings.Source.FullName);
                await packer.AddAssetsAsync(matchedAssets);

                if (!settings.SkipDependencyCheck && analyser != null)
                {
                    var results = await analyser.FindDependenciesAsync(matchedAssets);
                    await packer.AddAssetsAsync(results);
                }

                _logger.LogInformation("Finished Packing in {0}ms", timer.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during export");
                throw;
            }
        }
    }
}
