using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Core
{
    public class UniversalDataReader<T> : DbDataReader
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

        #region DbDataReader

        public void Close()
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
                }
            }
        }

        public override int Depth
        {
            get
            {
                return 0;
            }
        }

        public override bool IsClosed
        {
            get
            {
                return isClosed;
            }
        }

        public override bool NextResult()
        {
            throw new NotImplementedException();
        }

        public override bool Read()
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

        public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
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

        public override int RecordsAffected
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override int FieldCount
        {
            get
            {
                return fieldCount;
            }
        }

        public override bool HasRows
        {
            get
            {
                throw new NotImplementedException();
            }
        }

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
            char[] chars = (char[])getters[i](curRow);

            if (fieldOffset < 0 || fieldOffset >= chars.Length || bufferOffset < 0 || bufferOffset >= buffer.Length)
            {
                return 0;
            }

            int fo = checked((int)fieldOffset);

            int len = Math.Min(Math.Min(chars.Length - fo, buffer.Length - bufferOffset), length);
            Array.Copy(chars, fo, buffer, bufferOffset, len);

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
            return this.ordinals[name.ToLowerInvariant()];
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

        public override object this[string name]
        {
            get
            {
                return getters[ordinals[name.ToLowerInvariant()]](curRow);
            }
        }

        public override object this[int i]
        {
            get
            {
                return getters[i](curRow);
            }
        }

        #endregion
    }
}
