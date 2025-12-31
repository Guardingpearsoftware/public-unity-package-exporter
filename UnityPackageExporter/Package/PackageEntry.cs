namespace GUPS.UnityPackageExporter.Package
{
    /// <summary>
    /// Represents a single entry inside a Unity Package.
    /// </summary>
    public class PackageEntry
    {
        /// <summary>
        /// The relative path of the file within the Unity project (e.g., "Assets/Scripts/MyScript.cs").
        /// </summary>
        public string RelativeFilePath { get; set; }

        /// <summary>
        /// The content of the .meta file.
        /// </summary>
        public byte[] Metadata { get; set; }

        /// <summary>
        /// The content of the asset file itself.
        /// </summary>
        public byte[] Content { get; set; }
    }
}
