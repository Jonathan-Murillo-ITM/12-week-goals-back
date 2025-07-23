using System.Text.Json;

namespace _12WeekGoals.Services
{
    public interface ITokenCacheService
    {
        Task<string?> GetValidTokenAsync();
        Task SaveTokenAsync(string accessToken, string refreshToken = "", int expiresIn = 3600);
        Task ClearTokenAsync();
        Task<bool> IsTokenValidAsync();
    }

    public class TokenCacheService : ITokenCacheService
    {
        private readonly string _tokenFilePath;

        public TokenCacheService()
        {
            // Guardar el token en un archivo temporal
            _tokenFilePath = Path.Combine(Path.GetTempPath(), "12weeks_token.json");
        }

        public async Task<string?> GetValidTokenAsync()
        {
            try
            {
                if (!File.Exists(_tokenFilePath))
                    return null;

                var json = await File.ReadAllTextAsync(_tokenFilePath);
                var tokenData = JsonSerializer.Deserialize<TokenCache>(json);

                if (tokenData == null || DateTime.UtcNow >= tokenData.ExpiresAt)
                {
                    // Token expirado, intentar renovar con refresh token
                    if (!string.IsNullOrEmpty(tokenData?.RefreshToken))
                    {
                        try
                        {
                            var newToken = await RefreshTokenAsync(tokenData.RefreshToken);
                            return newToken;
                        }
                        catch
                        {
                            // Si falla el refresh, eliminar cache
                            await ClearTokenAsync();
                            return null;
                        }
                    }
                    return null;
                }

                return tokenData.AccessToken;
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveTokenAsync(string accessToken, string refreshToken = "", int expiresIn = 3600)
        {
            var tokenData = new TokenCache
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 300) // 5 minutos antes para estar seguro
            };

            var json = JsonSerializer.Serialize(tokenData);
            await File.WriteAllTextAsync(_tokenFilePath, json);
        }

        public Task ClearTokenAsync()
        {
            try
            {
                if (File.Exists(_tokenFilePath))
                {
                    File.Delete(_tokenFilePath);
                }
            }
            catch
            {
                // Ignorar errores al eliminar
            }
            return Task.CompletedTask;
        }

        public async Task<bool> IsTokenValidAsync()
        {
            var token = await GetValidTokenAsync();
            return !string.IsNullOrEmpty(token);
        }

        private Task<string?> RefreshTokenAsync(string refreshToken)
        {
            // Implementar refresh token si Microsoft Graph lo soporta
            // Por ahora retornamos null para forzar re-autenticación
            return Task.FromResult<string?>(null);
        }

        private class TokenCache
        {
            public string AccessToken { get; set; } = "";
            public string RefreshToken { get; set; } = "";
            public DateTime ExpiresAt { get; set; }
        }
    }
}
