// System
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

// Microsoft
using Microsoft.Extensions.Logging;

// GuardingPearSoftware
using GUPS.UnityPackageExporter.Export;

namespace GUPS.UnityPackageExporter
{
    /// <summary>
    /// Main entry point for the Unity Package Exporter application.
    /// Handles command-line argument parsing and setup.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Exit code.</returns>
        static async Task<int> Main(string[] args)
        {
            var command = BuildCommands();
            return await command.Parse(args).InvokeAsync();
        }

        /// <summary>
        /// Builds the root command and configures options and arguments.
        /// </summary>
        /// <returns>Configured RootCommand.</returns>
        static RootCommand BuildCommands()
        {
            var sourceArg = new Argument<DirectoryInfo>(name: "source")
            {
                Description = "Unity Project Direcotry."
            };

            var outputArg = new Argument<FileInfo>(name: "output")
            {
                Description = "Output .unitypackage file"
            };

            var assetPatternOpt = new Option<IEnumerable<string>>("--assets", "-a")
            {
                Description = "Adds an asset to the pack. Supports glob matching.",
                DefaultValueFactory = _ => new[] { "**.*" }
            };

            var excludePatternOpt = new Option<IEnumerable<string>>("--exclude", "-e")
            {
                Description = "Excludes an asset from the pack. Supports glob matching.",
                DefaultValueFactory = _ => new[] { "Library/**.*", "**/.*" }
            };

            var skipDepOpt = new Option<bool>("--skip-dependency-check")
            {
                Description = "Skips dependency analysis. Disabling this feature may result in missing assets in your packages.",
                DefaultValueFactory = _ => false
            };

            var assetRootOpt = new Option<string>("--asset-root", "-r")
            {
                Description = "Sets the root directory for the assets. Used in dependency analysis to only check files that could be potentially included.",
                DefaultValueFactory = _ => "Assets"
            };

            var subFolderOpt = new Option<string>("--sub-folder", "-s")
            {
                Description = "Sets the child folder to all included files under.",
                DefaultValueFactory = _ => ""
            };

            var verboseOpt = new Option<LogLevel>("--verbose", "--log-level", "-v")
            {
                Description = "Sets the logging level",
                DefaultValueFactory = _ => LogLevel.Trace
            };

            var command = new RootCommand(description: "Packs the assets in a Unity Project")
            {
                sourceArg,
                outputArg,
                assetPatternOpt,
                excludePatternOpt,
                skipDepOpt,
                assetRootOpt,
                subFolderOpt,
                verboseOpt,
            };

            command.SetAction(async (parseResult) =>
            {
                var settings = new ExportSettings
                {
                    Source = parseResult.GetValue(sourceArg),
                    Output = parseResult.GetValue(outputArg),
                    Assets = parseResult.GetValue(assetPatternOpt),
                    Excludes = parseResult.GetValue(excludePatternOpt),
                    SkipDependencyCheck = parseResult.GetValue(skipDepOpt),
                    AssetRoot = parseResult.GetValue(assetRootOpt),
                    SubFolder = parseResult.GetValue(subFolderOpt),
                    Verbose = parseResult.GetValue(verboseOpt)
                };

                using var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder
                        .AddFilter("Microsoft", LogLevel.Warning)
                        .AddFilter("System", LogLevel.Warning)
                        .AddFilter("UnityPackageExporter", settings.Verbose)
                        .AddConsole();
                });

                var service = new ExportService(loggerFactory);
                await service.ExportAsync(settings);
            });

            return command;
        }
    }
}
