using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Gerakul.FastSql.Common
{
    public class ConnectionScope : DbScope
    {
        private DbConnection connection;

        internal ConnectionScope(DbContext context, DbConnection connection) 
            : base(context)
        {
            this.connection = connection;
        }

        public override DbCommand CreateCommand(string commandText)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = commandText;
            return cmd;
        }
    }
}
