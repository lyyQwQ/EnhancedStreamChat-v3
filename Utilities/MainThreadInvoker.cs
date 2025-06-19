using IPA.Utilities.Async;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EnhancedStreamChat.Utilities
{
    public class MainThreadInvoker
    {
        private static CancellationTokenSource s_cancellationToken = new CancellationTokenSource();

        public static void ClearQueue()
        {
            s_cancellationToken.Cancel();
            s_cancellationToken = new CancellationTokenSource();
        }
        
        /// <summary>
        /// 完全重置 MainThreadInvoker 状态，用于软重启
        /// </summary>
        public static void Reset()
        {
            try
            {
                s_cancellationToken?.Cancel();
                s_cancellationToken?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error($"[MainThreadInvoker] Error during reset: {ex}");
            }
            finally
            {
                s_cancellationToken = new CancellationTokenSource();
                Logger.Info("[MainThreadInvoker] State reset for soft restart");
            }
        }

        #region Void Methods (Fire-and-Forget) - 保持向后兼容

        public static void Invoke(Action? action)
        {
            if (action != null) {
                try {
                    UnityMainThreadTaskScheduler.Factory.StartNew(action, s_cancellationToken.Token);
                }
                catch (Exception ex) {
                    // 记录异常但不抛出，保持 fire-and-forget 语义
                    Logger.Error($"[MainThreadInvoker] Exception in Invoke: {ex}");
                }
            }
        }

        public static void Invoke<TA>(Action<TA?>? action, TA? a)
            where TA : class
        {
            if (action != null) {
                try {
                    UnityMainThreadTaskScheduler.Factory.StartNew(() => action(a), s_cancellationToken.Token);
                }
                catch (Exception ex) {
                    Logger.Error($"[MainThreadInvoker] Exception in Invoke<TA>: {ex}");
                }
            }
        }

        public static void Invoke<TA, TB>(Action<TA?, TB?>? action, TA? a, TB? b)
            where TA : class
            where TB : class
        {
            if (action != null) {
                try {
                    UnityMainThreadTaskScheduler.Factory.StartNew(() => action(a, b), s_cancellationToken.Token);
                }
                catch (Exception ex) {
                    Logger.Error($"[MainThreadInvoker] Exception in Invoke<TA,TB>: {ex}");
                }
            }
        }

        #endregion

        #region Task Methods (Awaitable) - 新增异步支持

        public static Task InvokeAsync(Action? action)
        {
            if (action == null) {
                return Task.CompletedTask;
            }
            
            return UnityMainThreadTaskScheduler.Factory.StartNew(action, s_cancellationToken.Token);
        }

        public static Task InvokeAsync<TA>(Action<TA?>? action, TA? a)
        {
            if (action == null) {
                return Task.CompletedTask;
            }
            
            return UnityMainThreadTaskScheduler.Factory.StartNew(() => action(a), s_cancellationToken.Token);
        }

        public static Task InvokeAsync<TA, TB>(Action<TA?, TB?>? action, TA? a, TB? b)
        {
            if (action == null) {
                return Task.CompletedTask;
            }
            
            return UnityMainThreadTaskScheduler.Factory.StartNew(() => action(a, b), s_cancellationToken.Token);
        }

        #endregion

        #region Value Type Support Methods - 支持值类型

        public static void InvokeValue<TA>(Action<TA>? action, TA a)
            where TA : struct
        {
            if (action != null) {
                try {
                    UnityMainThreadTaskScheduler.Factory.StartNew(() => action(a), s_cancellationToken.Token);
                }
                catch (Exception ex) {
                    Logger.Error($"[MainThreadInvoker] Exception in InvokeValue<TA>: {ex}");
                }
            }
        }

        public static Task InvokeValueAsync<TA>(Action<TA>? action, TA a)
            where TA : struct
        {
            if (action == null) {
                return Task.CompletedTask;
            }
            
            return UnityMainThreadTaskScheduler.Factory.StartNew(() => action(a), s_cancellationToken.Token);
        }

        #endregion
    }
}