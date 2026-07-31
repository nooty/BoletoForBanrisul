using BoletoForBanrisul.Report.Extensions;
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
    }
}
