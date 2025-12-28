using Helper;
using SoftCircuits.IniFileParser;
namespace Rush;

public static class RushTask
{
    public static double RetryTimeOut { get; set; } = 0.5;
    public static List<TaskConfig> Tasks { get; private set; } = new();

    public class TaskConfig
    {
        public string ClassType { get; set; } = string.Empty;
        public string CourseID { get; set; } = string.Empty;
        public string TeacherIndex { get; set; } = string.Empty;
    }

    static RushTask()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.ini");
        var iniFile = new IniFile(StringComparer.OrdinalIgnoreCase);
        iniFile.Load(configPath);
        
        RetryTimeOut = iniFile.GetSetting("Config", "Rush_RetryTimeOut", 0.5);
        
        Tasks.Clear();
        foreach (var sectionName in iniFile.GetSections())
        {
            if (sectionName.StartsWith("Task", StringComparison.OrdinalIgnoreCase))
            {
                var Mode = (iniFile.GetSetting(sectionName, "Mode", "")?? string.Empty).Trim().Trim('"');
                if (Mode == "Rush")
                {
                    var ClassType = (iniFile.GetSetting(sectionName, "ClassType", "")?? string.Empty).Trim().Trim('"');
                    var CourseID = (iniFile.GetSetting(sectionName, "CourseID", "")?? string.Empty).Trim().Trim('"');
                    var TeacherIndex = (iniFile.GetSetting(sectionName, "TeacherIndex", "")?? string.Empty).Trim().Trim('"');

                    if (!string.IsNullOrEmpty(ClassType) && !string.IsNullOrEmpty(CourseID) && !string.IsNullOrEmpty(TeacherIndex))
                    {
                        Tasks.Add(new TaskConfig
                        {
                            ClassType = ClassType,
                            CourseID = CourseID,
                            TeacherIndex = TeacherIndex
                        });
                    }
                }
                else
                {
                    Log.Logger.Warning("不匹配的任务：{sectionName}，未载入",sectionName);
                }
            }
        }
        Log.Logger.Information("已加载 {Count} 个任务", Tasks.Count);
    }
}