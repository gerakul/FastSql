using System;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.SqlServer;
using Gerakul.FastSql.Tests.Shared;

namespace Gerakul.FastSql.UnitTests.Infrastructure
{
    public static class ProviderSpecExtensions
    {
        // without a provider, a new provider instance is used, so tests never share option changes
        public static ICapturingContext CreateCapturingContext(this ProviderSpec spec, ContextProvider provider = null)
        {
            var contextProvider = provider ?? spec.CreateProvider();

            switch (spec.Kind)
            {
                case ProviderKind.SqlServer:
                    return new CapturingSqlConnectionContext((SqlContextProvider)contextProvider);
                case ProviderKind.PostgreSql:
                    return new CapturingNpgsqlConnectionContext((NpgsqlContextProvider)contextProvider);
                default:
                    throw new ArgumentOutOfRangeException(nameof(spec), spec.Kind, "Unknown provider");
            }
        }
    }
}
