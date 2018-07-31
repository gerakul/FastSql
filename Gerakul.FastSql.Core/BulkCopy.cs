using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Core
{
    public abstract class BulkCopy
    {
        public abstract void WriteToServer();

        public abstract Task WriteToServerAsync(CancellationToken cancellationToken);

        public Task WriteToServerAsync()
        {
            return WriteToServerAsync(CancellationToken.None);
        }
    }
}
