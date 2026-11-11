using Gerakul.FastSql.Common;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Gerakul.FastSql.SqlServer
{
    public partial class SqlContextProvider : ContextProvider
    {
        public static SqlContextProvider DefaultInstance { get; } = new SqlContextProvider();

        public SqlContextProvider()
        {
        }

        public override CommandTextGenerator CommandTextGenerator { get; } = new SqlCommandTextGenerator();
        public override QueryOptions QueryOptions { get; } = new SqlQueryOptions();
        public override BulkOptions BulkOptions { get; } = new SqlBulkOptions();

        protected override ConnectionStringContext GetConnectionStringContext(string connectionString, QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        {
            return new SqlConnectionStringContext(this, connectionString, queryOptions, bulkOptions, readOptions);
        }

        protected override ConnectionContext GetConnectionContext(DbConnection connection, QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        {
            return new SqlConnectionContext(this, (SqlConnection)connection, queryOptions, bulkOptions, readOptions);
        }

        protected override TransactionContext GetTransactionContext(DbTransaction transaction, QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        {
            return new SqlTransactionContext(this, (SqlTransaction)transaction, queryOptions, bulkOptions, readOptions);
        }

        //protected override void ApplyQueryOptions(DbCommand cmd, QueryOptions queryOptions)
        //{
        //    base.ApplyQueryOptions(cmd, queryOptions);
        //}

        protected override DbParameter AddParamWithValue(DbCommand cmd, string paramName, object value)
        {
            return ((SqlCommand)cmd).Parameters.AddWithValue(paramName, value);
        }

        protected override object GetDbNull(Type type)
        {
            if (type == typeof(byte[]))
            {
                return System.Data.SqlTypes.SqlBytes.Null;
            }

            return DBNull.Value;
        }

        protected override string GetSqlParameterName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Parameter name is empty", nameof(name));
            }

            return name[0] == '@' ? name[1..] : name;
        }

        // generated at compile time
        [GeneratedRegex("(([^@]@)|(^@))(?<Name>([a-z]|[A-Z]|[0-9]|[$#_])+)", RegexOptions.Multiline)]
        private static partial Regex ParameterNameRegex();

        protected override string[] ParamsFromCommandText(string commandText)
        {
            return ParameterNameRegex().Matches(RemoveLiteralsAndComments(commandText))
              .Select(x => "@" + x.Groups["Name"].Value)
              .Distinct().ToArray();
        }

        // an @ in a string literal ('...'), a quoted identifier ("..." or [...]) or a comment (-- to the end of the line,
        // or /* ... */, which can be nested) is not a parameter; each of them is replaced with a space
        private static string RemoveLiteralsAndComments(string commandText)
        {
            var result = new StringBuilder(commandText.Length);
            int i = 0;

            while (i < commandText.Length)
            {
                char c = commandText[i];
                char next = i + 1 < commandText.Length ? commandText[i + 1] : '\0';

                if (c == '\'' || c == '"' || c == '[')
                {
                    i = SkipQuoted(commandText, i + 1, c == '[' ? ']' : c);
                    result.Append(' ');
                }
                else if (c == '-' && next == '-')
                {
                    // the line break stays, so a parameter at the start of the next line is still found
                    while (i < commandText.Length && commandText[i] != '\n' && commandText[i] != '\r')
                    {
                        i++;
                    }

                    result.Append(' ');
                }
                else if (c == '/' && next == '*')
                {
                    i = SkipBlockComment(commandText, i + 2);
                    result.Append(' ');
                }
                else
                {
                    result.Append(c);
                    i++;
                }
            }

            return result.ToString();
        }

        // returns the position after the closing quote; a doubled closing quote belongs to the text
        private static int SkipQuoted(string text, int i, char close)
        {
            while (i < text.Length)
            {
                if (text[i] == close)
                {
                    if (i + 1 < text.Length && text[i + 1] == close)
                    {
                        i += 2;
                        continue;
                    }

                    return i + 1;
                }

                i++;
            }

            return i;
        }

        private static int SkipBlockComment(string text, int i)
        {
            int depth = 1;

            while (i < text.Length && depth > 0)
            {
                char next = i + 1 < text.Length ? text[i + 1] : '\0';

                if (text[i] == '/' && next == '*')
                {
                    depth++;
                    i += 2;
                }
                else if (text[i] == '*' && next == '/')
                {
                    depth--;
                    i += 2;
                }
                else
                {
                    i++;
                }
            }

            return i;
        }

        protected override string[] ParamsFromSettings<T>(IEnumerable<FieldSettings<T>> settings)
        {
            return settings.Select(x => "@" + x.Name).ToArray();
        }

        protected override BulkCopy GetBulkCopy(ScopedContext context, DbDataReader reader, string destinationTable, BulkOptions bulkOptions, params string[] fields)
        {
            return new MsSqlBulkCopy(context, reader, destinationTable, (SqlBulkOptions)bulkOptions, fields);
        }

        protected override QueryOptions GetTyped(QueryOptions options, out bool needClone)
        {
            needClone = false;
            return new SqlQueryOptions(options);
        }

        protected override BulkOptions GetTyped(BulkOptions options, out bool needClone)
        {
            needClone = false;
            return new SqlBulkOptions(options);
        }
    }
}
