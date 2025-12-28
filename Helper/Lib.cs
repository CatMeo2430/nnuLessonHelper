using Flurl.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SoftCircuits.IniFileParser;

namespace Helper;

public static class Log
{
    private static readonly string LogFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "Logs",
        $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");

    public static readonly ILogger Logger = new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .WriteTo.Console(outputTemplate:
            "[等级：{Level:u3}][时间：{Timestamp:yyyy-MM-dd HH:mm:ss}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(LogFilePath,
            rollingInterval: RollingInterval.Infinite,
            rollOnFileSizeLimit: false,
            shared: true,
            fileSizeLimitBytes: null,
            buffered: false,
            outputTemplate: "[等级：{Level:u3}][时间：{Timestamp:yyyy-MM-dd HH:mm:ss}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();
}

public static class Config
{
    public static bool isEncrypt { get; set; } = false;
    public static double SendTimeOut { get; set; } = 5.0;
    public static int Concurrency { get; set; } = 10;

    private static readonly IniFile _iniFile;
    private static readonly string _configPath;
    
    static Config()
    {
        try
        {
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.ini");
            _iniFile = new IniFile(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(_configPath))
            {
                _iniFile.Load(_configPath);

                isEncrypt = _iniFile.GetSetting("Auth", "isEncrypt", false);
                User.StudentID = _iniFile.GetSetting("Auth", "Username", "") ?? string.Empty;
                User.Password = _iniFile.GetSetting("Auth", "Password", "") ?? string.Empty;
                SendTimeOut = _iniFile.GetSetting("Config", "SendTimeOut", 5.0);
                Concurrency = _iniFile.GetSetting("Config", "Concurrency", 10);
                Log.Logger.Information("配置文件加载成功");
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "配置文件加载失败: {Message}", ex.Message);
            Environment.Exit(1);
        }
    }
}

public sealed class Requests
{
    public FlurlClient Session { get; }
    private CookieJar _cookieJar;
    
    public Requests()
    {
        _cookieJar = new CookieJar();
        Session = new FlurlClient()
            .WithTimeout(TimeSpan.FromSeconds(Config.SendTimeOut));
    }
    
    public async Task<string?> Send(
            string method,
            string url,
            IDictionary<string, object>? payload = null,
            Action<IFlurlRequest>? modify = null)
    {
        payload ??= new Dictionary<string, object>();

        try
        {
            var request = Session.Request(url).WithCookies(_cookieJar);
            modify?.Invoke(request);

            var response = method.ToUpperInvariant() switch
            {
                "GET" => await request.SetQueryParams(payload).GetAsync(),
                "POST" => await request.PostUrlEncodedAsync(payload),
                _ => throw new ArgumentOutOfRangeException(nameof(method))
            };

            if (response.StatusCode != 200)
            {
                Log.Logger.Error("[ERROR] HTTP错误：{StatusCode} {Url}", response.StatusCode, url);
                return null;
            }

            var body = await response.GetStringAsync();
            if (body.Length > 1024 * 1024)
                Log.Logger.Warning("[WARN] 响应体过大");

            return body;
        }
        catch (FlurlHttpException ex)
        {
            Log.Logger.Error(ex, "网络异常：{Message}", ex.Message);
            return null;
        }
        catch (JsonException jex)
        {
            Log.Logger.Error(jex, "JSON 解析失败：{Message}", jex.Message);
            return null;
        }
        catch (TaskCanceledException tcex)
        {
            Log.Logger.Error(tcex, "请求超时：{Message}", tcex.Message);
            return null;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "未知错误：{Message}", ex.Message);
            return null;
        }
    }

    public void ChangeHeaders(IDictionary<string, string> newHeaders)
    {
        Session.Headers.Clear();
        foreach (var kv in newHeaders)
            Session.Headers.Add(kv.Key, kv.Value);
    }
}

public static class OCR
{
    public static string? Get(byte[] data)
    {
        try
        {
            const string url = "http://183.66.27.14:50233/ocr";
            string json = url
                .PostJsonAsync(new { image = Convert.ToBase64String(data) }).Result
                .GetStringAsync().Result;
            string code = JObject.Parse(json)?["data"]?["text"]?.Value<string>() ?? string.Empty;
            code = code.ToUpper();
            if (code.Length == 4 && code.All(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
            {
                Log.Logger.Debug("OCR识别结果: {Code}", code);
                return code;
            }
            else
            {
                Log.Logger.Error("OCR识别失败,验证码格式错误");
                return null;
            }
        }
        catch (FlurlHttpException ex)
        {
            Log.Logger.Error(ex, "OCR识别失败,请检查网络");
            return null;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "OCR识别失败,响应解析错误");
            return null;
        }
    }
}