using System.Threading.Tasks;
using Gerakul.FastSql.Tests.Shared;
using Testcontainers.PostgreSql;

namespace Gerakul.FastSql.IntegrationTests.Infrastructure
{
    public class PostgreSqlContainerFixture : ContainerFixture
    {
        public const string ImageName = "postgres:18.6-alpine";

        private PostgreSqlContainer container;

        public PostgreSqlContainerFixture()
            : base(new PostgreSqlSpec())
        {
        }

        public override string Image => ImageName;

        protected override async Task<string> StartContainerAsync()
        {
            container = new PostgreSqlBuilder(ImageName).Build();
            await container.StartAsync().ConfigureAwait(false);
            return container.GetConnectionString();
        }

        protected override ValueTask DisposeContainerAsync()
        {
            return container == null ? default(ValueTask) : container.DisposeAsync();
        }
    }
}
