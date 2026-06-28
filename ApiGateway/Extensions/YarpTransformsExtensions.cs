using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;

namespace ApiGateway.Extensions
{
    /// <summary>
    /// 网关验签通过后，利用 YARP 的 Request Transforms（请求转换器），
    /// 把 JWT 里的 Claims（如 tenant_id、client_type）剥离出来，打成标准的 HTTP Header（如 X-Tenant-Id）塞进请求。
    //下游微服务只需闭着眼睛读取 HTTP Header 即可，完全不需要知道 JWT 的存在
    /// </summary>
    public static class YarpTransformsExtensions
    {
        public static IReverseProxyBuilder AddGatewayClaimsTransformer(this IReverseProxyBuilder builder)
        {
            return builder.AddTransforms(builderContext =>
            {
                builderContext.AddRequestTransform(transformContext =>
                {
                    var proxyHeaders = transformContext.ProxyRequest.Headers;

                    // =========================================================================
                    // 1.零信任清洗：无论是否登录，进来的伪造内网请求头一律无情抹除！
                    // =========================================================================
                    proxyHeaders.Remove("X-Caller-Type");
                    proxyHeaders.Remove("X-User-Id");
                    proxyHeaders.Remove("X-Client-Id");
                    proxyHeaders.Remove("X-Tenant-Id");
                    proxyHeaders.Remove("X-Store-Id");
                    proxyHeaders.Remove("X-Bypass-Isolation");

                    // =========================================================================
                    // 2. 匿名流量放行检查
                    // =========================================================================
                    // 如果是登录接口等匿名路由，此时请求头已被洗干净，可以安全地放行丢给下游了
                    var user = transformContext.HttpContext.User;
                    if (user.Identity?.IsAuthenticated != true)
                    {
                        return ValueTask.CompletedTask;
                    }

                    // =========================================================================
                    //  3. 核心分流透传（仅针对已认证过的合法 Token）
                    // =========================================================================
                    var clientType = user.FindFirst("client_type")?.Value;

                    if (clientType== "internalmicroservice")
                    {
                        // 路线 A：凭据模式 (Client Credentials) -> 机器流
                        proxyHeaders.TryAddWithoutValidation("X-Caller-Ty pe", "Machine");

                        var clientId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                       ?? user.FindFirst("client_id")?.Value;
                        proxyHeaders.TryAddWithoutValidation("X-Client-Id", clientId ?? "Unknown-Service");

                        var isBypass = user.FindFirst("merchant_isolation_bypass")?.Value ?? "false";
                        proxyHeaders.TryAddWithoutValidation("X-Bypass-Isolation", isBypass);
                    }
                    else
                    {
                        // 路线 B：账号密码/授权码模式 -> 前台Saas用户流
                        proxyHeaders.TryAddWithoutValidation("X-Caller-Type", "User");

                        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        if (!string.IsNullOrEmpty(userId)) proxyHeaders.TryAddWithoutValidation("X-User-Id", userId);

                        var tenantId = user.FindFirst("tenant_id")?.Value
                                       ?? user.FindFirst("belong_merchant_id")?.Value;
                        if (!string.IsNullOrEmpty(tenantId)) proxyHeaders.TryAddWithoutValidation("X-Tenant-Id", tenantId);

                        var storeId = user.FindFirst("belong_store_id")?.Value;
                        if (!string.IsNullOrEmpty(storeId)) proxyHeaders.TryAddWithoutValidation("X-Store-Id", storeId);
                    }

                    return ValueTask.CompletedTask;
                });
            });
        }
    }
}
