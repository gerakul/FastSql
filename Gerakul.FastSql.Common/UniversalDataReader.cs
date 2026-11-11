using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
{
    public class UniversalDataReader<T> : DbDataReader
    {
        private readonly IEnumerator<T> enumerator;
        private readonly IAsyncEnumerator<T> asyncEnumerator;
        private bool isClosed;
        private T curRow;
        private readonly int fieldCount;
        private readonly Func<T, object>[] getters;
        private readonly Func<T, bool>[] isNullGetters;
        private readonly string[] dataTypeNames;
        private readonly Type[] types;
        private readonly string[] names;
        private readonly Dictionary<string, int> ordinals;

        private readonly FieldSettings<T>[] settings;

        public ReadOnlyCollection<FieldSettings<T>> Settings => new ReadOnlyCollection<FieldSettings<T>>(settings);

        private UniversalDataReader(IEnumerable<FieldSettings<T>> settings, bool caseSensitive)
        {
            this.settings = settings.ToArray();
            this.fieldCount = this.settings.Length;

            this.getters = new Func<T, object>[this.fieldCount];
            this.isNullGetters = new Func<T, bool>[this.fieldCount];
            this.dataTypeNames = new string[this.fieldCount];
            this.types = new Type[this.fieldCount];
            this.names = new string[this.fieldCount];
            this.ordinals = new Dictionary<string, int>(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < this.fieldCount; i++)
            {
                var item = this.settings[i];
                this.getters[i] = item.Getter;
                this.isNullGetters[i] = item.IsNullGetter;
                this.dataTypeNames[i] = item.DataTypeName;
                this.types[i] = item.FieldType;
                this.names[i] = item.Name;
                this.ordinals.Add(item.Name, i);
            }
        }

        public UniversalDataReader(IEnumerable<T> values, IEnumerable<FieldSettings<T>> settings, bool caseSensitive)
          : this(settings, caseSensitive)
        {
            this.enumerator = values.GetEnumerator();
        }

        public UniversalDataReader(IAsyncEnumerable<T> values, IEnumerable<FieldSettings<T>> settings, bool caseSensitive)
           : this(settings, caseSensitive)
        {
            this.asyncEnumerator = values.GetAsyncEnumerator();
        }

        #region DbDataReader

        // hides DbDataReader.Close instead of overriding it (Q01): closing through a DbDataReader reference does nothing
        public new void Close()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                if (!isClosed)
                {
                    isClosed = true;

                    // the source can hold resources, for example a FastSql query keeps its reader and connection
                    enumerator?.Dispose();
                    asyncEnumerator?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
        }

        // disposes an async source without blocking on it, unlike Dispose
        public override async ValueTask DisposeAsync()
        {
            if (!isClosed)
            {
                isClosed = true;
                enumerator?.Dispose();

                if (asyncEnumerator != null)
                {
                    await asyncEnumerator.DisposeAsync().ConfigureAwait(false);
                }
            }

            await base.DisposeAsync().ConfigureAwait(false);
        }

        public override int Depth => 0;

        public override bool IsClosed => isClosed;

        // there is only one result set
        public override bool NextResult()
        {
            return false;
        }

        public override bool Read()
        {
            if (isClosed)
            {
                throw new InvalidOperationException("DataReader has been closed");
            }

            if (enumerator == null)
            {
                // a move that has already completed (buffered rows, in-memory sources) is read without a task
                var move = asyncEnumerator.MoveNextAsync();
                if (move.IsCompletedSuccessfully ? move.Result : move.AsTask().GetAwaiter().GetResult())
                {
                    curRow = asyncEnumerator.Current;
                    return true;
                }
            }
            else
            {
                if (enumerator.MoveNext())
                {
                    curRow = enumerator.Current;
                    return true;
                }
            }

            return false;
        }

        public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            if (isClosed)
            {
                throw new InvalidOperationException("DataReader has been closed");
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (enumerator == null)
            {
                if (await asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    curRow = asyncEnumerator.Current;
                    return true;
                }
            }
            else
            {
                if (enumerator.MoveNext())
                {
                    curRow = enumerator.Current;
                    return true;
                }
            }

            return false;
        }

        // -1, as for a select statement
        public override int RecordsAffected => -1;

        public override int FieldCount => fieldCount;

        public override bool HasRows => throw new NotImplementedException();

        public override bool GetBoolean(int i)
        {
            return (bool)getters[i](curRow);
        }

        public override byte GetByte(int i)
        {
            return (byte)getters[i](curRow);
        }

        public override long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferOffset, int length)
        {
            byte[] bytes = (byte[])getters[i](curRow);

            // without a buffer the length of the value is returned (IDataRecord contract)
            if (buffer == null)
            {
                return bytes.Length;
            }

            if (fieldOffset < 0 || fieldOffset >= bytes.Length || bufferOffset < 0 || bufferOffset >= buffer.Length)
            {
                return 0;
            }

            int fo = checked((int)fieldOffset);

            int len = Math.Min(Math.Min(bytes.Length - fo, buffer.Length - bufferOffset), length);
            Array.Copy(bytes, fo, buffer, bufferOffset, len);

            return len;
        }

        public override char GetChar(int i)
        {
            return (char)getters[i](curRow);
        }

        public override long GetChars(int i, long fieldOffset, char[] buffer, int bufferOffset, int length)
        {
            var value = getters[i](curRow);

            // a string is read in place, not copied as a whole on every call
            ReadOnlySpan<char> chars = value is string str ? str.AsSpan() : (char[])value;

            // without a buffer the length of the value is returned (IDataRecord contract)
            if (buffer == null)
            {
                return chars.Length;
            }

            if (fieldOffset < 0 || fieldOffset >= chars.Length || bufferOffset < 0 || bufferOffset >= buffer.Length)
            {
                return 0;
            }

            int fo = checked((int)fieldOffset);

            int len = Math.Min(Math.Min(chars.Length - fo, buffer.Length - bufferOffset), length);
            chars.Slice(fo, len).CopyTo(buffer.AsSpan(bufferOffset));

            return len;
        }

        public override string GetDataTypeName(int i)
        {
            return dataTypeNames[i];
        }

        public override DateTime GetDateTime(int i)
        {
            return (DateTime)getters[i](curRow);
        }

        public override decimal GetDecimal(int i)
        {
            return (decimal)getters[i](curRow);
        }

        public override double GetDouble(int i)
        {
            return (double)getters[i](curRow);
        }

        public override Type GetFieldType(int i)
        {
            return types[i];
        }

        public override float GetFloat(int i)
        {
            return (float)getters[i](curRow);
        }

        public override Guid GetGuid(int i)
        {
            return (Guid)getters[i](curRow);
        }

        public override short GetInt16(int i)
        {
            return (short)getters[i](curRow);
        }

        public override int GetInt32(int i)
        {
            return (int)getters[i](curRow);
        }

        public override long GetInt64(int i)
        {
            return (long)getters[i](curRow);
        }

        public override string GetName(int i)
        {
            return names[i];
        }

        public override int GetOrdinal(string name)
        {
            // IDataRecord documents IndexOutOfRangeException for an unknown name
            if (!this.ordinals.TryGetValue(name, out int ordinal))
            {
                throw new IndexOutOfRangeException($"Field '{name}' is not found");
            }

            return ordinal;
        }

        public override string GetString(int i)
        {
            return (string)getters[i](curRow);
        }

        public override object GetValue(int i)
        {
            return getters[i](curRow);
        }

        public override int GetValues(object[] values)
        {
            int res = Math.Min(values.Length, fieldCount);
            for (int i = 0; i < res; i++)
            {
                values[i] = getters[i](curRow);
            }

            return res;
        }

        public override bool IsDBNull(int i)
        {
            return isNullGetters[i](curRow);
        }

        public override IEnumerator GetEnumerator()
        {
            return enumerator;
        }

        public override object this[string name] => getters[GetOrdinal(name)](curRow);

        public override object this[int i] => getters[i](curRow);

        #endregion
    }
}
