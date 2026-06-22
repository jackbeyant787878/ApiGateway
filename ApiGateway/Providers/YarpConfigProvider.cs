using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.Providers
{
    public class YarpConfigProvider : IProxyConfigProvider
    {
        private CustomMemoryConfig _config;

        public YarpConfigProvider()
        {
            // 初始化空配置，防止启动时报错
            _config = new CustomMemoryConfig(new List<RouteConfig>(), new List<ClusterConfig>());
        }

        public IProxyConfig GetConfig() => _config;

        /// <summary>
        /// 当 Consul 监听到服务变化时，外部调用此方法触发 YARP 刷新
        /// </summary>
        public void Update(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
            var oldConfig = Interlocked.Exchange(ref _config, new CustomMemoryConfig(routes, clusters));
            oldConfig.SignalChange(); // 关键：通知YARP物理刷新路由表
        }

        private class CustomMemoryConfig : IProxyConfig
        {
            private readonly CancellationTokenSource _cts = new();
            public IReadOnlyList<RouteConfig> Routes { get; }
            public IReadOnlyList<ClusterConfig> Clusters { get; }
            public IChangeToken ChangeToken { get; }

            public CustomMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
            {
                Routes = routes;
                Clusters = clusters;
                ChangeToken = new CancellationChangeToken(_cts.Token);
            }

            public void SignalChange() => _cts.Cancel();
        }
    }
}
