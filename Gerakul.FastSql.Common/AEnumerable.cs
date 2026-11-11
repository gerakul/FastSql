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
        private CancellationToken cancellationToken;

        public AEnumerable(Func<TState, T> currentFunc, Action<TState> disposeAction,
          Func<TState, CancellationToken, Task> initTaskGetter, Func<TState, CancellationToken, Task<bool>> moveNextTaskGetter,
          CancellationToken cancellationToken)
        {
            this.currentFunc = currentFunc;
            this.disposeAction = disposeAction;
            this.initTaskGetter = initTaskGetter;
            this.moveNextTaskGetter = moveNextTaskGetter;
            this.cancellationToken = cancellationToken;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default(CancellationToken))
        {
            // the token given to the query method and the one given to the enumeration both cancel the database calls
            CancellationTokenSource linkedTokenSource = null;
            CancellationToken callToken = cancellationToken;
            if (this.cancellationToken.CanBeCanceled)
            {
                if (cancellationToken.CanBeCanceled)
                {
                    linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.cancellationToken, cancellationToken);
                    callToken = linkedTokenSource.Token;
                }
                else
                {
                    callToken = this.cancellationToken;
                }
            }

            return new AEnumerator<TState, T>(new TState(), cancellationToken, callToken, linkedTokenSource, currentFunc, disposeAction, initTaskGetter, moveNextTaskGetter);
        }
    }

    internal class AEnumerator<TState, T> : IAsyncEnumerator<T>
    {
        private TState state;
        private CancellationToken cancellationToken;
        private CancellationToken callToken;
        private CancellationTokenSource linkedTokenSource;
        private Func<TState, T> currentFunc;
        private Action<TState> disposeAction;
        private Func<TState, CancellationToken, Task> initTaskGetter;
        private Func<TState, CancellationToken, Task<bool>> moveNextTaskGetter;
        private bool started = false;
        private bool disposed = false;

        internal AEnumerator(TState state, CancellationToken cancellationToken, CancellationToken callToken, CancellationTokenSource linkedTokenSource,
          Func<TState, T> currentFunc, Action<TState> disposeAction,
          Func<TState, CancellationToken, Task> initTaskGetter, Func<TState, CancellationToken, Task<bool>> moveNextTaskGetter)
        {
            this.state = state;
            this.cancellationToken = cancellationToken;
            this.callToken = callToken;
            this.linkedTokenSource = linkedTokenSource;
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
            linkedTokenSource?.Dispose();
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
                await initTaskGetter(state, callToken).ConfigureAwait(false);
                started = true;
            }

            return await moveNextTaskGetter(state, callToken).ConfigureAwait(false);
        }
    }
}
