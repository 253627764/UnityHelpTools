using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using System;

namespace HSJTools
{
    public class SingleThreadWorker
    {
        private Thread mThread;
        private volatile bool mStopFlag;

        private Queue<Action> mTaskQueue = new Queue<Action>();

        private EventWaitHandle mNewTaskEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        private static int kEventCount;

        public SingleThreadWorker()
        {
            mThread = new Thread(new ThreadStart(WorkerProc));
            mThread.Start();
        }

        public void Stop()
        {
            mStopFlag = true;
        }

        public void StartTask(Action task)
        {
            if (task == null)
            {
                throw new Exception("param 1 can't be null");
            }
            Queue<Action> taskQueue = mTaskQueue;
            lock (taskQueue)
            {
                mTaskQueue.Enqueue(task);
            }
            mNewTaskEvent.Set();
        }

        public void StartTaskWithCallback<T>(Func<T> task, Action<T> onFinish, Action<Exception> onException)
        {
            if (task == null)
            {
                throw new Exception("param 1 can't be null.");
            }
            StartTask(delegate
            {
                T obj;
                try
                {
                    obj = task();
                }
                catch (Exception ex)
                {
                    if (onException != null)
                    {
                        onException(ex);
                        return;
                    }
                    throw;
                }
                if (onFinish != null)
                {
                    onFinish(obj);
                }
            });
        }

        public void SetThreadPriority(System.Threading.ThreadPriority priority)
        {
            mThread.Priority = priority;
        }

        private void WorkerProc()
        {
            while (!mStopFlag)
            {
                while (true)
                {
                    Action action = null;
                    Queue<Action> taskQueue = mTaskQueue;
                    lock (taskQueue)
                    {
                        if (mTaskQueue.Count == 0)
                        {
                            break;
                        }
                        action = mTaskQueue.Dequeue();
                    }
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Debug.Log(ex.Message);
                    }
                }
                mNewTaskEvent.WaitOne();
            }
        }
    }
}

