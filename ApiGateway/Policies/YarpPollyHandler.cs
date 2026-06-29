using ApiGateway.Policies;
using ApiGateway.Providers;

namespace ApiGateway.Handlers;

public class YarpPollyHandler : DelegatingHandler
{
    private readonly GatewayPolicyProvider _policyProvider;
    private readonly string _clusterId;

    // 在构造时直接传入所属的微服务集群 ID
    public YarpPollyHandler(GatewayPolicyProvider policyProvider, string clusterId)
    {
        _policyProvider = policyProvider;
        _clusterId = clusterId;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 1. 获取针对当前微服务的 Polly 策略（重试+熔断+降级）
        var policy = _policyProvider.GetPolicy(_clusterId);

        // 2. 用策略包裹执行
        return await policy.ExecuteAsync(async (ct) =>
            await base.SendAsync(request, ct), cancellationToken);
    }
}