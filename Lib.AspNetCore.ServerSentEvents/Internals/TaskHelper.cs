using System.Threading;
using System.Threading.Tasks;

namespace Lib.AspNetCore.ServerSentEvents.Internals
{
    internal static class TaskHelper
    {
        #region Methods
        internal static Task WaitAsync(this CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> cancellationTaskCompletionSource = new TaskCompletionSource<bool>();
            cancellationToken.Register(CancellationTokenCallback, cancellationTaskCompletionSource);

            return cancellationToken.IsCancellationRequested ? Task.CompletedTask : cancellationTaskCompletionSource.Task;
        }

        private static void CancellationTokenCallback(object taskCompletionSource)
        {
            ((TaskCompletionSource<bool>)taskCompletionSource).SetResult(true);
        }
        #endregion
    }
}
