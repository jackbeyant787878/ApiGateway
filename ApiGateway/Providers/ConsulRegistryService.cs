using Consul;

namespace ApiGateway.Providers
{
    public class ConsulRegistryService : IHostedService
    {
        private readonly IConsulClient _consulClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConsulRegistryService> _logger;
        private string? _dynamicServiceId; // 更改为动态保存

        public ConsulRegistryService(IConsulClient consulClient, IConfiguration configuration, ILogger<ConsulRegistryService> logger)
        {
            _consulClient = consulClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var section = _configuration.GetSection("Consul:Service");
            var serviceName = section["Name"] ?? "micro-gateway";
            var address = section["Address"] ?? "127.0.0.1";
            var port = section.GetValue<int>("Port", 5000);
            var healthCheckPath = section["HealthCheckPath"] ?? "/gateway/health";

            // 动态生成唯一 ServiceId 
            // 格式：micro-gateway-主机名-端口 (例如: micro-gateway-VM-4-15-ubuntu-5000)
            var machineName = Environment.MachineName.Replace(".", "_"); // 避免特殊字符污染
            _dynamicServiceId = $"{serviceName}-{machineName}-{port}";

            var registration = new AgentServiceRegistration()
            {
                ID = _dynamicServiceId, // 使用动态ID
                Name = serviceName,     // 服务名保持一致，Consul会自动将其归为同一集群
                Address = address,
                Port = port,
                Check = new AgentServiceCheck()
                {
                    HTTP = $"http://{address}:{port}{healthCheckPath}",
                    Timeout = TimeSpan.FromSeconds(5),
                    Interval = TimeSpan.FromSeconds(10),
                    DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(30)
                }
            };

            
            try
            {
                _logger.LogInformation("网关实例启动，正在动态注册到 Consul. Id: {ServiceId}", _dynamicServiceId);
                await _consulClient.Agent.ServiceRegister(registration, cancellationToken);
                _logger.LogInformation("网关已成功注册到 Consul中. Id: {ServiceId}", _dynamicServiceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "向Consul动态注册网关服务时发生异常");
            }

        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_dynamicServiceId)) return;

            _logger.LogInformation("网关实例关闭，正在从 Consul 动态注销. Id: {ServiceId}", _dynamicServiceId);
            try
            {
                await _consulClient.Agent.ServiceDeregister(_dynamicServiceId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从 Consul 注销网关服务时发生异常");
            }
        }
    }
}
