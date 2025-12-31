// System
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

// ICSharpCode
using ICSharpCode.SharpZipLib.Tar;

namespace GUPS.UnityPackageExporter.Package
{
    /// <summary>
    /// Extension methods for TarOutputStream and TarInputStream to handle async operations and Unity-specific quirks.
    /// </summary>
    public static class TarStreamExtensions
    {
        /// <summary>
        /// Reads a file from source and writes it to the tar stream asynchronously.
        /// </summary>
        /// <param name="stream">The target TarOutputStream.</param>
        /// <param name="source">Path to the source file on disk.</param>
        /// <param name="dest">Destination path inside the archive.</param>
        public static async Task WriteFileAsync(this TarOutputStream stream, string source, string dest)
        {
            using (Stream inputStream = File.OpenRead(source))
            {
                long fileSize = inputStream.Length;

                // Create a tar entry named as appropriate. You can set the name to anything,
                // but avoid names starting with drive or UNC.
                TarEntry entry = TarEntry.CreateTarEntry(dest);

                // Must set size, otherwise TarOutputStream will fail when output exceeds.
                entry.Size = fileSize;

                // Add the entry to the tar stream, before writing the data.
                stream.PutNextEntry(entry);

                // Use CopyToAsync with a decent buffer size (80KB is standard for CopyTo)
                await inputStream.CopyToAsync(stream);

                //Close the entry
                stream.CloseEntry();
            }
        }

        /// <summary>
        /// Writes a string content to the tar stream as a file.
        /// </summary>
        /// <param name="stream">The target TarOutputStream.</param>
        /// <param name="dest">Destination path inside the archive.</param>
        /// <param name="content">The text content to write.</param>
        public static async Task WriteAllTextAsync(this TarOutputStream stream, string dest, string content)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);

            TarEntry entry = TarEntry.CreateTarEntry(dest);
            entry.Size = bytes.Length;

            // Add the entry to the tar stream, before writing the data.
            stream.PutNextEntry(entry);

            // this is copied from TarArchive.WriteEntryCore
            await stream.WriteAsync(bytes, 0, bytes.Length);

            //Close the entry
            stream.CloseEntry();
        }

        /// <summary>
        /// Reads the next file's content from the tar stream into an output stream.
        /// Handles ASCII conversion for text files where necessary (LF/CRLF normalization).
        /// </summary>
        /// <param name="tarIn">The source TarInputStream.</param>
        /// <param name="outStream">The destination stream.</param>
        /// <returns>Total bytes read.</returns>
        public async static Task<long> ReadNextFileAsync(this TarInputStream tarIn, Stream outStream)
        {
            long totalRead = 0;
            byte[] buffer = new byte[4096];
            bool isAscii = true;
            bool cr = false;

            int numRead = await tarIn.ReadAsync(buffer, 0, buffer.Length);
            int maxCheck = Math.Min(200, numRead);

            totalRead += numRead;

            for (int i = 0; i < maxCheck; i++)
            {
                byte b = buffer[i];
                if (b < 8 || (b > 13 && b < 32) || b == 255)
                {
                    isAscii = false;
                    break;
                }
            }

            while (numRead > 0)
            {
                if (isAscii)
                {
                    // Convert LF without CR to CRLF. Handle CRLF split over buffers.
                    for (int i = 0; i < numRead; i++)
                    {
                        byte b = buffer[i];     // assuming plain Ascii and not UTF-16
                        if (b == 10 && !cr)     // LF without CR
                            outStream.WriteByte(13);
                        cr = (b == 13);

                        outStream.WriteByte(b);
                    }
                }
                else
                    outStream.Write(buffer, 0, numRead);

                numRead = await tarIn.ReadAsync(buffer, 0, buffer.Length);
                totalRead += numRead;
            }

            return totalRead;
        }
    }
}
