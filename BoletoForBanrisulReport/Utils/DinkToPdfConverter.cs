using DinkToPdf;
using DinkToPdf.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace BoletoForBanrisul.Report.Utils
{
    public class DinkToPdfConverter : IPdfConverter
    {
        private static readonly object _lock = new object();
        private static DinkToPdfConverter? _instance;
        private readonly IConverter _converter;

        public IConverter Converter => _converter;

        private DinkToPdfConverter(IConverter? converter = null)
        {
            _converter = converter ?? new SynchronizedConverter(new PdfTools());
        }

        public static DinkToPdfConverter GetInstance(IConverter? converter = null)
        {
            lock (_lock)
            {
                if (_instance == null)
                    _instance = new DinkToPdfConverter(converter);
            }

            return _instance;
        }

        public async Task<byte[]> ConvertToPdfAsync(string html)
        {
            return await Task.FromResult(_converter.Convert(new HtmlToPdfDocument
            {
                GlobalSettings = {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                },
                Objects =
                {
                    new ObjectSettings {
                        HtmlContent = html,
                        WebSettings = { DefaultEncoding = "utf-8" }
                    },
                }
            }));
        }
    }
}
