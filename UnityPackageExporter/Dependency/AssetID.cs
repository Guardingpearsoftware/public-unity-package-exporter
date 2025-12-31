namespace GUPS.UnityPackageExporter.Dependency
{
    /// <summary>
    /// Represents a unique identifier for a Unity asset.
    /// Combines the file ID and GUID.
    /// </summary>
    struct AssetID
    {
        /// <summary>
        /// The local identifier for the object within the file.
        /// </summary>
        public long fileID;

        /// <summary>
        /// The global unique identifier for the asset file.
        /// </summary>
        public string guid;

        /// <summary>
        /// Gets a value indicating whether this AssetID has a valid GUID.
        /// </summary>
        public bool HasGUID => !string.IsNullOrWhiteSpace(guid);
    }
}
