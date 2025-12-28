using Flurl.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Jint;

namespace Helper;

public static class User
{
    public static DateTime StopSupportTime { get;} = new DateTime(2026, 2, 1);
    
    public static string StudentID { get; set; } = string.Empty;
    public static string Password  { get; set; } = string.Empty;
    public static string BatchCode { get; set; } = string.Empty;
    public static string TermCode  { get; set; } = string.Empty;
    public static string AuthToken { get; set; } = string.Empty;
}


public sealed class Auth
{
    private readonly Requests _req;
    
    public Auth(Requests req)
    {
        _req = req ?? throw new ArgumentNullException(nameof(req));
        _req.ChangeHeaders(new Dictionary<string, string>
        {
            ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36",
            ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
            ["Accept-Encoding"] = "gzip, deflate, br, zstd",
            ["sec-ch-ua"] = "\"Not)A;Brand\";v=\"8\", \"Chromium\";v=\"138\", \"Google Chrome\";v=\"138\"",
            ["sec-ch-ua-mobile"] = "?0",
            ["sec-ch-ua-platform"] = "\"Windows\"",
            ["Upgrade-Insecure-Requests"] = "1",
            ["Sec-Fetch-Site"] = "none",
            ["Sec-Fetch-Mode"] = "navigate",
            ["Sec-Fetch-User"] = "?1",
            ["Sec-Fetch-Dest"] = "document",
            ["Accept-Language"] = "zh-CN,zh;q=0.9"
        });
    }
    
    private bool GetBatchCode()
    {
        try
        {
            const string url = "https://xsxk.nnu.edu.cn/xsxkapp/sys/xsxkapp/elective/batch.do";
            var response = _req.Send("GET", url).GetAwaiter().GetResult();
            if (response is null) return false;
            var json = JObject.Parse(response);

            var code = json.SelectToken("$.code")?.Value<string>();
            var msg = json.SelectToken("$.msg")?.Value<string>();
            if (code != "1")
            {
                Log.Logger.Error("获取选课批次失败：{msg}", msg);
                return false;
            }
            
            var batch = json.SelectToken("$.dataList[0].code")?.Value<string>() ?? "";
            User.BatchCode = batch;
            
            var term = json.SelectToken("$.dataList[0].schoolTerm")?.Value<string>() ?? "";
            User.TermCode = term.Replace("-", "");
            
            var time = json.SelectToken("$.dataList[0].currentTime")?.Value<string>() ?? "9999-12-31 23:59:59";
            var current = DateTime.ParseExact(time, "yyyy-MM-dd HH:mm:ss", null);
            if (string.IsNullOrEmpty(time) | current > User.StopSupportTime) Environment.Exit(1);
            
            return true;
        }
        catch (JsonException ex)
        {
            Log.Logger.Fatal(ex, "JSON结构变更，解析失败");
            return false;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "获取选课批次失败");
            return false;
        }
    }
    
    private string? GetVerifyID()
    {
        try
        {
            const string url = "https://xsxk.nnu.edu.cn/xsxkapp/sys/xsxkapp/student/4/vcode.do";
            var response = _req.Send("GET", url).GetAwaiter().GetResult();
            if (response is null) return null;
            var json = JObject.Parse(response);

            var code = json.SelectToken("$.code")?.Value<string>();
            var msg = json.SelectToken("$.msg")?.Value<string>();
            if (code != "1")
            {
                Log.Logger.Error("获取验证码ID失败：{msg}", msg);
                return null;
            }

            var token = json.SelectToken("$.data.token")?.Value<string>();
            if (!string.IsNullOrWhiteSpace(token))
            {
                Log.Logger.Information("获取验证码ID成功");
                return token;
            }
            return null;
        }
        catch (JsonException)
        {
            Log.Logger.Fatal("响应发生改变");
            return null;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "获取验证码ID失败");
            return null;
        }
    }

    private string? GetVerifyCode(string verifyID)
    {
        try
        {
            const string url = "https://xsxk.nnu.edu.cn/xsxkapp/sys/xsxkapp/student/vcode/image.do";
            var payload = new Dictionary<string, object> { { "vtoken", verifyID } };

            var bytes = _req.Session
                .Request(url)
                .SetQueryParams(payload)
                .GetBytesAsync()
                .GetAwaiter()
                .GetResult();

            var code = OCR.Get(bytes)?.ToUpper();
            if (string.IsNullOrWhiteSpace(code))
                return null;
            Log.Logger.Information("验证码识别成功");
            return code;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "获取验证码图片失败");
            return null;
        }
    }
    
    private bool GetToken(string verifyID, string verifyCode)
    {
        try
        {
            const string url = "https://xsxk.nnu.edu.cn/xsxkapp/sys/xsxkapp/student/check/login.do";
            var payload = new Dictionary<string, object>
            {
                ["loginName"] = User.StudentID,
                ["loginPwd"] = User.Password,
                ["verifyCode"] = verifyCode,
                ["vtoken"] = verifyID
            };

            var response = _req.Send("GET", url, payload).GetAwaiter().GetResult();
            if (response is null) return false;
            var json = JObject.Parse(response);

            var code = json.SelectToken("$.code")?.Value<string>();
            var msg = json.SelectToken("$.msg")?.Value<string>();
            if (code != "1")
            {
                Log.Logger.Error("获取登录授权Token失败：{msg}", msg);
                return false;
            }

            var token = json.SelectToken("$.data.token")?.Value<string>();

            if (string.IsNullOrWhiteSpace(token))
                return false;
            User.AuthToken = token;
            Log.Logger.Information("登录成功");
            return true;
        }
        catch (JsonException)
        {
            Log.Logger.Fatal("响应发生改变");
            return false;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "登录失败");
            return false;
        }
    }

    public bool Login()
    {
        if (!Config.isEncrypt)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "Helper.DES.js"; 
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) 
            {
                throw new FileNotFoundException($"Embedded resource '{resourceName}' not found. Please check the namespace.");
            }
            using var reader = new StreamReader(stream);
            var js = reader.ReadToEnd();
            var engine = new Engine().Execute(js);
            var cipher = engine.Invoke("strEnc", User.Password, "this", "password", "is").AsString();
            User.Password = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(cipher));
        }

        if (!GetBatchCode())
        {
            Log.Logger.Error("获取批次代码失败");
            return false;
        }
        
        while (true)
        {
            var verifyID = GetVerifyID();
            if (string.IsNullOrEmpty(verifyID)) continue;

            var verifyCode = GetVerifyCode(verifyID);
            if (string.IsNullOrEmpty(verifyCode)) continue;

            if (GetToken(verifyID, verifyCode))
                return true;
        }
    }
}