using dotenv.net;
using LoginZju;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Load environment variables from .env file (copy .env.template to .env and fill in your credentials).
DotEnv.Load();

static bool IsEnabled(string key) =>
    string.Equals(Environment.GetEnvironmentVariable(key), "true", StringComparison.OrdinalIgnoreCase);

static string Env(string key) => Environment.GetEnvironmentVariable(key)
    ?? throw new InvalidOperationException($"Missing environment variable: {key}");

// Any ZJUAM-based test enabled?
var anyZjuamTest = IsEnabled("TEST_ZJUAM") || IsEnabled("TEST_ZDBK") || IsEnabled("TEST_COURSES")
    || IsEnabled("TEST_CLASSROOM") || IsEnabled("TEST_FORM") || IsEnabled("TEST_YQFKGL")
    || IsEnabled("TEST_OPEN") || IsEnabled("TEST_ETA");

// ============================================================
// Set up DI container with factory pattern
// ============================================================

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

var cc98ClientId = Environment.GetEnvironmentVariable("CC98_CLIENT_ID");
services.AddLoginZju(cc98ClientId is null
    ? null
    : options =>
    {
        options.ClientId = cc98ClientId;
        options.ClientSecret = Env("CC98_CLIENT_SECRET");
    });

await using var provider = services.BuildServiceProvider();
var factory = provider.GetRequiredService<ILoginZjuFactory>();

// Share ONE auth instance across all ZJUAM-based tests (just like the original TypeScript
// shares one ZJUAM instance). Each service still gets its own cookie jar / session.
using var auth = anyZjuamTest
    ? factory.CreateAuth(Env("ZJUAM_USERNAME"), Env("ZJUAM_PASSWORD"))
    : null;

// ============================================================
// ZJUAM — core authentication test
// ============================================================

if (IsEnabled("TEST_ZJUAM"))
{
    Console.WriteLine("=== ZJUAM ===");
    await auth!.LoginAsync();
    Console.WriteLine("ZJUAM login OK");

    var serviceCallbackUrl = await auth.LoginServiceAsync("https://service.zju.edu.cn/");
    var callbackResponse = await auth.FetchAsync(new HttpRequestMessage(HttpMethod.Get, serviceCallbackUrl));
    Console.WriteLine($"service.zju.edu.cn CAS callback: {callbackResponse.StatusCode}");

    var loginInfoResponse = await auth.FetchAsync(new HttpRequestMessage(
        HttpMethod.Get,
        "https://service.zju.edu.cn/_web/portal/api/user/loginInfo.rst?_p=YXM9MiZ0PTUmZD0xMzMmcD0xJmY9MjImbT1OJg__"));
    var loginInfoBody = await loginInfoResponse.Content.ReadAsStringAsync();
    Console.WriteLine($"service.zju.edu.cn loginInfo response: {loginInfoResponse.StatusCode}, length={loginInfoBody.Length}");
    Console.WriteLine(loginInfoBody);
}

// ============================================================
// ZDBK — 本科教学管理信息服务平台
// ============================================================

if (IsEnabled("TEST_ZDBK"))
{
    Console.WriteLine("\n=== ZDBK ===");
    using var zdbk = factory.CreateZdbk(auth!);

    // Query class schedule (课表查询)
    var su = Env("ZJUAM_USERNAME");
    var zdbkRequest = new HttpRequestMessage(
        HttpMethod.Post,
        $"https://zdbk.zju.edu.cn/jwglxt/kbcx/xskbcx_cxXsKb.html?gnmkdm=N253508&su={su}")
    {
        Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["xnm"] = "2024-2025",
            ["xqm"] = "2|春、夏",
            ["xqmmc"] = "春、夏",
        })
    };
    zdbkRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");
    var response = await zdbk.FetchAsync(zdbkRequest);
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"ZDBK response: {response.StatusCode}, length={body.Length}");
    Console.WriteLine(body);
}

// ============================================================
// Courses — 学在浙大
// ============================================================

if (IsEnabled("TEST_COURSES"))
{
    Console.WriteLine("\n=== Courses ===");
    using var courses = factory.CreateCourses(auth!);

    // Query pending todos
    var response = await courses.FetchAsync(
        new HttpRequestMessage(HttpMethod.Get, "https://courses.zju.edu.cn/api/todos?no-intercept=true"));
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Courses response: {response.StatusCode}, length={body.Length}");
    Console.WriteLine(body);
}

// ============================================================
// Classroom — 智云课堂
// ============================================================

if (IsEnabled("TEST_CLASSROOM"))
{
    Console.WriteLine("\n=== Classroom ===");
    using var classroom = factory.CreateClassroom(auth!);
    var response = await classroom.FetchAsync(new HttpRequestMessage(HttpMethod.Get, "https://classroom.zju.edu.cn/"));
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Classroom response: {response.StatusCode}, length={body.Length}");
    Console.WriteLine(body);
}

// ============================================================
// Form — 表单填报助手
// ============================================================

if (IsEnabled("TEST_FORM"))
{
    Console.WriteLine("\n=== Form ===");
    using var form = factory.CreateForm(auth!);
    var response = await form.FetchAsync(
        new HttpRequestMessage(HttpMethod.Get, "https://form.zju.edu.cn/dfi/formSetting/queryListPage?pageNo=1&pageSize=10"));
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Form response: {response.StatusCode}, length={body.Length}");
    Console.WriteLine(body);
}

// ============================================================
// Yqfkgl — 校园卡二维码页面
// ============================================================

if (IsEnabled("TEST_YQFKGL"))
{
    Console.WriteLine("\n=== Yqfkgl ===");
    using var yqfkgl = factory.CreateYqfkgl(auth!);
    var response = await yqfkgl.FetchAsync(
        new HttpRequestMessage(HttpMethod.Get, "https://yqfkgl.zju.edu.cn/_web/_customizes/ykt/index3.jsp"));
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Yqfkgl response: {response.StatusCode}, length={body.Length}");
    Console.WriteLine(body);
}

// ============================================================
// Open — 浙大先生开放平台 (HiAgent)
// ============================================================

if (IsEnabled("TEST_OPEN"))
{
    Console.WriteLine("\n=== Open ===");
    using var open = factory.CreateOpen(auth!);
    var response = await open.FetchAsync(new HttpRequestMessage(HttpMethod.Get, "https://open.zju.edu.cn/api/user/info"));
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Open response: {response.StatusCode}, length={body.Length}");
    Console.WriteLine(body);
}

// ============================================================
// ETA — 三全育人平台 (+ encryption helpers)
// ============================================================

if (IsEnabled("TEST_ETA"))
{
    Console.WriteLine("\n=== ETA ===");
    using var eta = factory.CreateEta(auth!);
    await eta.LoginAsync();
    Console.WriteLine("ETA login OK");

    var encrypted = EtaService.Encode("Hello, ZJU!");
    var decrypted = EtaService.Decode(encrypted);
    Console.WriteLine($"ETA encode/decode: '{decrypted}'");
}

// ============================================================
// CC98 — CC98 论坛
// ============================================================

if (IsEnabled("TEST_CC98"))
{
    Console.WriteLine("\n=== CC98 ===");
    var cc98Factory = provider.GetRequiredService<ICc98ServiceFactory>();
    using var cc98 = cc98Factory.Create(Env("CC98_USERNAME"), Env("CC98_PASSWORD"));
    var response = await cc98.FetchAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.cc98.org/me"));
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"CC98 response: {response.StatusCode}, length={body.Length}");
    Console.WriteLine(body);
}

Console.WriteLine("\nAll selected tests completed.");
