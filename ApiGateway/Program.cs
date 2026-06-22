using ApiGateway.Extensions;
using ApiGateway.Middlewares;
using ApiGateway.Providers;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// 1. 注入基础日志
Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).Enrich.FromLogContext().CreateLogger();
builder.Host.UseSerilog();


// 注册 YARP 自定义配置提供者
builder.Services.AddSingleton<YarpConfigProvider>();
builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<YarpConfigProvider>());

// 注入 YARP 核心服务
builder.Services.AddReverseProxy();

// 3. 引入自定义扩展（组件解耦）
builder.Services.AddGatewayAuth(builder.Configuration);
builder.Services.AddConsulRegistry(builder.Configuration);      //Consul自注册扩展
// .NET 10 原生限流
builder.Services.AddRateLimiter(options => {
    options.AddTokenBucketLimiter("GatewayRateLimitPolicy", opt => {
        opt.TokenLimit = 200; opt.ReplenishmentPeriod = TimeSpan.FromSeconds(1); opt.TokensPerPeriod = 50; opt.QueueLimit = 0;
    });
});

// 4. 注册 Consul 动态监听后台任务
builder.Services.AddHostedService<ConsulServiceWatcher>();



var app = builder.Build();

// 5. 中间件管道顺序（严格注意顺序！）
app.UseMiddleware<TraceIdMiddleware>(); // 1. 最外层注入 TraceId

app.UseRateLimiter();                  // 2. 限流挡板（防止恶意刷）

app.UseAuthentication();               // 3. 身份验证
app.UseAuthorization();                // 4. 权限授权

// 6. 激活 YARP 终点路由转发
app.MapGatewayEndpoints(app.Configuration); //映射内部基础设施端点
app.MapReverseProxy();

app.Run();
