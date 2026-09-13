using BoletoForBanrisul.Client.HttpClients;

namespace BoletoForBanrisul.Client.Tests.HttpClients
{
    [Trait("Category", "Unit")]
    public class DefaultTransportHandlerTests
    {
        [Fact]
        public void Instance_IsTheSameObjectEveryTime()
        {
            Assert.Same(DefaultTransportHandler.Instance, DefaultTransportHandler.Instance);
        }

        [Fact]
        public void Instance_RecyclesPooledConnections()
        {
            // A finite lifetime is what lets a never-disposed transport pick up DNS changes.
            var handler = Assert.IsType<StandardSocketsHttpHandler>(DefaultTransportHandler.Instance);

            Assert.NotEqual(Timeout.InfiniteTimeSpan, handler.PooledConnectionLifetime);
            Assert.NotEqual(Timeout.InfiniteTimeSpan, handler.PooledConnectionIdleTimeout);
        }
    }
}
