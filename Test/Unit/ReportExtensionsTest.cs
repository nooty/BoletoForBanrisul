using BoletoForBanrisul.Report.Utils;
using BoletoForBanrisul.Report;
using DinkToPdf.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BoletoForBanrisul.Report.Extensions;
using DinkToPdf;

namespace Test
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
        public void Should_Use_Converter_If_Registered()
        {
            // Arrange.
            var services = new ServiceCollection();
            var converter = new SynchronizedConverter(new PdfTools());

            // Register IConverter as Singleton.
            services.AddSingleton(converter);

            // Act.
            services.AddBoletoForBanrisulReportBuilder();

            var serviceProvider = services.BuildServiceProvider();
            var builder = serviceProvider.GetService<BoletoReportBuilder>();

            // Assert.
            Assert.NotNull(builder);
            Assert.Equal(converter, DinkToPdfConverter.GetInstance(converter).Converter);
        }

        [Fact]
        public void Should_Allow_Custom_Configuration()
        {
            // Arrange.
            var services = new ServiceCollection();
            var customSettingsCalled = false;

            // Act.
            services.AddBoletoForBanrisulReportBuilder(builder =>
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
    }
}
