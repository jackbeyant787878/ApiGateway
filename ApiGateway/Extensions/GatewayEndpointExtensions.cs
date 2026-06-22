namespace ApiGateway.Extensions
{
    public static class GatewayEndpointExtensions
    {
        /// <summary>
        /// 映射网关内部的基础基础设施端点
        /// </summary>
        public static IEndpointRouteBuilder MapGatewayEndpoints(this IEndpointRouteBuilder endpoints, IConfiguration configuration)
        {
            // 从配置中读取健康检查路径，保持与 Consul 检查路径完全一致
            var healthPath = configuration["Consul:Service:HealthCheckPath"] ?? "/gateway/health";

            endpoints.MapGet(healthPath, () => Results.Ok(new
            {
                Status = "Healthy",
                MachineName = Environment.MachineName,
                Time = DateTime.UtcNow
            }))
            .AllowAnonymous(); // 必须允许匿名，绕过 JWT

            return endpoints;
        }
    }
}
