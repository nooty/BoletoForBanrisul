using System;
using System.IO;

namespace BoletoForBanrisul.Report.Exceptions
{
    /// <summary>
    /// Lançada quando não há espaço livre suficiente em disco para iniciar uma conversão.
    /// </summary>
    /// <remarks>
    /// Sinaliza a falha rápida no lugar de um Chromium pendurado, que é o que acontece quando ele
    /// sobe sem espaço em disco. Retentar não adianta enquanto o disco não for liberado: deixe esta
    /// exceção de fora da política de retry.
    /// </remarks>
    public class InsufficientDiskSpaceException : IOException
    {
        /// <summary>
        /// Cria a exceção com o caminho verificado e os valores medidos.
        /// </summary>
        /// <param name="path">Caminho cujo volume foi verificado.</param>
        /// <param name="freeBytes">Espaço livre encontrado, em bytes.</param>
        /// <param name="requiredBytes">Espaço livre mínimo exigido, em bytes.</param>
        public InsufficientDiskSpaceException(string path, long freeBytes, long requiredBytes)
            : base($"Espaço em disco insuficiente para iniciar a conversão. Path={path}, Livre={freeBytes} bytes, Mínimo={requiredBytes} bytes.")
        {
            Path = path;
            FreeBytes = freeBytes;
            RequiredBytes = requiredBytes;
        }

        /// <summary>
        /// Caminho cujo volume foi verificado.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Espaço livre encontrado, em bytes.
        /// </summary>
        public long FreeBytes { get; }

        /// <summary>
        /// Espaço livre mínimo exigido, em bytes.
        /// </summary>
        public long RequiredBytes { get; }
    }
}
