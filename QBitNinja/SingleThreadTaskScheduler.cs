using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QBitNinja
{
    public sealed class SingleThreadTaskScheduler : TaskScheduler, IDisposable
    {
        public SingleThreadTaskScheduler()
        {
            Thread thread = new Thread(RunOnCurrentThread);
            thread.IsBackground = true;
            thread.Start();
        }
        ManualResetEvent _End = new ManualResetEvent(false);
        public override int MaximumConcurrencyLevel
        {
            get
            {
                return 1;
            }
        }

        private readonly BlockingCollection<Task> m_queue = new BlockingCollection<Task>();


        void RunOnCurrentThread()
        {

            Task workItem;

            while (m_queue.TryTake(out workItem, Timeout.Infinite))
            {
                TryExecuteTask(workItem);
                if (_End.WaitOne(0))
                    break;
            }

        }



        public void Complete()
        {
            m_queue.CompleteAdding();
        }




        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return m_queue;
        }

        protected override void QueueTask(Task task)
        {
            m_queue.Add(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }


        #region IDisposable Members

        public void Dispose()
        {
            new Task(() => _End.Set()).Start(this);
            _End.WaitOne();
        }

        #endregion
    }
}
