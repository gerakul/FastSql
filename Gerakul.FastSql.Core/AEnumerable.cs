using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Core
{
    internal class AEnumerable<TState, T> : IAsyncEnumerable<T> where TState : new()
    {
        private Func<TState, T> currentFunc;
        private Action<TState> disposeAction;
        private Func<TState, CancellationToken, Task> initTaskGetter;
        private Func<TState, CancellationToken, Task<bool>> moveNextTaskGetter;

        public AEnumerable(Func<TState, T> currentFunc, Action<TState> disposeAction,
          Func<TState, CancellationToken, Task> initTaskGetter, Func<TState, CancellationToken, Task<bool>> moveNextTaskGetter)
        {
            this.currentFunc = currentFunc;
            this.disposeAction = disposeAction;
            this.initTaskGetter = initTaskGetter;
            this.moveNextTaskGetter = moveNextTaskGetter;
        }

        public IAsyncEnumerator<T> GetEnumerator()
        {
            return new AEnumerator<TState, T>(new TState(), currentFunc, disposeAction, initTaskGetter, moveNextTaskGetter);
        }
    }

    internal class AEnumerator<TState, T> : IAsyncEnumerator<T>
    {
        private TState state;
        private Func<TState, T> currentFunc;
        private Action<TState> disposeAction;
        private Func<TState, CancellationToken, Task> initTaskGetter;
        private Func<TState, CancellationToken, Task<bool>> moveNextTaskGetter;
        private bool started = false;
        private bool disposed = false;

        internal AEnumerator(TState state, Func<TState, T> currentFunc, Action<TState> disposeAction,
          Func<TState, CancellationToken, Task> initTaskGetter, Func<TState, CancellationToken, Task<bool>> moveNextTaskGetter)
        {
            this.state = state;
            this.currentFunc = currentFunc;
            this.disposeAction = disposeAction;
            this.initTaskGetter = initTaskGetter;
            this.moveNextTaskGetter = moveNextTaskGetter;
        }

        public T Current
        {
            get
            {
                return currentFunc(state);
            }
        }

        public void Dispose()
        {
            disposeAction?.Invoke(state);
            disposed = true;
            GC.SuppressFinalize(this);
        }

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (disposed || cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (!started)
            {
                await initTaskGetter(state, cancellationToken).ConfigureAwait(false);
                started = true;
            }

            return await moveNextTaskGetter(state, cancellationToken).ConfigureAwait(false);
        }
    }
}
