using Helper;
namespace Rush;

public static class Rush
{
    public static async Task Start(Helper.API api)
    {
        var semaphore = new SemaphoreSlim(Config.Concurrency, Config.Concurrency);
        var taskList = new List<Task>();
    
        foreach (var task in RushTask.Tasks)
        {
            var taskCopy = task;
            var taskInstance = Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var ClassType = taskCopy.ClassType;
                    var CourseID = taskCopy.CourseID;
                    var TeacherIndex = taskCopy.TeacherIndex;
                    var TeachingClassID = CourseID + "-" + TeacherIndex;
                    while (true)
                    {
                        try
                        {
                            var result = await api.AddCourse(ClassType, CourseID, TeacherIndex);
                            if (result == 1)
                            {
                                Log.Logger.Information("{Time} 课程 {CourseID} 抢课成功！", 
                                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), TeachingClassID);
                                break;
                            }
                            if (result == -1)
                            {
                                Log.Logger.Information("{Time} 课程 {CourseID} 抢课失败！", 
                                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), TeachingClassID);
                                break;
                            }
                            await Task.Delay(TimeSpan.FromSeconds(RushTask.RetryTimeOut));
                        }
                        catch (Exception ex)
                        {
                            Log.Logger.Error(ex, "执行任务时发生错误");
                            await Task.Delay(TimeSpan.FromSeconds(RushTask.RetryTimeOut));
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });
            taskList.Add(taskInstance);
        }
        await Task.WhenAll(taskList);
    }
}