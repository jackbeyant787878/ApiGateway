using Consul;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
using DestinationConfig = Yarp.ReverseProxy.Configuration.DestinationConfig;
using RouteConfig = Yarp.ReverseProxy.Configuration.RouteConfig;

namespace ApiGateway.Providers
{
    public class ConsulServiceWatcher : BackgroundService
    {
        private readonly IConsulClient _consulClient;
        private readonly YarpConfigProvider _yarpConfigProvider;
        private readonly ILogger<ConsulServiceWatcher> _logger;

        public ConsulServiceWatcher(IConsulClient consulClient, IProxyConfigProvider yarpConfigProvider, ILogger<ConsulServiceWatcher> logger)
        {
            _consulClient = consulClient;
            // 强制转换为我们的实现类
            _yarpConfigProvider = (YarpConfigProvider)yarpConfigProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Consul 动态路由监听服务已启动...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 1.从 Consul 获取所有健康的服务
                    var servicesResponse = await _consulClient.Catalog.Services(stoppingToken);
                    var routes = new List<RouteConfig>();
                    var clusters = new List<ClusterConfig>();

                    foreach (var service in servicesResponse.Response)
                    {
                        string serviceName = service.Key;
                        if (serviceName.Equals("consul", StringComparison.OrdinalIgnoreCase)) continue;

                        // 获取该服务下的所有健康节点
                        var instancesResponse = await _consulClient.Health.Service(serviceName, null, true, stoppingToken);

                        // 从健康节点中,探查该服务是否带有“允许匿名”的标签
                        var serviceTags = instancesResponse.Response.FirstOrDefault()?.Service?.Tags ?? Array.Empty<string>();
                        bool allowAnonymous = serviceTags.Contains("anonymous_allowed", StringComparer.OrdinalIgnoreCase);


                        var destinations = new Dictionary<string, DestinationConfig>();
                        foreach (var instance in instancesResponse.Response)
                        {
                            destinations.Add(instance.Service.ID, new DestinationConfig
                            {
                                Address = $"http://{instance.Service.Address}:{instance.Service.Port}"
                            });
                        }

                        if (destinations.Count == 0) continue;

                        // 2. 核心避坑:构建符合前端约定的 路由/服务 规则
                        // 规则：网关域名/order-service/{**catch-all} -> 转发
                        var route = new RouteConfig
                        {
                            RouteId = $"{serviceName}_route",
                            ClusterId = $"{serviceName}_cluster",
                            Match = new RouteMatch { Path = $"/{serviceName}/{{**catch-all}}" },
                            Order = 10,
                            //动态赋值:如果是登录/公开微服务，就不挂载鉴权策略；其余业务服务强制挂载！
                            AuthorizationPolicy = allowAnonymous ? null : "GatewayAuthPolicy", // 绑定鉴权,
                            RateLimiterPolicy= "GatewayRateLimitPolicy" // 绑定限流
                        }
                        .WithTransformPathRemovePrefix(prefix: $"/{serviceName}"); // 裁剪前缀
                        var cluster = new ClusterConfig
                        {
                            ClusterId = $"{serviceName}_cluster",
                            Destinations = destinations
                        };

                        routes.Add(route);
                        clusters.Add(cluster);
                    }

                    // 3. 刷新 YARP
                    _yarpConfigProvider.Update(routes, clusters);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "从 Consul 刷新路由表时发生异常");
                }

                // 频率控制:每 5 秒轮询一次（生产环境建议配合 Consul 的 Wait 参数做长轮询，此处为简化演示）
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
