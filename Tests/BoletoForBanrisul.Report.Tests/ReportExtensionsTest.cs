using BoletoForBanrisul.Report.Exceptions;
using BoletoForBanrisul.Report.Extensions;
using BoletoForBanrisul.Report.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace BoletoForBanrisul.Report.Tests
{
    [Trait("Category", "Unit")]
    public class ReportExtensionsTest
    {
        [Fact]
        public void Should_Register_BoletoReportBuilder_As_Transient()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddBoletoForBanrisulReportBuilder();
            var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(BoletoReportBuilder));

            // Assert
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        }

        [Fact]
        public void Should_Allow_Custom_Configuration()
        {
            // Arrange.
            var services = new ServiceCollection();
            var customSettingsCalled = false;

            // Act.
            services.AddBoletoForBanrisulReportBuilder((builder, provider) =>
            {
                // Custom configuration action.
                customSettingsCalled = true;
            });

            var serviceProvider = services.BuildServiceProvider();
            var builder = serviceProvider.GetService<BoletoReportBuilder>();

            // Assert.
            Assert.NotNull(builder);
            Assert.True(customSettingsCalled);
        }

        [Fact]
        public void Should_Register_The_Expensive_Dependencies_As_Singletons()
        {
            // Both carry process-wide state that is expensive to build: a Chromium process and the
            // concurrency semaphore on one side, the Razor engine and its template cache on the other.
            var services = new ServiceCollection();

            services.AddBoletoForBanrisulReportBuilder();

            Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(IPdfConverter)).Lifetime);
            Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == typeof(IBoletoTemplateRenderer)).Lifetime);
        }

        [Fact]
        public void Should_Not_Register_IBoletoReport()
        {
            // Registering it would mean calling Build() at registration time, freezing the
            // PrintSettings - the beneficiary above all - of whoever resolves it first.
            var services = new ServiceCollection();

            services.AddBoletoForBanrisulReportBuilder();

            Assert.DoesNotContain(services, x => x.ServiceType == typeof(IBoletoReport));
        }

        [Fact]
        public async Task Should_Let_Each_Resolution_Configure_Its_Own_PrintSettings()
        {
            // Two requests, two beneficiaries, one shared converter.
            var services = new ServiceCollection();
            var converter = new RecordingPdfConverter();

            services.AddSingleton<IPdfConverter>(converter);
            services.AddBoletoForBanrisulReportBuilder();

            using var provider = services.BuildServiceProvider();

            foreach (var beneficiary in new[] { "BENEFICIARIO UM", "BENEFICIARIO DOIS" })
            {
                using var report = provider.GetRequiredService<BoletoReportBuilder>()
                    .Configure(options => options.Beneficiary = new BeneficiarySettings { Name = beneficiary })
                    .Build();

                await report.PrintAsync(CreateBoleto());

                Assert.Contains(beneficiary, converter.LastHtml);
            }
        }

        [Fact]
        public void Should_Inject_The_Container_Services_Into_The_Builder()
        {
            var services = new ServiceCollection();
            var injectedConverter = new RecordingPdfConverter();

            // Registered first, so TryAddSingleton must leave them alone instead of replacing them
            // with the defaults.
            services.AddSingleton<IPdfConverter>(injectedConverter);
            services.AddBoletoForBanrisulReportBuilder();

            using var provider = services.BuildServiceProvider();

            Assert.Same(injectedConverter, provider.GetRequiredService<IPdfConverter>());

            using var report = (BoletoReport)provider.GetRequiredService<BoletoReportBuilder>().Build();

            Assert.Same(provider.GetRequiredService<IBoletoTemplateRenderer>(), report.TemplateRenderer);
        }

        [Fact]
        public async Task Should_Not_Override_Services_Chosen_In_ConfigureBuilder()
        {
            var services = new ServiceCollection();
            var chosen = new RecordingPdfConverter();

            services.AddBoletoForBanrisulReportBuilder(
                (builder, provider) => builder.SetPdfConverter(chosen),
                chromium => chromium.MinimumFreeDiskMegabytes = 0);

            using var provider = services.BuildServiceProvider();

            using var report = provider.GetRequiredService<BoletoReportBuilder>().Build();

            await report.PrintAsync(CreateBoleto());

            // The converter the caller picked is the one that ran, not the registered singleton.
            Assert.NotEmpty(chosen.AllHtml);
        }

        [Fact]
        public void Should_Not_Dispose_A_Converter_Chosen_In_ConfigureBuilder()
        {
            var services = new ServiceCollection();
            var chosen = new RecordingPdfConverter();

            services.AddBoletoForBanrisulReportBuilder((builder, provider) => builder.SetPdfConverter(chosen));

            var provider = services.BuildServiceProvider();
            var report = provider.GetRequiredService<BoletoReportBuilder>().Build();

            report.Dispose();
            provider.Dispose();

            // The report never owned it: it was handed in, so disposing it must leave the converter
            // usable.
            Assert.False(chosen.WasDisposed);
        }

        [Fact]
        public async Task Should_Apply_ConfigureChromium_To_The_Registered_Converter()
        {
            var services = new ServiceCollection();

            // An unreachable disk threshold keeps the converter observable without ever launching a
            // browser: it fails fast on the pre-check, and reaching that failure at all proves the
            // configuration was applied.
            services.AddBoletoForBanrisulReportBuilder(
                configureChromium: chromium => chromium.MinimumFreeDiskMegabytes = int.MaxValue);

            using var provider = services.BuildServiceProvider();

            await Assert.ThrowsAsync<InsufficientDiskSpaceException>(
                () => provider.GetRequiredService<IPdfConverter>().ConvertToPdfAsync("<html></html>"));
        }

        [Fact]
        public async Task Should_Dispose_The_Registered_Converter_With_The_Provider()
        {
            var services = new ServiceCollection();

            services.AddBoletoForBanrisulReportBuilder();

            var provider = services.BuildServiceProvider();
            var converter = provider.GetRequiredService<IPdfConverter>();

            provider.Dispose();

            // Disposal of a converter the container itself created is the container's job - which is
            // why nothing here calls Dispose on it.
            await Assert.ThrowsAsync<ObjectDisposedException>(() => converter.ConvertToPdfAsync("<html></html>"));
        }

        private static Core.Models.Boleto CreateBoleto() => new Core.Models.Boleto
        {
            Titulo = new Core.Models.Titulo
            {
                NossoNumero = "123456",
                SeuNumero = "7890",
                DataVencimento = new DateTime(2026, 10, 5),
                DataEmissao = new DateTime(2026, 9, 30),
                Pagador = new Core.Models.Pagador { CpfCnpj = "12345678900", Nome = "PAGADOR TESTE" }
            }
        };
    }
}
