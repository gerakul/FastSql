using System;
using System.Data;
using System.Data.Common;
using Gerakul.FastSql.Common;

namespace Gerakul.FastSql.Tests.Shared
{
    public enum ProviderKind
    {
        SqlServer,
        PostgreSql
    }

    // What differs between the providers in tests. Shared test classes are generic over the spec,
    // each provider has a thin concrete class.
    public abstract class ProviderSpec
    {
        public abstract ProviderKind Kind { get; }

        // the shared DefaultInstance: tests must not change its options
        public abstract ContextProvider DefaultProvider { get; }

        // a new provider instance, safe to change
        public abstract ContextProvider CreateProvider();

        public abstract DbConnection CreateConnection(string connectionString = null);

        public abstract string Quote(string identifier);

        // provider parameter created through DbCommand.CreateParameter, so tests do not depend on driver types
        public DbParameter CreateParameter(string name, object value, ParameterDirection direction = ParameterDirection.Input)
        {
            using (var connection = CreateConnection())
            using (var command = connection.CreateCommand())
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value ?? DBNull.Value;
                parameter.Direction = direction;
                return parameter;
            }
        }

        public override string ToString()
        {
            return Kind.ToString();
        }
    }
}
