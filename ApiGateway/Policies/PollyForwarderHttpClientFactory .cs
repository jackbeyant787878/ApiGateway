using ApiGateway.Policies;
using ApiGateway.Providers;
using Yarp.ReverseProxy.Forwarder;

namespace ApiGateway.Handlers;

public class PollyForwarderHttpClientFactory : ForwarderHttpClientFactory
{
    private readonly GatewayPolicyProvider _policyProvider;

    public PollyForwarderHttpClientFactory(ILoggerFactory loggerFactory, GatewayPolicyProvider policyProvider)
        : base(loggerFactory.CreateLogger<ForwarderHttpClientFactory>())
    {
        _policyProvider = policyProvider;
    }

    // 参数和返回值都是 HttpMessageHandler！
    // 完美重写 YARP 的官方管道，顺畅编译
    protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler)
    {
        // 一气呵成：直接把 YARP 配置好的底层 handler 塞进我们的 Polly 拦截器里
        var pollyHandler = new YarpPollyHandler(_policyProvider, context.ClusterId)
        {
            InnerHandler = handler
        };

        // 顺着 YARP 默认链条返回即可
        return base.WrapHandler(context, pollyHandler);
    }
}