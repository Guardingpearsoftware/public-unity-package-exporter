// System
using System.Collections.Generic;
using System.IO;

namespace GUPS.UnityPackageExporter.Export
{
    /// <summary>
    /// Configuration settings for the export operation.
    /// </summary>
    public class ExportSettings
    {
        /// <summary>
        /// The root directory of the Unity project to export from.
        /// </summary>
        public DirectoryInfo Source { get; set; }

        /// <summary>
        /// The destination file for the .unitypackage.
        /// </summary>
        public FileInfo Output { get; set; }

        /// <summary>
        /// Glob patterns for assets to include.
        /// </summary>
        public IEnumerable<string> Assets { get; set; }

        /// <summary>
        /// Glob patterns for assets to exclude.
        /// </summary>
        public IEnumerable<string> Excludes { get; set; }

        /// <summary>
        /// Whether to skip the dependency analysis phase.
        /// </summary>
        public bool SkipDependencyCheck { get; set; }

        /// <summary>
        /// The root folder name for assets (default "Assets").
        /// </summary>
        public string AssetRoot { get; set; }

        /// <summary>
        /// Optional subfolder to prepend to asset paths in the package.
        /// </summary>
        public string SubFolder { get; set; }

        /// <summary>
        /// Logging verbosity level.
        /// </summary>
        public Microsoft.Extensions.Logging.LogLevel Verbose { get; set; }
    }
}

