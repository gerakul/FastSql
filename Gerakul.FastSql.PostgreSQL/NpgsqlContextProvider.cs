using Gerakul.FastSql.Common;
using System;
using System.Collections.Generic;
using System.Data.Common;
using Npgsql;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Gerakul.FastSql.PostgreSQL
{
    public class NpgsqlContextProvider : ContextProvider
    {
        public static NpgsqlContextProvider DefaultInstance { get; } = new NpgsqlContextProvider();

        public NpgsqlContextProvider()
        {
        }

        public override CommandTextGenerator CommandTextGenerator { get; } = new NpgsqlCommandTextGenerator();
        public override QueryOptions QueryOptions { get; } = new NpgsqlQueryOptions();
        public override BulkOptions BulkOptions { get; } = new NpgsqlBulkOptions();

        protected override ConnectionStringContext GetConnectionStringContext(string connectionString, QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        {
            return new NpgsqlConnectionStringContext(this, connectionString, queryOptions, bulkOptions, readOptions);
        }

        protected override ConnectionContext GetConnectionContext(DbConnection connection, QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        {
            return new NpgsqlConnectionContext(this, (NpgsqlConnection)connection, queryOptions, bulkOptions, readOptions);
        }

        protected override TransactionContext GetTransactionContext(DbTransaction transaction, QueryOptions queryOptions, BulkOptions bulkOptions, ReadOptions readOptions)
        {
            return new NpgsqlTransactionContext(this, (NpgsqlTransaction)transaction, queryOptions, bulkOptions, readOptions);
        }

        //protected override void ApplyQueryOptions(DbCommand cmd, QueryOptions queryOptions)
        //{
        //    base.ApplyQueryOptions(cmd, queryOptions);
        //}

        protected override DbParameter AddParamWithValue(DbCommand cmd, string paramName, object value)
        {
            return ((NpgsqlCommand)cmd).Parameters.AddWithValue(paramName, value);
        }

        protected override object GetDbNull(Type type)
        {
            return DBNull.Value;
        }

        protected override string GetSqlParameterName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Parameter name is empty", nameof(name));
            }

            return name[0] == '@' ? name.Substring(1) : name;
        }

        private static Regex regName = new Regex("(([^@]@)|(^@))(?<Name>([a-z]|[A-Z]|[0-9]|[$#_])+)", RegexOptions.Multiline);
        protected override string[] ParamsFromCommandText(string commandText)
        {
            return regName.Matches(RemoveLiteralsAndComments(commandText)).Cast<Match>()
              .Select(x => "@" + x.Groups["Name"].ToString())
              .Distinct().ToArray();
        }

        // an @ in a string literal ('...', E'...' with backslash escapes, $tag$...$tag$), a quoted identifier ("...")
        // or a comment (-- to the end of the line, or /* ... */, which can be nested) is not a parameter; each of them
        // is replaced with a space. Brackets are array subscripts in PostgreSQL, so they are not skipped
        private static string RemoveLiteralsAndComments(string commandText)
        {
            var result = new StringBuilder(commandText.Length);
            int i = 0;

            while (i < commandText.Length)
            {
                char c = commandText[i];
                char next = i + 1 < commandText.Length ? commandText[i + 1] : '\0';
                int dollarQuoteEnd;

                if (c == '\'')
                {
                    i = IsEscapeString(commandText, i) ? SkipEscapeString(commandText, i + 1) : SkipQuoted(commandText, i + 1, '\'');
                    result.Append(' ');
                }
                else if (c == '"')
                {
                    i = SkipQuoted(commandText, i + 1, '"');
                    result.Append(' ');
                }
                else if (c == '$' && (dollarQuoteEnd = SkipDollarQuoted(commandText, i)) > i)
                {
                    i = dollarQuoteEnd;
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

        private static bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '$';
        }

        // E'...' (or e'...'): the E is a prefix, not the end of an identifier
        private static bool IsEscapeString(string text, int quote)
        {
            return quote > 0 && (text[quote - 1] == 'E' || text[quote - 1] == 'e') && (quote < 2 || !IsIdentifierChar(text[quote - 2]));
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

        private static int SkipEscapeString(string text, int i)
        {
            while (i < text.Length)
            {
                if (text[i] == '\\')
                {
                    i += 2;
                    continue;
                }

                if (text[i] == '\'')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\'')
                    {
                        i += 2;
                        continue;
                    }

                    return i + 1;
                }

                i++;
            }

            return Math.Min(i, text.Length);
        }

        // $$...$$ or $tag$...$tag$; returns the position after the closing delimiter, or start when there is no
        // dollar quote here ($1 is a positional parameter, and a $ inside an identifier belongs to it)
        private static int SkipDollarQuoted(string text, int start)
        {
            if (start > 0 && IsIdentifierChar(text[start - 1]))
            {
                return start;
            }

            int i = start + 1;
            if (i < text.Length && (char.IsLetter(text[i]) || text[i] == '_'))
            {
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                {
                    i++;
                }
            }

            if (i >= text.Length || text[i] != '$')
            {
                return start;
            }

            var delimiter = text.Substring(start, i - start + 1);
            int end = text.IndexOf(delimiter, i + 1, StringComparison.Ordinal);
            return end < 0 ? text.Length : end + delimiter.Length;
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
            return new NpgsqlBulkCopy(context, reader, destinationTable, (NpgsqlBulkOptions)bulkOptions, fields);
        }

        protected override QueryOptions GetTyped(QueryOptions options, out bool needClone)
        {
            needClone = false;
            return new NpgsqlQueryOptions(options);
        }

        protected override BulkOptions GetTyped(BulkOptions options, out bool needClone)
        {
            needClone = false;
            return new NpgsqlBulkOptions(options);
        }
    }
}
