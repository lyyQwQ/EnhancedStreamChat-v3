using System;
using UnityEngine;
using Zenject;

namespace EnhancedStreamChat.Utilities
{
    /// <summary>
    /// 共享的协程启动器，用于在没有 MonoBehaviour 的情况下启动协程
    /// 已迁移到 Zenject 管理
    /// </summary>
    internal class SharedCoroutineStarter : MonoBehaviour, IInitializable
    {
        // 单例实例保留用于向后兼容
        private static SharedCoroutineStarter _instance;
        public static SharedCoroutineStarter Instance 
        { 
            get 
            {
                if (_instance == null)
                {
                    Logger.Warn("SharedCoroutineStarter.Instance accessed before initialization. This should be replaced with dependency injection.");
                }
                return _instance;
            }
        }
        
        // Zenject 构造函数
        [Inject]
        public void Construct()
        {
            // SharedCoroutineStarter 目前没有依赖项
            // 未来可以在这里注入其他服务
        }
        
        // IInitializable 实现
        public void Initialize()
        {
            _instance = this; // 设置单例实例用于向后兼容
            Logger.Log.Info("SharedCoroutineStarter initialized as Zenject service");
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }

    /// <summary>
    /// 旧的协程运行器，保留用于向后兼容
    /// </summary>
    [Obsolete("使用 SharedCoroutineStarter 代替 CoroutineRunner")]
    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;

        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("CoroutineRunner");
                    _instance = go.AddComponent<CoroutineRunner>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
    }

}
