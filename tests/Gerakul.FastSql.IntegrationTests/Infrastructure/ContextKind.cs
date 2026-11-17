using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Infrastructure
{
    // the three kinds of context the library offers; they run commands through different internal code
    public enum ContextKind
    {
        ConnectionString,
        Connection,
        Transaction
    }

    public static class ContextKinds
    {
        // usage: [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public static TheoryData<ContextKind> All => new TheoryData<ContextKind>()
        {
            ContextKind.ConnectionString,
            ContextKind.Connection,
            ContextKind.Transaction
        };
    }
}
