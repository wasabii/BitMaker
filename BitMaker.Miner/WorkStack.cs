using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

using BitMaker.Miner.Plugin;

namespace BitMaker.Miner
{

    internal sealed class WorkStack : IProducerConsumerCollection<Work>
    {

        /// <summary>
        /// Storage for work items.
        /// </summary>
        private ConcurrentStack<Work> stack = new ConcurrentStack<Work>();

        public bool TryAdd(Work item)
        {
            return ((IProducerConsumerCollection<Work>)stack).TryAdd(item);
        }

        public bool TryTake(out Work item)
        {
            // continue trying to pop an item off the stack until either no items, or successful
            do
            {
                if (!stack.TryPop(out item))
                    return false;
            }
            while (item != null && item.Token.IsCancellationRequested);

            // we succeeded if we have an item
            return item != null;
        }

        public Work[] ToArray()
        {
            return stack.ToArray();
        }

        public void CopyTo(Work[] array, int index)
        {
            stack.CopyTo(array, index);
        }

        public void CopyTo(Array array, int index)
        {
            Array.Copy(stack.ToArray(), index, array, 0, stack.Count - index);
        }

        public int Count
        {
            get { return stack.Count; }
        }

        public IEnumerator<Work> GetEnumerator()
        {
            return stack.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool IsSynchronized
        {
            get { throw new NotImplementedException(); }
        }

        public object SyncRoot
        {
            get { throw new NotImplementedException(); }
        }

    }

}
