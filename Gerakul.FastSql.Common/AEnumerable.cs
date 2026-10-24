using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gerakul.FastSql.Common
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

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default(CancellationToken))
        {
            return new AEnumerator<TState, T>(new TState(), cancellationToken, currentFunc, disposeAction, initTaskGetter, moveNextTaskGetter);
        }
    }

    internal class AEnumerator<TState, T> : IAsyncEnumerator<T>
    {
        private TState state;
        private CancellationToken cancellationToken;
        private Func<TState, T> currentFunc;
        private Action<TState> disposeAction;
        private Func<TState, CancellationToken, Task> initTaskGetter;
        private Func<TState, CancellationToken, Task<bool>> moveNextTaskGetter;
        private bool started = false;
        private bool disposed = false;

        internal AEnumerator(TState state, CancellationToken cancellationToken, Func<TState, T> currentFunc, Action<TState> disposeAction,
          Func<TState, CancellationToken, Task> initTaskGetter, Func<TState, CancellationToken, Task<bool>> moveNextTaskGetter)
        {
            this.state = state;
            this.cancellationToken = cancellationToken;
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

        public ValueTask DisposeAsync()
        {
            disposeAction?.Invoke(state);
            disposed = true;
            GC.SuppressFinalize(this);
            return default(ValueTask);
        }

        public async ValueTask<bool> MoveNextAsync()
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
