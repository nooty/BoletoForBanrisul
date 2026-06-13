using ChromiumHtmlToPdfLib;
using ChromiumHtmlToPdfLib.Settings;
using System;
using System.Collections.Generic;
using System.Text;

namespace BoletoForBanrisul.Report.Utils
{
    public class ChromiumHtmlToPdfConverter : IPdfConverter
    {
        private static string _chromiumProfilePath = Path.Combine(Directory.GetCurrentDirectory(), "ChromiumProfileData");
        private static readonly SemaphoreSlim _conversionSemaphore = new SemaphoreSlim(1, 1);
        private readonly List<string> _chromiumArguments;
        private readonly int _conversionTimeout;

        public ChromiumHtmlToPdfConverter() : this(new List<string>
            {
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--disable-software-rasterizer",
                "--no-zygote",
                "--disable-crash-reporter"
            })
        {
        }

        public ChromiumHtmlToPdfConverter(IEnumerable<string> chromiumArguments, int conversionTimeout = 300000)
        {
            _chromiumArguments = chromiumArguments.ToList();
            _conversionTimeout = conversionTimeout;

            if (!Directory.Exists(_chromiumProfilePath))
                Directory.CreateDirectory(_chromiumProfilePath);
        }

        public async Task<byte[]> ConvertToPdfAsync(string html)
        {
            await _conversionSemaphore.WaitAsync();

            try
            {
                return await Task.Run(() =>
                {
                    var pageSettings = new PageSettings();
                    using var converter = new Converter(userProfile: _chromiumProfilePath);

                    using var byteStream = new MemoryStream();

                    foreach (var argument in _chromiumArguments)
                    {
                        converter.AddChromiumArgument(argument);
                    }

                    converter.ConvertToPdf(html, byteStream, pageSettings, conversionTimeout: _conversionTimeout);

                    return byteStream.ToArray();
                });
            }
            finally
            {
                _conversionSemaphore.Release();
            }
        }
    }
}
