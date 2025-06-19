using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace EnhancedStream_139.Utils
{
    /// <summary>
    /// 主线程调度器，确保 Unity UI 操作在主线程执行
    /// 参考 v3 的 MainThreadInvoker 实现
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();
        private static readonly object _queueLock = new object();
        private static int _mainThreadId;

        /// <summary>
        /// 获取当前是否在主线程
        /// </summary>
        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        [Inject]
        public void Construct()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 在主线程执行操作
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (IsMainThread)
            {
                // 如果已经在主线程，直接执行
                action();
                return;
            }

            lock (_queueLock)
            {
                _executionQueue.Enqueue(action);
            }
        }

        /// <summary>
        /// 在主线程异步执行操作
        /// </summary>
        public static Task InvokeAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();

            Enqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// 在主线程异步执行操作并返回结果
        /// </summary>
        public static Task<T> InvokeAsync<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();

            Enqueue(() =>
            {
                try
                {
                    var result = func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// 在主线程异步执行异步操作
        /// </summary>
        public static Task<T> InvokeAsync<T>(Func<Task<T>> asyncFunc)
        {
            var tcs = new TaskCompletionSource<T>();

            Enqueue(async () =>
            {
                try
                {
                    var result = await asyncFunc();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        void Update()
        {
            lock (_queueLock)
            {
                while (_executionQueue.Count > 0)
                {
                    try
                    {
                        _executionQueue.Dequeue().Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MainThreadDispatcher] Error executing queued action: {ex}");
                    }
                }
            }
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 确保实例存在（用于初始化）
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_instance == null)
            {
                var go = new GameObject("MainThreadDispatcher");
                _instance = go.AddComponent<MainThreadDispatcher>();
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                DontDestroyOnLoad(go);
            }
        }
    }
}