using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

//Const Var
const string Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9";
const string UA_PC = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36";
const string BaseURL = "http://home.yngqt.org.cn";
const string JsonName = "daxuexi_config.json";

T? GetJsonObject<T>(string jsonstring)
{
    using MemoryStream ms = new(Encoding.Unicode.GetBytes(jsonstring));
    return (T?)new DataContractJsonSerializer(typeof(T)).ReadObject(ms);
}
UserList? LoadFromJson()
{
    var Md5 = (string txt) =>
    {
        byte[] result = MD5.HashData(Encoding.UTF8.GetBytes(txt));
        StringBuilder strbul = new(40);
        for (int i = 0; i < result.Length; i++)
        {
            strbul.Append(result[i].ToString("x2"));
        }
        return strbul.ToString();
    };
    string path = $"./{JsonName}";
    string json;
    bool init = false;
    if (File.Exists(path))
    {
        using StreamReader sr = new(path);
        json = sr.ReadToEnd();
    }
    else
    {
        Console.WriteLine("首次运行...\n输入账号密码，使用  ;  分割账号密码，使用#分割不同账号");
        string? readContent = Console.ReadLine() ?? throw new Exception("输入为空！");
        string[] input = readContent.Split("#");
        json = "{\"Users\":[";
        foreach (string line in input)
        {
            string[] kv = line.Split(";");
            json += "{\"txtusername\":\"" + kv[0] + "\",\"txtpassword\":\"" + Md5(kv[1]) + "\"},";
        }
        json = json[..^1];
        json += "]}";
        init = true;
    }
    UserList? model = GetJsonObject<UserList>(json);
    if (init)
        SaveToJson(model);
    return model;
}
string GetJsonString<T>(T jsonobject)
{
    DataContractJsonSerializer js = new(typeof(T));
    using MemoryStream msObj = new();
    js.WriteObject(msObj, jsonobject);
    msObj.Position = 0;
    using StreamReader sr = new(msObj, Encoding.UTF8);
    return sr.ReadToEnd();
}
void SaveToJson(UserList? lst)
{
    using StreamWriter sw = new($"./{JsonName}");
    sw.Write(GetJsonString(lst));
}
Cookie? Login(User user)
{
    CookieContainer cookieContainer = new();
    HttpClientHandler handler = new()
    {
        CookieContainer = cookieContainer,
        AllowAutoRedirect = true
    };
    using HttpClient client = new(handler);
    client.DefaultRequestHeaders.Add("Accept", Accept);
    client.DefaultRequestHeaders.Add("user-agent", UA_PC);
    StringContent content = new(GetJsonString(user), Encoding.UTF8, "application/json");
    Uri uri = new($"{BaseURL}/qndxx/login.ashx");
    HttpResponseMessage respond = client.PostAsync(uri, content).Result;
    if (respond.IsSuccessStatusCode)
    {
        string a = respond.Content.ReadAsStringAsync().Result;
        List<Cookie> cookies = cookieContainer.GetCookies(uri).Cast<Cookie>().ToList();
        return (cookies.Count > 0) ? cookies[0] : null;
    }
    else
        throw (new Exception("登录请求异常！"));
}
void ExcutePost(string url, Cookie cookie, string? content, string header)
{
    CookieContainer cookieContainer = new();
    cookieContainer.Add(cookie);
    HttpClientHandler handler = new()
    {
        CookieContainer = cookieContainer,
        AllowAutoRedirect = true
    };
    using HttpClient client = new(handler);
    client.DefaultRequestHeaders.Add("Accept", Accept);
    client.DefaultRequestHeaders.Add("user-agent", UA_PC);
    Console.WriteLine($"{header}: {client.PostAsync(url,
        content == null ? null : new StringContent(content, Encoding.UTF8, "application/json"))
        .Result.Content.ReadAsStringAsync().Result}");
}

string szStudyId;
Console.WriteLine("正在抓取最新的序号.......");
using HttpClient httpClient = new();
httpClient.DefaultRequestHeaders.Add("user-agent", "MicroMessenger");
HttpResponseMessage httpResponseMessage = httpClient.GetAsync($"{BaseURL}/qndxx/default.aspx").Result;
if (httpResponseMessage.IsSuccessStatusCode)
{
    szStudyId = Regex.Replace(Regex.Matches(httpResponseMessage.Content.ReadAsStringAsync().Result, @"[s][t][u][d][y][(].*?[)]+")[0].Value, @"[^0-9]+", "");
    Console.WriteLine("成功！当前最新序号: {0}", szStudyId);
}
else
    throw new Exception("无法获取到序号！");
UserList pList = LoadFromJson() ?? throw new Exception("UserList为空！");
List<User> userlist = pList.Users ?? throw new Exception("User为空！");
foreach (User user in userlist)
{
    Cookie usrCookie = Login(user) ?? throw (new Exception("无法获取cookie")); ;
    Console.WriteLine("登录成功!: " + usrCookie.Value);
    ExcutePost($"{BaseURL}/qndxx/user/qiandao.ashx", usrCookie, null, "签到");
    ExcutePost($"{BaseURL}/qndxx/xuexi.ashx", usrCookie, $"{{txtid:{szStudyId}}}", "学习");
}
SaveToJson(pList);

[DataContract]
public class User
{
    [DataMember]
    public string? txtusername { get; set; }
    [DataMember]
    public string? txtpassword { get; set; }
}
[DataContract]
public class UserList
{
    [DataMember]
    public List<User>? Users { get; set; }
}