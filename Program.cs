using Custom_Async_Await;

/*
We have threadpool, concurrency != parrelism as we know but async await utlises threads 
*/
// for (var i = 0; i < 100; i++)
// {
//     // this won't display i 0->100 and that is because all I am doing here is queueing 100 work items and going away doing something else
//     // by the time the work item do start and work, i is already at 100
//     ThreadPool.QueueUserWorkItem(delegate { Console.WriteLine(i); });
// }


// for (var i = 0; i < 100; i++)
// {
//     //create a var in local scope and that will be used by the threads
//     // this value is being stored and just passed to the delegate below
//     var capturedValue = i;
//     MyThreadPool.QueueUserWorkItem(delegate { Console.WriteLine(capturedValue); });
// }

/*
 * Use async local is correct approach but the context in which the value has been set is not being used by the thread pool hence we need to use that when operation on the action
 */
// var asyncLocalValue = new AsyncLocal<int>();
//
// for (var i = 0; i < 100; i++)
// {
//     asyncLocalValue.Value = i;
//     MyThreadPool.QueueUserWorkItem(delegate { Console.WriteLine(asyncLocalValue.Value); });
// }


// var asyncLocalValue = new AsyncLocal<int>();
// var tasks = new List<MyTask>();
//
// for (var i = 0; i < 100; i++)
// {
//     asyncLocalValue.Value = i;
//     tasks.Add(MyTask.Run(() =>
//     {
//         Console.WriteLine(asyncLocalValue.Value);
//         Thread.Sleep(10);
//     }));
// }
//
// MyTask.WaitAll(tasks);

// Console.Write("Hello,");
// MyTask.Delay(2000).ContinueWith(() =>
// {
//     Console.Write(" World");
//     return MyTask.Delay(2000).ContinueWith(() =>
//     {
//         Console.Write(" and Vas");
//     });
//
// }).Wait();
// Console.ReadKey();

static async Task PrintAsync()
{
    for (int i = 0;; i++)
    {
        await MyTask.Delay(1000);
        Console.WriteLine(i);
    }
}