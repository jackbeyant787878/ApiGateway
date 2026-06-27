using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace ApiGateway.Extensions
{
    public static class GatewayAuthExtensions
    {
        public static IServiceCollection AddGatewayAuth(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. 加载所有公钥（活跃 + 历史）
            var publicKeys = LoadAllPublicKeys(configuration);

            if (!publicKeys.Any())
            {
                throw new InvalidOperationException("未能加载到任何有效的公钥，请检查密钥文件路径和格式。");
            }

            // 2. 配置 JWT Bearer 认证
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        // 兼容新旧密钥，允许使用多个公钥进行验证
                        IssuerSigningKeys = publicKeys,

                        ValidateIssuer = true,
                        ValidIssuer = configuration["Jwt:Issuer"] ?? "your-ddd-auth-center",

                        ValidateAudience = false,
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("GatewayAuthPolicy", policy => policy.RequireAuthenticatedUser());
            });

            return services;
        }

        /// <summary>
        /// 加载所有公钥，包括活跃密钥和历史兼容密钥
        /// </summary>
        private static List<SecurityKey> LoadAllPublicKeys(IConfiguration configuration)
        {
            var keys = new List<SecurityKey>();

            // 获取密钥根目录，与 IdentityService 保持一致
            var keysBaseDir = configuration["KeySettings:BaseDirectory"] ?? "keys";
            // 确保是绝对路径，避免在 Linux 环境下因工作目录不同导致的问题
            if (!Path.IsPathRooted(keysBaseDir))
            {
                keysBaseDir = Path.Combine(AppContext.BaseDirectory, keysBaseDir);
            }

            // 1. 加载活跃公钥 (active/public.pem)
            var activePublicKeyPath = Path.Combine(keysBaseDir, "signing", "public.pem");
            if (File.Exists(activePublicKeyPath))
            {
                keys.Add(LoadKeyFromFile(activePublicKeyPath));
            }
            else
            {
                // 在生产环境中，这通常是一个严重错误，因为服务无法验证任何新签发的 Token
                throw new FileNotFoundException($"活跃公钥文件未找到: {activePublicKeyPath}");
            }

            // 2. 加载历史公钥 (history/signing/*/public.pem)
            var historyDir = Path.Combine(keysBaseDir, "history", "signing");
            if (Directory.Exists(historyDir))
            {
                // 获取 history/signing 下的所有子目录
                var historySubDirs = Directory.GetDirectories(historyDir);
                foreach (var subDir in historySubDirs)
                {
                    var historyPublicKeyPath = Path.Combine(subDir, "public.pem");
                    if (File.Exists(historyPublicKeyPath))
                    {
                        try
                        {
                            keys.Add(LoadKeyFromFile(historyPublicKeyPath));
                        }
                        catch (Exception ex)
                        {
                            // 记录日志：某个历史密钥加载失败，但不应中断整个服务的启动
                            Console.WriteLine($"警告: 加载历史公钥失败，路径: {historyPublicKeyPath}, 错误: {ex.Message}");
                        }
                    }
                }
            }

            return keys;
        }

        /// <summary>
        /// 从 PEM 文件加载单个 RSA 公钥
        /// </summary>
        private static SecurityKey LoadKeyFromFile(string publicKeyPath)
        {
            var rsa = RSA.Create();
            var publicKeyPem = File.ReadAllText(publicKeyPath);
            rsa.ImportFromPem(publicKeyPem);
            return new RsaSecurityKey(rsa);
        }
    }

}
