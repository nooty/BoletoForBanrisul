using System;
using System.IO;

namespace BoletoForBanrisul.Report.Exceptions
{
    /// <summary>
    /// Thrown when there is not enough free disk space to start a conversion.
    /// </summary>
    /// <remarks>
    /// It signals the fast failure in place of a hung Chromium, which is what happens when it starts
    /// without disk space. Retrying gets nowhere until the disk is freed: leave this exception out of
    /// the retry policy.
    /// </remarks>
    public class InsufficientDiskSpaceException : IOException
    {
        /// <summary>
        /// Creates the exception with the path checked and the measured values.
        /// </summary>
        /// <param name="path">Path whose volume was checked.</param>
        /// <param name="freeBytes">Free space found, in bytes.</param>
        /// <param name="requiredBytes">Minimum free space required, in bytes.</param>
        public InsufficientDiskSpaceException(string path, long freeBytes, long requiredBytes)
            : base($"Espaço em disco insuficiente para iniciar a conversão. Path={path}, Livre={freeBytes} bytes, Mínimo={requiredBytes} bytes.")
        {
            Path = path;
            FreeBytes = freeBytes;
            RequiredBytes = requiredBytes;
        }

        /// <summary>
        /// Path whose volume was checked.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Free space found, in bytes.
        /// </summary>
        public long FreeBytes { get; }

        /// <summary>
        /// Minimum free space required, in bytes.
        /// </summary>
        public long RequiredBytes { get; }
    }
}
