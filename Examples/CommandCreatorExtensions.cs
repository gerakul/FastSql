using Gerakul.FastSql.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Examples
{
    public static partial class CommandCreatorExtensions
    {
        // making your own custom command accessible from all types of contexts (connection string, connection, transaction)
        public static IWrappedCommand CreateSelectTop10Employees(this ICommandCreator creator)
        {
            return creator.Set(c =>
            {
                var cmd = c.CreateCommand("select top 10 * from dbo.Employee");
                cmd.CommandType = System.Data.CommandType.Text;
                cmd.CommandTimeout = 1000;
                return cmd;
            });
        }
    }
}
