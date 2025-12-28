using Helper;
namespace Watch;

public static class Program
{
    public static async Task Main()
    {
        Log.Logger.Information("开始运行");
        try
        {
            var requests = new Requests();
            var auth = new Auth(requests);
            if (!auth.Login()) 
            {
                Log.Logger.Error("登录失败");
                return;
            }
            
            var api = new API(requests);
            await Watch.Start(api);
            Log.Logger.Information("所有任务执行完成");
        }
        catch (Exception ex) 
        {
            Log.Logger.Error(ex, "程序执行过程中发生错误");
        }
        finally
        {
            Console.WriteLine("\n"+"Watch监测程序所有任务已经执行完成，请按任意键退出并关闭窗口");
            Console.ReadKey();
        }
    }
}