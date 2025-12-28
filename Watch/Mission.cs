using Helper;
using SoftCircuits.IniFileParser;
namespace Watch;

public static class WatchTask
{
    public static double RetryTimeOut { get; set; } = 60;
    public static List<TaskConfig> Tasks { get; private set; } = new();

    public class TaskConfig
    {
        public bool Delete { get; set; } = false;
        public string OriginCourseID { get; set; } = string.Empty;
        public string OriginTeacherIndex { get; set; } = string.Empty;
        public string NewClassType { get; set; } = string.Empty;
        public string NewCourseID { get; set; } = string.Empty;
        public string NewTeacherIndex { get; set; } = string.Empty;
    }

    static WatchTask()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.ini");
        var iniFile = new IniFile(StringComparer.OrdinalIgnoreCase);
        iniFile.Load(configPath);
        
        RetryTimeOut = iniFile.GetSetting("Config", "Watch_RetryTimeOut", 60);
        
        Tasks.Clear();
        foreach (var sectionName in iniFile.GetSections())
        {
            if (sectionName.StartsWith("Task", StringComparison.OrdinalIgnoreCase))
            {
                var Mode = (iniFile.GetSetting(sectionName, "Mode", "")?? string.Empty).Trim().Trim('"');
                if (Mode == "Watch")
                {
                    var Delete = iniFile.GetSetting(sectionName, "Delete", false);
                    var OriginCourseID = (iniFile.GetSetting(sectionName, "Origin_CourseID", "")?? string.Empty).Trim().Trim('"');
                    var OriginTeacherIndex = (iniFile.GetSetting(sectionName, "Origin_TeacherIndex", "")?? string.Empty).Trim().Trim('"');
                    var NewClassType = (iniFile.GetSetting(sectionName, "New_ClassType", "")?? string.Empty).Trim().Trim('"');
                    var NewCourseID = (iniFile.GetSetting(sectionName, "New_CourseID", "")?? string.Empty).Trim().Trim('"');
                    var NewTeacherIndex = (iniFile.GetSetting(sectionName, "New_TeacherIndex", "")?? string.Empty).Trim().Trim('"');

                    if (!string.IsNullOrEmpty(NewClassType) && !string.IsNullOrEmpty(NewCourseID) && !string.IsNullOrEmpty(NewTeacherIndex))
                    {
                        Tasks.Add(new TaskConfig
                        {
                            Delete = Delete,
                            OriginCourseID = OriginCourseID,
                            OriginTeacherIndex = OriginTeacherIndex,
                            NewClassType = NewClassType,
                            NewCourseID = NewCourseID,
                            NewTeacherIndex = NewTeacherIndex
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