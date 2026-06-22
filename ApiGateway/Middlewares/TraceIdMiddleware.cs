using Serilog.Context;
using System.Diagnostics;

namespace ApiGateway.Middlewares
{
    public class TraceIdMiddleware
    {
        private readonly RequestDelegate _next;

        public TraceIdMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            // 优先获取 .NET Activity 机制（OpenTelemetry 传播的）的 TraceId，如果没有则生成全新的
            var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

            // 将 TraceId 塞入响应头，方便前端排查问题
            context.Response.Headers.TryAdd("X-Trace-Id", traceId);

            // 推入 Serilog 属性压栈，当前请求内的所有 Log 都会自动带上这个 TraceId
            using (LogContext.PushProperty("TraceId", traceId))
            {
                await _next(context);
            }
        }
    }
}
