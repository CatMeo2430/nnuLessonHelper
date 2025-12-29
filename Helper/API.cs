using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Helper;

public sealed class API
{
    private readonly Requests _req;

    public API(Requests req)
    {
        _req = req ?? throw new ArgumentNullException(nameof(req));
        _req.ChangeHeaders(new Dictionary<string, string>
        {
            ["Host"] = "xsxk.nnu.edu.cn",
            ["Origin"] = "https://xsxk.nnu.edu.cn",
            ["Referer"] =
                $"https://xsxk.nnu.edu.cn/xsxkapp/sys/xsxkapp/*default/grablessons.do?token={User.AuthToken!}",
            ["User-Agent"] =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36",
            ["Accept"] = "application/json, text/javascript, */*; q=0.01",
            ["Accept-Encoding"] = "gzip, deflate, br, zstd",
            ["Accept-Language"] = "zh-CN,zh;q=0.9",
            ["Connection"] = "keep-alive",
            ["Content-Type"] = "application/x-www-form-urlencoded; charset=UTF-8",
            ["sec-ch-ua"] = "\"Not)A;Brand\";v=\"8\", \"Chromium\";v=\"138\", \"Google Chrome\";v=\"138\"",
            ["sec-ch-ua-platform"] = "\"Windows\"",
            ["sec-ch-ua-mobile"] = "?0",
            ["Sec-Fetch-Site"] = "same-origin",
            ["Sec-Fetch-Mode"] = "cors",
            ["Sec-Fetch-Dest"] = "empty",
            ["X-Requested-With"] = "XMLHttpRequest",
            ["token"] = User.AuthToken!
        });
    }

    public async Task<int> AddCourse(
        string ClassType,
        string CourseID,
        string TeacherIndex,
        string? TestID = ""
    )
    {
        var teachingClassType = ClassType switch
        {
            "系统推荐" or "Recommended" => "TJKC",
            "博雅" or "Public" => "XGXK",
            "实验课" or "Test" => "TJKC",
            "跨年级" or "CrossGrade" => "FANKC",
            "跨专业" or "InterProfessional" => "FAWKC",
            "重修" or "Retake" => "CXKC",
            "体育" or "Sport" => "TYKC",
            "辅修" or "Minor" => "FXKC",
            _ => null
        };
        if (string.IsNullOrEmpty(teachingClassType))
        {
            Log.Logger.Fatal("未知的课程类型");
            return -1;
        }
        var TeachingClassID = User.TermCode+CourseID+TeacherIndex;
        
        const string url = "https://xsxk.nnu.edu.cn/xsxkapp/sys/xsxkapp/elective/volunteer.do";
        var data = new
        {
            data = new
            {
                operationType = "1",
                studentCode = User.StudentID,
                electiveBatchCode = User.BatchCode,
                teachingClassId = TeachingClassID,
                isMajor = "1",
                campus = "2",
                teachingClassType,
                testTeachingClassID = TestID
            }
        };
        var payload = new Dictionary<string, object>
        {
            ["addParam"] = JsonConvert.SerializeObject(data)
        };
        var response = await _req.Send("POST", url, payload);
        if (response is null) return 0;

        try
        {
            var json = JObject.Parse(response);
            var code = json.SelectToken("$.code")?.Value<string>();
            var msg = json.SelectToken("$.msg")?.Value<string>();
            if (code != "1")
            {
                switch (msg)
                {
                    case "教学班不在开放选课轮次中":
                        Log.Logger.Warning("选课：{CourseID}还未开抢，等待开放", CourseID, msg);
                        return 0;
                    case "该课程超过课容量":
                        Log.Logger.Error("选课：{CourseID}获取失败，已无名额", CourseID, msg);
                        return -1;
                    case "该课程已经存在选课结果中":
                        Log.Logger.Warning("选课：{CourseID}已抢成功，重复添加", CourseID, msg);
                        return 1;
                    case "上次提交的选课还没有处理完":
                        Log.Logger.Warning("选课：{CourseID}并发错误，即将重试", CourseID, msg);
                        return 0;
                    case "超过[博雅课程限选3门]的门数或学分限制,要求最多选择3门课,最高选择7学分":
                        Log.Logger.Error("选课：{CourseID}触发限制，博雅课最多3门且7学分", CourseID, msg);
                        return -1;
                    default:
                        Log.Logger.Error("选课：{CourseID},失败：{msg}", CourseID, msg);
                        return 0;
                }
            }

            Log.Logger.Information("添加选课：{CourseID},成功", CourseID);
            return 1;
        }
        catch (JsonException ex)
        {
            Log.Logger.Fatal(ex, "添加选课响应格式变更");
            return -1;
        }
    }

    public async Task<int> DelCourse(string CourseID,string TeacherIndex)
    {
        var TeachingClassID = User.TermCode+CourseID+TeacherIndex;
        const string url = "https://xsxk.nnu.edu.cn/xsxkapp/sys/xsxkapp/elective/deleteVolunteer.do";
        var data = new
        {
            data = new
            {
                operationType = "2",
                studentCode = User.StudentID!,
                electiveBatchCode = User.BatchCode!,
                teachingClassId = TeachingClassID,
                isMajor = "1"
            }
        };
        var payload = new Dictionary<string, object>
        {
            ["deleteParam"] = JsonConvert.SerializeObject(data, Formatting.None)
        };
        var response = await _req.Send("GET", url, payload);
        if (response is null) return 0;
    
        try
        {
            var json = JObject.Parse(response);
            var code = json.SelectToken("$.code")?.Value<string>();
            var msg = json.SelectToken("$.msg")?.Value<string>();
            if (code != "1")
            {
                switch (msg)
                {
                    case "教学班不在开放选课轮次中":
                        Log.Logger.Warning("退课：{CourseID}还未能推，等待开放", CourseID, msg);
                        return 0;
                    case "选课结果中找不到该教学班":
                        Log.Logger.Warning("退课：{CourseID}已退成功，重复添加", CourseID, msg);
                        return 1;
                    case "上次提交的选课还没有处理完":
                        Log.Logger.Warning("退课：{CourseID}并发错误，即将重试", CourseID, msg);
                        return 0;
                    default:
                        Log.Logger.Error("退课：{CourseID},失败：{msg}", CourseID, msg);
                        return 0;
                }
            }
            Log.Logger.Information("退课：{CourseID},成功", CourseID);
            return 1;
        }
        catch (JsonException ex)
        {
            Log.Logger.Fatal(ex, "删除志愿响应格式变更");
            return 0;
        }
    }
    public async Task<bool> isAvaliable(string CourseID,string TeacherIndex)
    {
        var TeachingClassID = User.TermCode+CourseID+TeacherIndex;
        const string url = "https://xsxk.nnu.edu.cn/xsxkapp/sys/xsxkapp/elective/teachingclass/capacity.do";
        var payload = new Dictionary<string, object>
        {
            ["teachingClassId"] = TeachingClassID,
            ["xh"] = User.StudentID,
            ["capacitySuffix"] = ""
        };

        var response = await _req.Send("GET", url, payload);
        if (response is null) return false;

        try
        {
            var json = JObject.Parse(response);

            var code = json.SelectToken("$.code")?.Value<string>();
            var msg = json.SelectToken("$.msg")?.Value<string>();
            if (code != "1")
            {
                Log.Logger.Error("获取单课信息失败：{msg}", msg);
                return false;
            }

            var capacity = json.SelectToken("$.data.classCapacity")?.Value<int>();
            var selected = json.SelectToken("$.data.numberOfSelected")?.Value<int>();
            return capacity > selected;
        }
        catch (JsonException ex)
        {
            Log.Logger.Fatal(ex, "单课信息响应格式变更");
            return false;
        }
    }
}