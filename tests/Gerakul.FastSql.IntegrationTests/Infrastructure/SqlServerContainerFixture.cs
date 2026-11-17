using System.Threading.Tasks;
using Gerakul.FastSql.Tests.Shared;
using Testcontainers.MsSql;

namespace Gerakul.FastSql.IntegrationTests.Infrastructure
{
    public class SqlServerContainerFixture : ContainerFixture
    {
        public const string ImageName = "mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04";

        private MsSqlContainer container;

        public SqlServerContainerFixture()
            : base(new SqlServerSpec())
        {
        }

        public override string Image => ImageName;

        protected override async Task<string> StartContainerAsync()
        {
            container = new MsSqlBuilder(ImageName).Build();
            await container.StartAsync().ConfigureAwait(false);
            return container.GetConnectionString();
        }

        protected override ValueTask DisposeContainerAsync()
        {
            return container == null ? default(ValueTask) : container.DisposeAsync();
        }
    }
}
