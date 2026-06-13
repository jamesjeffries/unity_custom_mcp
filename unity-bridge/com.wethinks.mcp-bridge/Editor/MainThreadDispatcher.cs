using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;

namespace WeThinks.Mcp.Editor
{
    /// <summary>
    /// Marshals work from the network background thread onto Unity's main
    /// thread. The Unity Editor API may only be touched on the main thread, so
    /// every command handler runs through here.
    /// </summary>
    public static class MainThreadDispatcher
    {
        private sealed class Job
        {
            public Func<object> Work;
            public object Result;
            public Exception Error;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private static readonly Queue<Job> Pending = new Queue<Job>();
        private static readonly object Gate = new object();
        private static bool _installed;

        /// <summary>
        /// Registers the pump on EditorApplication.update. Safe to call multiple
        /// times; only the first call has an effect.
        /// </summary>
        public static void EnsureInstalled()
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
            EditorApplication.update += Pump;
        }

        /// <summary>
        /// Queues <paramref name="work"/> to run on the main thread and blocks
        /// the calling (background) thread until it completes or times out.
        /// </summary>
        public static object Run(Func<object> work, int timeoutMs)
        {
            var job = new Job { Work = work };
            lock (Gate)
            {
                Pending.Enqueue(job);
            }

            if (!job.Done.Wait(timeoutMs))
            {
                throw new TimeoutException(
                    "Unity main thread did not process the command in time " +
                    "(it may be compiling or busy).");
            }

            if (job.Error != null)
            {
                throw job.Error;
            }

            return job.Result;
        }

        private static void Pump()
        {
            while (true)
            {
                Job job;
                lock (Gate)
                {
                    if (Pending.Count == 0)
                    {
                        return;
                    }

                    job = Pending.Dequeue();
                }

                try
                {
                    job.Result = job.Work();
                }
                catch (Exception ex)
                {
                    job.Error = ex;
                }
                finally
                {
                    job.Done.Set();
                }
            }
        }
    }
}
