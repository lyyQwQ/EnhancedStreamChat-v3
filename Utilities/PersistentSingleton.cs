using UnityEngine;

namespace EnhancedStreamChat.Utilities
{
    public class DoesNotRequireDomainReloadInitAttribute : PropertyAttribute
    {
    }

    public class PersistentSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        protected static T _instance;

        [DoesNotRequireDomainReloadInit] protected static object _lock = new object();

        protected static bool _applicationIsQuitting = false;

        public static T instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning(string.Concat("[Singleton] Instance '", typeof(T),
                        "' already destroyed on application quit. Won't create again - returning null."));
                    return null;
                }

                lock (_lock)
                {
                    if ((Object)_instance == (Object)null)
                    {
                        _instance = (T)Object.FindObjectOfType(typeof(T));
                        if (Object.FindObjectsOfType(typeof(T)).Length > 1)
                        {
                            Debug.LogError(
                                "[Singleton] Something went really wrong  - there should never be more than 1 singleton! Reopenning the scene might fix it.");
                            return _instance;
                        }

                        if ((Object)_instance == (Object)null)
                        {
                            GameObject obj = new GameObject();
                            _instance = obj.AddComponent<T>();
                            obj.name = typeof(T).ToString();
                            Object.DontDestroyOnLoad(obj);
                        }
                    }

                    return _instance;
                }
            }
        }

        public static bool IsSingletonAvailable
        {
            get
            {
                if (!_applicationIsQuitting)
                {
                    return (Object)_instance != (Object)null;
                }

                return false;
            }
        }

        public static void TouchInstance()
        {
            _ = (Object)instance == (Object)null;
        }

        public virtual void OnEnable()
        {
            Object.DontDestroyOnLoad(this);
        }

        protected virtual void OnDestroy()
        {
            _applicationIsQuitting = true;
        }
    }
}