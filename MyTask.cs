using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Custom_Async_Await;

public struct Awaiter(MyTask task) : INotifyCompletion
{
    public bool IsCompleted => task.IsCompleted;
    public void OnCompleted(Action continuation) => task.ContinueWith(continuation);
    public void GetResult() => task.Wait();
}

public class MyTask
{
    private bool _completed;
    private Exception? _exception;
    private Action? _continuation;
    private ExecutionContext? _executionContext;
    public Awaiter GetAwaiter() => new(this);
    
   /// <summary>
   /// When you use lock(this), you are locking on the current instance of the class. Because the instance is likely public (or at least accessible to other parts of your code), any external code can also lock on that same instance.
   /// </summary>
    public bool IsCompleted
    {
        get
        {
            lock (this)
            {
                return _completed;
            }
        } 
        
    }

    public void Complete(Exception? exception)
    {
        lock (this)
        {
            if (_completed)
            {
                throw new InvalidOperationException("Already completed");
            }
            _exception = exception;
            _completed = true;

            if (_continuation is not null)
            {
               MyThreadPool.QueueUserWorkItem(() =>
               {
                   if (_executionContext is null)
                   {
                       _continuation.Invoke();
                   }
                   else
                   {
                       ExecutionContext.Run(_executionContext, (object? state) => ((Action)state!).Invoke(), _continuation);
                   }
               });
            }
        }
    }

    public void SetResult() => Complete(null);

    public void SetException(Exception exception) =>  Complete(exception);

    /// <summary>
    // Here is exactly what happens when a thread calls Wait():
    // The Quick Check: It enters a lock and checks if (!_completed). If the task is already done, it skips the logic entirely and returns immediately. This is the "fast path."
    // The "Gate" Creation: If the task is not done, it creates a new ManualResetEventSlim(false). This is a gate that starts in the closed state.
    //  The Callback Registration: It calls ContinueWith(resetEvent.Set). This is the clever part. It tells the task: "When you finally finish, please call .Set() on this gate to open it.
    // The Block: Finally, outside the lock, it calls resetEvent?.Wait(). The thread now sits there and does nothing (it's blocked) until the task finishes and opens the gate.
    // This act of stopping a thread untill something esle completes is a called synchronous blocking
    // because you are starting the work, stopping the thread untill it something finishes    
    /// </summary>
    public void Wait()
    {
        ManualResetEventSlim resetEvent = null;
        lock (this)
        {
            if (!_completed)
            {
                /*
                 * In your MyTask implementation, the Wait() method is performing a synchronous block. Its job is to stop the current thread from moving forward until the task has actually finished (either successfully or with an exception).   
                   Think of it as turning an asynchronous operation back into a "stop and wait" operation.
                   This is synchronous blocking:
                    CPU is not used
                    But a thread is consumed
                    Stack memory is held

                Thread pool capacity is reduced
                 */
                resetEvent = new ManualResetEventSlim();
                ContinueWith(resetEvent.Set);
            }
        }

        resetEvent?.Wait();

        if (_exception is not null)
        {
            ExceptionDispatchInfo.Throw(_exception);
        }
    }

    public MyTask ContinueWith(Action action)
    {
        var t = new MyTask();
        Action callback = () =>
        {
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                t.SetException(e);
                return;
            }
            t.SetResult();
        };
        lock (this)
        {
            if (_completed)
            {
                MyThreadPool.QueueUserWorkItem(action);
            }
            else
            {
                _continuation = callback;
                _executionContext = ExecutionContext.Capture();
            }
        }

        return t;
    }
    
    public MyTask ContinueWith(Func<MyTask> action)
    {
        var t = new MyTask();
        Action callback = () =>
        {
            try
            {
                var next = action.Invoke();
                next.ContinueWith(() =>
                {
                    if (next._exception is not null)
                    {
                        t.SetException(next._exception);
                    }
                    else
                    {
                        t.SetResult();
                    }
                });
            }
            catch (Exception e)
            {
                t.SetException(e);
                return;
            }
            
        };
        lock (this)
        {
            if (_completed)
            {
                MyThreadPool.QueueUserWorkItem(callback);
            }
            else
            {
                _continuation = callback;
                _executionContext = ExecutionContext.Capture();
            }
        }

        return t;
    }
    
    public static MyTask Run(Action action)
    {
        var t = new MyTask();
        MyThreadPool.QueueUserWorkItem(() =>
        {
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                t.SetException(e);
                return;
            }
            
            t.SetResult();
        });
        return t;
    }

    public static MyTask WaitAll(List<MyTask> tasks)
    {
        var t = new MyTask();
        if (tasks.Count == 0)
        {
            t.SetResult();
        }
        else
        {
            var remainingTasks = tasks.Count;
            Action? continuation = () =>
            {
                // These task could complete at the same time so we could lose when tasks remaning was 0
                // This is a lightweight sync method, we could also use a lock to lock on the int
                if (Interlocked.Decrement(ref remainingTasks) == 0)
                {
                    t.SetResult();
                }
            };
            foreach (var task in tasks)
            {
                task.ContinueWith(continuation);
            }
        }
        return t;
    }

    public static MyTask Delay(int timeout)
    {
        var t = new MyTask();
        // we don;t want to use thread.Sleep as that thread is move to sleep state and after the interval move to running state
        // this way we still have that thread avaiable for other process to use
        new Timer(_ => t.SetResult()).Change(timeout, -1);
        return t;
    }

    public static MyTask Iterate(IEnumerable<MyTask> tasks)
    {
        var t = new MyTask();
        var e = tasks.GetEnumerator();

        void MoveNext()
        {
            try
            {
                if (e.MoveNext())
                {
                    var nextTask = e.Current;
                    nextTask.ContinueWith(MoveNext);
                    return;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
            t.SetResult();
        }
        
        MoveNext();
        return t;
    }
}