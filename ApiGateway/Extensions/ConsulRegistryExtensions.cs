using ApiGateway.Providers;
using Consul;

namespace ApiGateway.Extensions
{
    public static class ConsulRegistryExtensions
    {
        /// <summary>
        /// 优雅集成 Consul 注册服务
        /// </summary>
        public static IServiceCollection AddConsulRegistry(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. 注入 Consul 核心客户端（如果之前没注入过）
            services.AddSingleton<IConsulClient>(sp => new ConsulClient(cfg =>
            {
                cfg.Address = new Uri(configuration["Consul:Address"] ?? "http://localhost:8500");
            }));

            // 2. 注入自注册后台托管服务
            services.AddHostedService<ConsulRegistryService>();

            return services;
        }
    }
}
