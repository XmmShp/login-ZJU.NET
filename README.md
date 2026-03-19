# login-ZJU.NET

A lightweight .NET library for authenticating with ZJU (Zhejiang University) services, with first-class dependency injection support.

[![NuGet](https://img.shields.io/nuget/v/login-ZJU.NET.svg)](https://www.nuget.org/packages/login-ZJU.NET)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![CI](https://github.com/XmmShp/login-ZJU.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/XmmShp/login-ZJU.NET/actions/workflows/ci.yml)

> **Based on [login-ZJU](https://github.com/5dbwat4/login-ZJU)** by [5dbwat4](https://github.com/5dbwat4) (MIT License).
> This is a C# / .NET port — see [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES) for the original license.

## Install

Target frameworks:

- `net8.0`
- `net9.0`
- `net10.0`

This package uses TargetFramework-specific `Microsoft.Extensions.*` versions to maximize compatibility:

- `net8.0` -> `Microsoft.Extensions.* 8.x`
- `net9.0` -> `Microsoft.Extensions.* 9.x`
- `net10.0` -> `Microsoft.Extensions.* 10.x`

```shell
dotnet add package login-ZJU.NET
```

## Quick Start

### Dependency Injection — factory pattern (recommended)

```csharp
using LoginZju.DependencyInjection;

// Register factories (no credentials here — those are per-user at runtime).
services.AddLoginZju();

// Optionally register CC98 factory (app-level OAuth2 client config).
services.AddLoginZjuCc98(options =>
{
    options.ClientId = "your_client_id";
    options.ClientSecret = "your_client_secret";
});
```

Then inject `ILoginZjuFactory` and create per-user instances:

```csharp
public class MyService(ILoginZjuFactory factory)
{
    public async Task DoWorkAsync(string username, string password)
    {
        using var auth = factory.CreateAuth(username, password);
        using var courses = factory.CreateCourses(auth);

        // FetchAsync automatically handles login on first call.
        var response = await courses.FetchAsync("https://courses.zju.edu.cn/api/courses");
        var content = await response.Content.ReadAsStringAsync();
    }
}
```

### Direct usage (without DI)

```csharp
using LoginZju;
using LoginZju.Services;

using var auth = new ZjuamAuth("username", "password");
using var zdbk = new ZdbkService(auth);

var response = await zdbk.FetchAsync("https://zdbk.zju.edu.cn/jwglxt/some/api");
var content = await response.Content.ReadAsStringAsync();
```

When using DI in a multi-user application, prefer creating one shared `IZjuamAuth` instance per user session/request flow and then create per-service instances from that auth object. This matches the upstream implementation behavior and avoids repeated CAS logins.

## Supported Services

| 名称 | 域名 | 接口 | 实现 | 备注 |
| --- | --- | --- | --- | --- |
| 统一身份认证 | zjuam.zju.edu.cn | `IZjuamAuth` | `ZjuamAuth` | 核心认证服务 |
| 智云课堂 | classroom.zju.edu.cn | `IClassroomService` | `ClassroomService` | — |
| 本科教学管理信息服务平台 | zdbk.zju.edu.cn | `IZdbkService` | `ZdbkService` | — |
| 表单填报助手 | form.zju.edu.cn | `IFormService` | `FormService` | — |
| 学在浙大 | courses.zju.edu.cn | `ICoursesService` | `CoursesService` | — |
| 校园卡二维码页面 | yqfkgl.zju.edu.cn | `IYqfkglService` | `YqfkglService` | — |
| 浙大先生开放平台 | open.zju.edu.cn | `IOpenService` | `OpenService` | 即 HiAgent |
| CC98 | cc98.org | `ICc98Service` | `Cc98Service` | 使用 CC98 账号，非 ZJUAM |
| ETA 三全育人平台 | eta.zju.edu.cn | `IEtaService` | `EtaService` | 提供 `Encode`/`Decode` 静态方法 |

> 鉴于部分服务可能会变更登录流程，如果发现登录流程失效，欢迎 [提交 Issue](https://github.com/XmmShp/login-ZJU.NET/issues) 或 [发起 PR](https://github.com/XmmShp/login-ZJU.NET/pulls)。

## API Overview

All services implement `IZjuService`:

```csharp
public interface IZjuService : IDisposable
{
    Task LoginAsync(CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> FetchAsync(
        string url,
        Action<HttpRequestMessage>? configureRequest = null,
        CancellationToken cancellationToken = default);
}
```

Each service additionally has its own interface (e.g. `ICoursesService`, `ICc98Service`) that extends `IZjuService`, and `IZjuamAuth` adds CAS/OAuth2 helpers:

```csharp
public interface IZjuamAuth : IZjuService
{
    Task<string> LoginServiceAsync(string serviceUrl, ...);
    Task<string> LoginServiceOAuth2Async(string redirectUrl, ...);
}
```

### Dependency Injection — factory pattern

采用工厂模式，支持多用户多实例。`AddLoginZju` 注册 `ILoginZjuFactory`，通过工厂在运行时为每个用户创建独立的认证和服务实例：

```csharp
// 注册工厂（不含凭据，凭据在运行时按用户提供）
services.AddLoginZju();

// 可选：注册 CC98 工厂（ClientId/ClientSecret 为应用级配置）
services.AddLoginZjuCc98(options =>
{
    options.ClientId = config["Cc98:ClientId"]!;
    options.ClientSecret = config["Cc98:ClientSecret"]!;
});
```

运行时创建用户实例：

```csharp
// 每个用户拥有独立的 auth + 服务实例（独立 cookie / token）
using var auth = factory.CreateAuth(username, password);
using var courses = factory.CreateCourses(auth);
var response = await courses.FetchAsync("...");

// CC98（独立账号体系）
using var cc98 = cc98Factory.Create(cc98Username, cc98Password);
```

### Sending POST requests

```csharp
var response = await service.FetchAsync("https://example.zju.edu.cn/api", request =>
{
    request.Method = HttpMethod.Post;
    request.Content = new StringContent("{\"key\":\"value\"}", Encoding.UTF8, "application/json");
});
```

### ETA Encryption Helpers

```csharp
using LoginZju.Services;

var encrypted = EtaService.Encode("plaintext");
var decrypted = EtaService.Decode(encrypted);
```

## Sample

See the [Sample](Sample/) project for complete usage examples. Copy `.env.template` to `.env`, fill in your credentials, and toggle `TEST_*` flags to choose which services to test.

The Sample project currently targets `net9.0` and inherits TargetFramework-specific package version selection from central package management.

## License

This project is licensed under the [Apache License 2.0](LICENSE).

This project is a derivative work of [login-ZJU](https://github.com/5dbwat4/login-ZJU) by [5dbwat4](https://github.com/5dbwat4), which is licensed under the [MIT License](https://opensource.org/licenses/MIT). See [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES) for the full original license text.
