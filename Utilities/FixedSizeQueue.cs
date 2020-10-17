using System;
using System.Collections.Concurrent;

namespace EnhancedStreamChat.Utilities
{
    public class FixedSizedQueue<T> : ConcurrentQueue<T>
    {
        private readonly object _object = new object();

        public int Size { get; private set; }

        private event Action<T> OnFree;

        public FixedSizedQueue(int size, Action<T> OnFree)
        {
            Size = size;
            this.OnFree += OnFree;
        }

        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);
            lock (_object)
            {
                while (Count > Size)
                {
                    if(TryDequeue(out var outObj))
                    {
                        OnFree?.Invoke(outObj);
                    }
                }
            }
        }
    }
}
