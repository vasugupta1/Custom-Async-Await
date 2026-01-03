using System.Collections.Concurrent;

namespace Custom_Async_Await;

public class MyThreadPool
{
    /*
       How it Works
       The "Blocking" part of the name refers to its two most powerful behaviors:
       
       Blocking on Empty: If a consumer tries to take an item and the collection is empty, the thread will "sleep" until an item is added.
       
       Blocking on Full (Bounding): You can set a maximum capacity. If the collection is full, the producer thread will wait until a consumer removes an item, preventing your memory from exploding if the producer is much faster than the consumer.
       
       Blocking collection is basically a blocking queue but when something is popped out of it, it will lock to only allow one thread to pop a action at a time so this basically prevent race conditions when popping values
     */
    private static readonly BlockingCollection<(Action, ExecutionContext?)> _s_workItems = new();
    
    public static void QueueUserWorkItem(Action action)
    {
        _s_workItems.Add((action, ExecutionContext.Capture()));
    }

    static MyThreadPool()
    {
        for (var i = 0; i < Environment.ProcessorCount; i++)
        {
            new Thread(() =>
            {
                (Action workItem, ExecutionContext? context) = _s_workItems.Take();
                if (context is null)
                {
                    workItem.Invoke();
                }
                else
                {
                    ExecutionContext.Run(context, (object? state) => ((Action)state!).Invoke(), workItem);
                }
            })
            { 
                //make sure if main thread is finished then program exists, if set to false then all threads that have started will keep stop the main thread from exiting
                IsBackground = true, 
            }.Start();
        }
    }
}