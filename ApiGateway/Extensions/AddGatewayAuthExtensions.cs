using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace ApiGateway.Extensions
{
    public static class AddGatewayAuthExtensions
    {
        public static IServiceCollection AddGatewayAuth(this IServiceCollection services, IConfiguration configuration)
        {
            var pemPath = configuration["Jwt:PublicKeyPath"] ?? "Configurations/auth_public_key.pem";

            if (!File.Exists(pemPath))
                throw new FileNotFoundException($"找不到权限中心的公钥文件: {pemPath}");

            var publicKeyPem = File.ReadAllText(pemPath);
            var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem); // .NET Core 3.0+ 已经原生支持直接导入 PEM

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new RsaSecurityKey(rsa),
                        ValidateIssuer = true,
                        ValidIssuer = configuration["Jwt:Issuer"] ?? "your-ddd-auth-center",
                        ValidateAudience = false,
                        ClockSkew = TimeSpan.FromMinutes(1) // 容忍时钟漂移
                    };
                });

            services.AddAuthorization(options =>
            {
                // 定义策略供 YARP 路由标签绑定
                options.AddPolicy("GatewayAuthPolicy", policy => policy.RequireAuthenticatedUser());
            });

            return services;
        }
    }
}
