using ApiGateway.Policies;
using Polly;
using System.Collections.Concurrent;

namespace ApiGateway.Providers
{
    public class GatewayPolicyProvider
    {
        // 用字典把各个服务的策略存起来，Key 是 YARP 的 ClusterId (如 OrderService, UserService)
        private readonly ConcurrentDictionary<string, IAsyncPolicy<HttpResponseMessage>> _policyCache = new();
        private readonly ILoggerFactory _loggerFactory;

        public GatewayPolicyProvider(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public IAsyncPolicy<HttpResponseMessage> GetPolicy(string clusterId)
        {
            // 核心：存在就直接拿，不存在才动态创建（保证熔断器状态不丢失）
            return _policyCache.GetOrAdd(clusterId, id =>
            {
                // 为每个微服务单独创建对应前缀的 Logger，方便在日志里过滤搜寻
                var logger = _loggerFactory.CreateLogger($"ApiGateway.Polly.{id}");
                return PollyPolicyFactory.Create(id, logger);
            });
        }
    }
}
