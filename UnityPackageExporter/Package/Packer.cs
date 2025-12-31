// System
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Microsoft
using Microsoft.Extensions.Logging;

// ICSharpCode
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace GUPS.UnityPackageExporter.Package
{
    /// <summary>
    /// Packs files into a Unity Package (.unitypackage), which is essentially a tar.gz archive.
    /// Handles the specific directory structure required by Unity packages (GUID-based folders).
    /// </summary>
    class Packer : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Logger instance.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Path to the Unity Project root.
        /// </summary>
        public string ProjectPath { get; }
        
        /// <summary>
        /// Output file path. If a stream is given, this is null.
        /// </summary>
        public string OutputPath { get; }

        /// <summary>
        /// Sub Folder to write assets too.
        /// </summary>
        public string SubFolder { get; set; } = "";

        /// <summary>
        /// The underlying output stream.
        /// </summary>
        private Stream _outStream;

        /// <summary>
        /// The GZip stream wrapping the output stream.
        /// </summary>
        private GZipOutputStream _gzStream;

        /// <summary>
        /// The Tar stream wrapping the GZip stream.
        /// </summary>
        private TarOutputStream _tarStream;

        /// <summary>
        /// Semaphore to ensure thread-safe writing to the Tar stream.
        /// </summary>
        private readonly SemaphoreSlim _streamLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Set of files already added to the package to prevent duplicates.
        /// Uses ConcurrentDictionary for thread safety during parallel processing.
        /// </summary>
        private ConcurrentDictionary<string, byte> _files;

        /// <summary>
        /// Read-only view of files added to the package.
        /// </summary>
        public IReadOnlyCollection<string> Files => (IReadOnlyCollection<string>)_files.Keys;

        /// <summary>
        /// Creates a new Packer that writes to the output file.
        /// </summary>
        /// <param name="projectPath">Path to the Unity Project.</param>
        /// <param name="output">The .unitypackage file path.</param>
        /// <param name="logger">Logger.</param>
        public Packer(string projectPath, string output, ILogger logger) 
            :this(projectPath, new FileStream(output, FileMode.OpenOrCreate), logger) 
        {
            OutputPath = output;
        }

        /// <summary>
        /// Creates a new Packer that writes to the outputStream.
        /// </summary>
        /// <param name="projectPath">Path to the Unity Project.</param>
        /// <param name="stream">The stream the contents will be written to.</param>
        /// <param name="logger">Logger.</param>
        public Packer(string projectPath, Stream stream, ILogger logger)
        {
            _logger = logger;
            ProjectPath = projectPath;
            OutputPath = null;

            _files = new ConcurrentDictionary<string, byte>();
            _outStream = stream;
            _gzStream = new GZipOutputStream(_outStream);
            _gzStream.FileName = "archtemp.tar";
            _tarStream = new TarOutputStream(_gzStream, Encoding.UTF8);
        }

        /// <summary>
        /// Adds an asset to the pack.
        /// Checks for existence, generates/reads meta files, and writes to the tar stream.
        /// This method is thread-safe.
        /// </summary>
        /// <param name="filePath">The full path to the asset.</param>
        /// <returns>True if the asset was written to the pack, false otherwise.</returns>
        public async Task<bool> AddAssetAsync(string filePath)
        {
            FileInfo file = new FileInfo(Path.GetExtension(filePath) == ".meta" ? filePath.Substring(0, filePath.Length - 5) : filePath);
            if (!file.Exists)
            {
                if (Directory.Exists(file.FullName))
                {
                    _logger.LogWarning($"Attempted to add a folder {file.FullName} as a file. This is not supported.");
                    return false;
                }

                throw new FileNotFoundException($"Could not find the file {file.FullName}");
            }
             
            if (!_files.TryAdd(file.FullName, 0)) 
                return false;

            // Prepare data outside the lock (IO heavy)
            string relativePath = Path.GetRelativePath(ProjectPath, file.FullName);
            string metaFile = $"{file.FullName}.meta";
            string guidString = "";
            string metaContents;

            if (!File.Exists(metaFile))
            {
                //Meta file is missing so we have to generate it ourselves.
                _logger.LogWarning("Missing .meta for {0}", relativePath);

                Guid guid = Guid.NewGuid();
                foreach (var byt in guid.ToByteArray())
                    guidString += string.Format("{0:X2}", byt);

                var builder = new System.Text.StringBuilder();
                builder.Append("guid: " + new Guid()).Append("\n");
                metaContents = builder.ToString();
            }
            else
            {
                //Read the meta contents
                metaContents = await File.ReadAllTextAsync(metaFile);

                int guidIndex = metaContents.IndexOf("guid: ");
                guidString = metaContents.Substring(guidIndex + 6, 32);
            }

            // Write to stream inside the lock (Sequential requirement)
            await _streamLock.WaitAsync();
            try
            {
                _logger.LogInformation("Writing File {0} ( {1} )", relativePath, guidString);
                await _tarStream.WriteFileAsync(file.FullName, $"{guidString}/asset");
                await _tarStream.WriteAllTextAsync($"{guidString}/asset.meta", metaContents);

                string pathname = Path.Combine(SubFolder, relativePath).Replace('\\', '/');
                if (!pathname.StartsWith("Assets/")) pathname = $"Assets/{pathname}";
                await _tarStream.WriteAllTextAsync($"{guidString}/pathname", pathname);
            }
            catch(FileNotFoundException fnf)
            {
                _logger.LogError($"Failed to write: {fnf.Message}");
                return false;
            }
            finally
            {
                _streamLock.Release();
            }

            return true;
        }

        /// <summary>
        /// Adds a collection of assets to the pack in parallel.
        /// </summary>
        /// <param name="assets">List of asset paths.</param>
        public async Task AddAssetsAsync(IEnumerable<string> assets)
        {
            await Parallel.ForEachAsync(assets, async (asset, ct) => 
            {
                await AddAssetAsync(asset);
            });
        }

        /// <summary>
        /// Flushes the underlying tar stream.
        /// </summary>
        public Task FlushAsync()
            => _tarStream.FlushAsync();

        /// <summary>
        /// Disposes the streams.
        /// </summary>
        public void Dispose()
        {
            _streamLock.Dispose();
            _tarStream.Dispose();
            _gzStream.Dispose();
            _outStream.Dispose();
        }

        /// <summary>
        /// Asynchronously disposes the streams.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            _streamLock.Dispose();
            await _tarStream.DisposeAsync();
            await _gzStream.DisposeAsync();
            await _outStream.DisposeAsync();
        }

        /// <summary>
        /// Unpacks all the assets from a .unitypackage file.
        /// </summary>
        /// <param name="package">Path to the package file.</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>Collection of unpacked entries.</returns>
        public static Task<IEnumerable<PackageEntry>> Unpack(string package, ILogger logger = null)
        {
            using var fileStream = new FileStream(package, FileMode.Open);
            return Unpack(fileStream, logger);
        }

        /// <summary>
        /// Unpacks all assets from a stream.
        /// </summary>
        /// <param name="package">Stream containing the package data.</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>Collection of unpacked entries.</returns>
        public async static Task<IEnumerable<PackageEntry>> Unpack(Stream package, ILogger logger = null)
        {
            using var gzoStream = new GZipInputStream(package);
            using var tarStream = new TarInputStream(gzoStream, Encoding.UTF8);

            Dictionary<string, PackageEntry> entries = new Dictionary<string, PackageEntry>();

            TarEntry tarEntry;
            while ((tarEntry = tarStream.GetNextEntry()) != null)
            {
                if (tarEntry.IsDirectory)
                    continue;

                string[] parts = tarEntry.Name.Split('/');
                string file = parts[1];
                string guid = parts[0];
                byte[] data = null;

                //Create a new memory stream and read the entries into it.
                using (MemoryStream mem = new MemoryStream())
                {
                    await tarStream.ReadNextFileAsync(mem);
                    data = mem.ToArray();
                }

                //Make sure we actually read data
                if (data == null)
                    continue;

                //Add a new element
                if (!entries.ContainsKey(guid))
                    entries.Add(guid, new PackageEntry());

                switch (file)
                {
                    case "asset":
                        entries[guid].Content = data;
                        break;

                    case "asset.meta":
                        entries[guid].Metadata = data;
                        break;

                    case "pathname":
                        string path = Encoding.ASCII.GetString(data);
                        entries[guid].RelativeFilePath = path;
                        break;

                    default:
                        logger?.LogWarning("Skipping {0} because its a unkown file", tarEntry.Name);
                        break;
                }
            }

            return entries.Values;
        }
    }
}
