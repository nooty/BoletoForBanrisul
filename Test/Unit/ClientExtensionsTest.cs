using BoletoForBanrisul.Client;
using BoletoForBanrisul.Client.Extensions;
using BoletoForBanrisul.Report.Extensions;
using BoletoForBanrisul.Report;
using Microsoft.Extensions.DependencyInjection;

namespace Test
{
    [Trait("Category", "Unit")]
    public class ClientExtensionsTest
    {
        [Fact]
        public void Should_Register_BoletoClientBuilder_As_Transient()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddBoletoForBanrisulClientBuilder();
            var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(BoletoClientBuilder));

            // Assert
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        }

        [Fact]
        public void Should_Invoke_Configure_Action_When_Provided()
        {
            // Arrange.
            var services = new ServiceCollection();
            var invoked = false;

            // Act.
            services.AddBoletoForBanrisulClientBuilder((builder, provider) =>
            {
                invoked = true;
            });
            var provider = services.BuildServiceProvider();

            // Assert.
            var builder = provider.GetService<BoletoClientBuilder>();
            Assert.True(invoked);
            Assert.NotNull(builder);
        }

        [Fact]
        public void Should_Provide_Different_BoletoClientBuilder_Per_Scope()
        {
            // Arrange.
            var services = new ServiceCollection();
            services.AddBoletoForBanrisulClientBuilder(null);

            var provider = services.BuildServiceProvider();

            // Act & Assert.
            using (var scope1 = provider.CreateScope())
            {
                var builder1 = scope1.ServiceProvider.GetService<BoletoClientBuilder>();
                using (var scope2 = provider.CreateScope())
                {
                    var builder2 = scope2.ServiceProvider.GetService<BoletoClientBuilder>();

                    Assert.NotNull(builder1);
                    Assert.NotNull(builder2);
                    Assert.NotSame(builder1, builder2);
                }
            }
        }

        [Fact]
        public void Should_Fallback_To_Default_When_No_Configure_Action_Provided()
        {
            // Arrange.
            var services = new ServiceCollection();

            // Act.
            services.AddBoletoForBanrisulClientBuilder(null);
            var provider = services.BuildServiceProvider();

            // Assert.
            var builder = provider.GetService<BoletoClientBuilder>();
            Assert.NotNull(builder);
        }
    }
}
