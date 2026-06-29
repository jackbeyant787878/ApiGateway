using Polly;
using System.Net;
using System.Diagnostics;
using Polly.Extensions.Http;

namespace ApiGateway.Policies;

public static class PollyPolicyFactory
{
    public static IAsyncPolicy<HttpResponseMessage> Create(
        string serviceName,
        ILogger logger)
    {
        // 1️⃣ 重试策略：基于你的结构，直接点出异常和状态码
        var retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, i => TimeSpan.FromMilliseconds(200 * i),
                (outcome, timespan, retryCount, ctx) =>
                {
                    var traceId = Activity.Current?.TraceId.ToString() ?? "N/A";
                    var exceptionMessage = outcome.Exception?.Message ?? "无内部异常(纯5xx状态码错误)";
                    var statusCode = outcome.Result?.StatusCode.ToString() ?? "网络连接失败/超时";

                    logger.LogWarning(
                        "[Retry] 服务 {Service} | 第 {Count} 次重试 | TraceId={TraceId} | [状态码: {StatusCode}] [错误流: {Exception}]",
                        serviceName, retryCount, traceId, statusCode, exceptionMessage);
                });

        // 2️⃣ 熔断策略：基于你的结构，直接点出熔断根源
        var breaker = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                10,
                TimeSpan.FromSeconds(30),
                (outcome, breakDelay) =>
                {
                    var traceId = Activity.Current?.TraceId.ToString() ?? "N/A";
                    var exceptionMessage = outcome.Exception?.Message ?? "纯5xx错误触发";
                    var statusCode = outcome.Result?.StatusCode.ToString() ?? "网络彻底断开";

                    logger.LogCritical(
                        "[Breaker OPEN] 服务 {Service} 熔断器开启！阻断 {Seconds} 秒 | TraceId={TraceId} | 最后一击 -> [状态码: {StatusCode}] [异常: {Exception}]",
                        serviceName, breakDelay.TotalSeconds, traceId, statusCode, exceptionMessage);
                },
                () =>
                {
                    logger.LogInformation("[Breaker RESET] 服务 {Service} 熔断恢复，健康流量全面放行。", serviceName);
                });

        // 3️⃣降级策略:完全保留Policy<HttpResponseMessage> 链式调用
        
        var fallback = Policy<HttpResponseMessage>
            .Handle<Exception>()
            .OrResult(r => !r.IsSuccessStatusCode)
            .FallbackAsync(
                // 参数 1 (fallbackAction)：负责干净、丝滑地返回降级数据
                (ct) =>
                {
                    var traceId = Activity.Current?.TraceId.ToString() ?? "N/A";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent($$$"""
                        {
                            "code": 503001,
                            "msg": "当前服务繁忙，网关已自动触发降级",
                            "traceId": "{{{traceId}}}",
                            "data": null
                        }
                        """, System.Text.Encoding.UTF8, "application/json")
                    });
                },
                // 参数 2 (onFallback)：负责安全、全面地抓取异常和状态码打日志
                (outcome) =>
                {
                    var traceId = Activity.Current?.TraceId.ToString() ?? "N/A";
                    var exceptionMessage = outcome.Exception?.Message ?? "无内部异常(常规业务状态码不通过)";
                    var statusCode = outcome.Result?.StatusCode.ToString() ?? "无法连接目标/请求已被熔断拦截";

                    logger.LogError(
                        "[Fallback] 服务 {Service} 触发最终降级保护！TraceId={TraceId} | 归因 -> [返回码: {StatusCode}] [底层异常: {Exception}]",
                        serviceName, traceId, statusCode, exceptionMessage);

                    return Task.CompletedTask;
                });

        return Policy.WrapAsync(fallback, breaker, retry);
    }
}