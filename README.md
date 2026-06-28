graph TD
    A[客户端] -- HTTPS请求gateway.domain.com/{服务名} --> B[YARP自研API网关.NET10 统一入口]
    
    B -->|1.JWT验签2.限流熔断3.链路追踪| C{Consul注册中心}
    C -- 动态获取健康实例 --> D[业务微服务集群Payment/Order/User等]
    
    E[IdentityService认证中心JWT签发+密钥轮换] --> F[NFS共享密钥存储ReadWriteMany持久卷]
    F -- 只读加载公钥 --> B
    
    %% 部署底座
    subgraph K3s 容器集群
        B
        C
        D
        E
    end
    
    %% 运维流水线
    G[GitHub Actions CI/CD] --> H[腾讯云CCR镜像仓库]
    H --> K3s
    
    %% 健康探测
    B -.->|/gateway/health| C
    D -.->|服务健康上报| C
