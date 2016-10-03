using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql
{
    public class UniversalDataReader<T> : IDataReader
    {
        private IEnumerator<T> enumerator;
        private IAsyncEnumerator<T> asyncEnumerator;
        private bool isClosed;
        private T curRow;
        private int fieldCount;
        private Func<T, object>[] getters;
        private Func<T, bool>[] isNullGetters;
        private string[] dataTypeNames;
        private Type[] types;
        private string[] names;
        private Dictionary<string, int> ordinals;

        private FieldSettings<T>[] settings;

        public ReadOnlyCollection<FieldSettings<T>> Settings
        {
            get
            {
                return new ReadOnlyCollection<FieldSettings<T>>(settings);
            }
        }

        private UniversalDataReader(IEnumerable<FieldSettings<T>> settings)
        {
            this.isClosed = false;
            this.settings = settings.ToArray();
            this.ApplySettings();
        }

        public UniversalDataReader(IEnumerable<T> values, IEnumerable<FieldSettings<T>> settings)
          : this(settings)
        {
            this.enumerator = values.GetEnumerator();
        }

        public UniversalDataReader(IAsyncEnumerable<T> values, IEnumerable<FieldSettings<T>> settings)
           : this(settings)
        {
            this.asyncEnumerator = values.GetEnumerator();
        }

        private void ApplySettings()
        {
            this.fieldCount = settings.Length;

            this.getters = new Func<T, object>[this.fieldCount];
            this.isNullGetters = new Func<T, bool>[this.fieldCount];
            this.dataTypeNames = new string[this.fieldCount];
            this.types = new Type[this.fieldCount];
            this.names = new string[this.fieldCount];
            this.ordinals = new Dictionary<string, int>();

            for (int i = 0; i < settings.Length; i++)
            {
                this.getters[i] = settings[i].Getter;
                this.isNullGetters[i] = settings[i].IsNullGetter;
                this.dataTypeNames[i] = settings[i].DataTypeName;
                this.types[i] = settings[i].FieldType;
                this.names[i] = settings[i].Name;
                this.ordinals.Add(settings[i].Name.ToLowerInvariant(), i);
            }
        }

        #region IDataReader

        public void Close()
        {
            isClosed = true;
        }

        public int Depth
        {
            get
            {
                return 0;
            }
        }

        public DataTable GetSchemaTable()
        {
            throw new NotImplementedException();
        }

        public bool IsClosed
        {
            get
            {
                return isClosed;
            }
        }

        public bool NextResult()
        {
            throw new NotImplementedException();
        }

        public bool Read()
        {
            if (isClosed)
            {
                throw new InvalidOperationException("DataReader has been closed");
            }

            if (enumerator == null)
            {
                if (asyncEnumerator.MoveNext().Result)
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

        public async Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            if (isClosed)
            {
                throw new InvalidOperationException("DataReader has been closed");
            }

            if (enumerator == null)
            {
                if (await asyncEnumerator.MoveNext().ConfigureAwait(false))
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

        public Task<bool> ReadAsync()
        {
            return ReadAsync(CancellationToken.None);
        }

        public int RecordsAffected
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public void Dispose()
        {
            Close();
        }

        public int FieldCount
        {
            get
            {
                return fieldCount;
            }
        }

        public bool GetBoolean(int i)
        {
            return (bool)getters[i](curRow);
        }

        public byte GetByte(int i)
        {
            return (byte)getters[i](curRow);
        }

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferOffset, int length)
        {
            byte[] bytes = (byte[])getters[i](curRow);

            if (fieldOffset < 0 || fieldOffset >= bytes.Length || bufferOffset < 0 || bufferOffset >= buffer.Length)
            {
                return 0;
            }

            int len = Math.Min((int)Math.Min(bytes.Length - fieldOffset, buffer.Length - bufferOffset), length);
            Array.Copy(bytes, fieldOffset, buffer, bufferOffset, len);

            return len;
        }

        public char GetChar(int i)
        {
            return (char)getters[i](curRow);
        }

        public long GetChars(int i, long fieldOffset, char[] buffer, int bufferOffset, int length)
        {
            char[] chars = (char[])getters[i](curRow);

            if (fieldOffset < 0 || fieldOffset >= chars.Length || bufferOffset < 0 || bufferOffset >= buffer.Length)
            {
                return 0;
            }

            int len = Math.Min((int)Math.Min(chars.Length - fieldOffset, buffer.Length - bufferOffset), length);
            Array.Copy(chars, fieldOffset, buffer, bufferOffset, len);

            return len;
        }

        public IDataReader GetData(int i)
        {
            return (IDataReader)getters[i](curRow);
        }

        public string GetDataTypeName(int i)
        {
            return dataTypeNames[i];
        }

        public DateTime GetDateTime(int i)
        {
            return (DateTime)getters[i](curRow);
        }

        public decimal GetDecimal(int i)
        {
            return (decimal)getters[i](curRow);
        }

        public double GetDouble(int i)
        {
            return (double)getters[i](curRow);
        }

        public Type GetFieldType(int i)
        {
            return types[i];
        }

        public float GetFloat(int i)
        {
            return (float)getters[i](curRow);
        }

        public Guid GetGuid(int i)
        {
            return (Guid)getters[i](curRow);
        }

        public short GetInt16(int i)
        {
            return (short)getters[i](curRow);
        }

        public int GetInt32(int i)
        {
            return (int)getters[i](curRow);
        }

        public long GetInt64(int i)
        {
            return (long)getters[i](curRow);
        }

        public string GetName(int i)
        {
            return names[i];
        }

        public int GetOrdinal(string name)
        {
            return this.ordinals[name.ToLowerInvariant()];
        }

        public string GetString(int i)
        {
            return (string)getters[i](curRow);
        }

        public object GetValue(int i)
        {
            return getters[i](curRow);
        }

        public int GetValues(object[] values)
        {
            int res = Math.Min(values.Length, fieldCount);
            for (int i = 0; i < res; i++)
            {
                values[i] = getters[i](curRow);
            }

            return res;
        }

        public bool IsDBNull(int i)
        {
            return isNullGetters[i](curRow);
        }

        public object this[string name]
        {
            get
            {
                return getters[ordinals[name.ToLowerInvariant()]](curRow);
            }
        }

        public object this[int i]
        {
            get
            {
                return getters[i](curRow);
            }
        }

        #endregion
    }
}
