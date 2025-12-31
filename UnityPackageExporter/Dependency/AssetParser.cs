// System
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GUPS.UnityPackageExporter.Dependency
{
    /// <summary>
    /// Parsers Unity asset files (.mat, .prefab, etc.) and meta files to extract ID and reference information.
    /// Uses regex matching to find YAML-like references.
    /// </summary>
    static class AssetParser
    {
        /// <summary>
        /// Regex to find fileID and guid pairs in asset files.
        /// Matches pattern: fileID: <id>, guid: <guid>
        /// </summary>
        private static Regex ReferenceRegex = new Regex(@"fileID: ([\-0-9]+), guid: ([a-z0-9]+)", RegexOptions.Compiled);

        /// <summary>
        /// Regex to find the GUID definition in a meta file.
        /// Matches pattern: guid: <guid>
        /// </summary>
        private static Regex GuidRegex = new Regex(@"guid: ([a-z0-9]+)", RegexOptions.Compiled);

        /// <summary>
        /// Reads all asset references from a given file.
        /// Reads the file line by line to reduce memory usage on large assets.
        /// </summary>
        /// <param name="file">Path to the asset file.</param>
        /// <returns>Array of AssetIDs referenced by the file.</returns>
        public static async Task<AssetID[]> ReadReferencesAsync(string file)
        {
            if (!File.Exists(file))
                return new AssetID[0];

            // Use a list to store results since we don't know the count upfront
            var results = new List<AssetID>();

            using (var reader = new StreamReader(file))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var matches = ReferenceRegex.Matches(line);
                    for(int i = 0; i < matches.Count; i++)
                    {
                        var match = matches[i];
                        results.Add(new AssetID
                        {
                            fileID = long.Parse(match.Groups[1].Value),
                            guid = match.Groups[2].Value
                        });
                    }
                }
            }

            return results.ToArray();
        }

        /// <summary>
        /// Reads the AssetID (specifically the GUID) from a .meta file.
        /// </summary>
        /// <param name="metaFile">Path to the .meta file.</param>
        /// <returns>The AssetID containing the file's GUID.</returns>
        public static async Task<AssetID> ReadAssetIDAsync(string metaFile)
        {
            if (!File.Exists(metaFile))
                return new AssetID();

            // Meta files are small, but consistently using streaming is good practice.
            // However, GUID is usually near the top, so we can stop once found.
            using (var reader = new StreamReader(metaFile))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var match = GuidRegex.Match(line);
                    if (match.Success)
                    {
                        return new AssetID
                        {
                            guid = match.Groups[1].Value
                        };
                    }
                }
            }

            return new AssetID();
        }
    }
}
