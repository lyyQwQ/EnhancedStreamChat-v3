using System;
using System.Collections.Concurrent;

namespace EnhancedStreamChat.Utilities
{
	public class FixedSizedQueue<T> : ConcurrentQueue<T>
	{
		private readonly object _object = new object();

        public int Size { get; }

		private event Action<T> OnFree;

        public FixedSizedQueue(int size, Action<T> onFree)
        {
            this.Size = size;
            OnFree += onFree;
        }

        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);
            lock (this._object) {
                while (this.Count > this.Size) {
                    if (this.TryDequeue(out var outObj)) {
                        OnFree?.Invoke(outObj);
                    }
                }
            }
        }
    }
}