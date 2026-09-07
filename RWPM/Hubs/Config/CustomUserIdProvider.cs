using Microsoft.AspNetCore.SignalR;

namespace RWPM.Hubs.Config
{
    public class CustomUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            return connection.User?.Identity?.Name;
        }
    }
}
