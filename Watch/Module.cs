using Helper;
namespace Watch;

public static class Watch
{
    public static async Task Start(Helper.API api)
    {
        var semaphore = new SemaphoreSlim(Config.Concurrency, Config.Concurrency);
        var taskList = new List<Task>();
    
        foreach (var task in WatchTask.Tasks)
        {
            var taskCopy = task;
            var taskInstance = Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var Delete = taskCopy.Delete;
                    var OriginCourseID = taskCopy.OriginCourseID;
                    var OriginTeacherIndex = taskCopy.OriginTeacherIndex;
                    var OriginTeachingClassID = OriginCourseID + "-" + OriginTeacherIndex;
                    
                    var NewClassType = taskCopy.NewClassType;
                    var NewCourseID = taskCopy.NewCourseID;
                    var NewTeacherIndex = taskCopy.NewTeacherIndex;
                    var NewTeachingClassID = NewCourseID + "-" + NewTeacherIndex;
                    while (true)
                    {
                        try
                        {
                            var result = await api.isAvaliable(NewCourseID,NewTeacherIndex);
                            if (result)
                            {
                                Log.Logger.Information("{Time} 检测到课程 {NewTeachingClassID} 释放名额！", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), NewTeachingClassID);
                            }
                            else
                            {
                                Log.Logger.Information("{Time} 课程 {NewTeachingClassID} 名额已满！", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), NewTeachingClassID);
                                await Task.Delay(TimeSpan.FromSeconds(WatchTask.RetryTimeOut));
                                continue;
                            }

                            if (Delete)
                            {
                                var delResult = await api.DelCourse(OriginCourseID, OriginTeacherIndex);
                                if (delResult == 1)
                                {
                                    Log.Logger.Information("{Time} 课程 {OriginTeachingClassID} 退选成功！", 
                                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), OriginTeachingClassID);
                                }
                                else
                                {
                                    Log.Logger.Error("{Time} 课程 {OriginTeachingClassID} 退选失败！", 
                                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), OriginTeachingClassID);
                                    continue;
                                }
                            }
                            
                            var addResult = await api.AddCourse(NewClassType, NewCourseID, NewTeacherIndex);
                            if (addResult == 1)
                            {
                                Log.Logger.Information("{Time} 课程 {NewTeachingClassID} 抢课成功！", 
                                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), NewTeachingClassID);
                                break;
                            }
                            if (addResult == -1)
                            {
                                Log.Logger.Information("{Time} 课程 {NewTeachingClassID} 抢课失败！", 
                                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), NewTeachingClassID);
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Logger.Error(ex, "执行任务时发生错误");
                            await Task.Delay(TimeSpan.FromSeconds(WatchTask.RetryTimeOut));
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
