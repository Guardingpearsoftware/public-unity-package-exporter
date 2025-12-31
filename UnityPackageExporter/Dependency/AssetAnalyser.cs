// System
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Microsoft
using Microsoft.Extensions.Logging;

namespace GUPS.UnityPackageExporter.Dependency
{
    /// <summary>
    /// Analyses Assets for their dependencies by parsing .meta files and resolving GUIDs.
    /// </summary>
    class AssetAnalyser
    {
        /// <summary>
        /// Logger for recording analysis progress and errors.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Maps AssetID to the physical file info.
        /// </summary>
        private Dictionary<AssetID, FileInfo> fileIndex = new Dictionary<AssetID, FileInfo>();

        /// <summary>
        /// Maps GUID string to AssetID.
        /// </summary>
        private Dictionary<string, AssetID> guidIndex = new Dictionary<string, AssetID>();

        /// <summary>
        /// The root path of the project being analysed.
        /// </summary>
        public string ProjectPath { get; }

        /// <summary>
        /// Initializes a new instance of the AssetAnalyser class.
        /// </summary>
        /// <param name="projectPath">Root directory of the project.</param>
        /// <param name="logger">Logger instance.</param>
        public AssetAnalyser(string projectPath, ILogger logger)
        {
            ProjectPath = projectPath;
            _logger = logger;
        }

        /// <summary>
        /// Adds a single file to the list of valid assets to check.
        /// Reads and indexes the asset's ID and GUID.
        /// </summary>
        /// <param name="file">Path to the file to add.</param>
        public async Task AddFileAsync(string file)
        {
            _logger.LogTrace("Adding file {0}", file);

            string filePath = Path.GetExtension(file) == ".meta" ? file : $"{file}.meta";
            var assetID = await AssetParser.ReadAssetIDAsync(filePath);
            
            lock(fileIndex) {
                fileIndex[assetID] = new FileInfo(filePath.Substring(0, filePath.Length - 5));
                if (assetID.HasGUID) guidIndex[assetID.guid] = assetID;
            }
        }

        /// <summary>
        /// Adds a collection of files to the valid assets list.
        /// </summary>
        /// <param name="files">List of file paths.</param>
        public async Task AddFilesAsync(IEnumerable<string> files)
        {
            await Parallel.ForEachAsync(files, async (file, ct) => 
            {
                await AddFileAsync(file);
            });
        }

        /// <summary>
        /// Gets a list of all dependencies (deep search) for the given list of files.
        /// </summary>
        /// <param name="files">Initial files to find dependencies for.</param>
        /// <returns>Collection of all dependent file paths.</returns>
        public async Task<IReadOnlyCollection<string>> FindAllDependenciesAsync(IEnumerable<string> files)
        {
            _logger.LogInformation("Finding Dependencies");

            ConcurrentDictionary<string, byte> results = new ConcurrentDictionary<string, byte>();
            ConcurrentQueue<string> queue = new ConcurrentQueue<string>();

            foreach (var item in files)
            {
                if (results.TryAdd(item, 0))
                    queue.Enqueue(item);
            }

            // Process queue in parallel chunks to maximize IO throughput
            while (!queue.IsEmpty)
            {
                // Dequeue a batch of items
                var batch = new List<string>();
                while (batch.Count < 32 && queue.TryDequeue(out var item))
                {
                    batch.Add(item);
                }

                if (batch.Count == 0) break;

                await Parallel.ForEachAsync(batch, async (currentFile, ct) =>
                {
                    _logger.LogTrace("Searching {0}", currentFile);
                    var dependencies = await FindFileDependenciesAsync(currentFile);
                    foreach (var dependency in dependencies)
                    {
                        if (results.TryAdd(dependency, 0))
                        {
                            _logger.LogTrace(" - Found {0}", dependency);
                            queue.Enqueue(dependency);
                        }
                    }
                });
            }

            return results.Keys.ToArray();
        }

        /// <summary>
        /// Gets a list of direct dependencies for a specific asset (shallow search).
        /// </summary>
        /// <param name="assetPath">Path to the asset file.</param>
        /// <returns>Collection of direct dependency file paths.</returns>
        public async Task<IReadOnlyCollection<string>> FindFileDependenciesAsync(string assetPath)
        {
            AssetID[] references = await AssetParser.ReadReferencesAsync(assetPath);
            HashSet<string> files = new HashSet<string>(references.Length);
            foreach(AssetID reference in references)
            {
                if (TryGetFileFromGUID(reference.guid, out var info))
                    files.Add(info.FullName);
            }

            return files;
        }

        /// <summary>
        /// Attempts to resolve a file path from a Unity GUID.
        /// </summary>
        /// <param name="guid">The GUID to look up.</param>
        /// <param name="info">The resolved FileInfo if found.</param>
        /// <returns>True if the file was found, false otherwise.</returns>
        private bool TryGetFileFromGUID(string guid, out FileInfo info) {
            // Dictionary lookups are thread safe for reading as long as no writes are happening simultaneously.
            // Since we populate indices before dependency search, this is safe.
            if (guid != null && guidIndex.TryGetValue(guid, out var assetID))
            {
                if (fileIndex.TryGetValue(assetID, out var fi))
                {
                    info = fi;
                    return true;
                }
            }
            
            info = null;
            return false;
        }
    }
}
