using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.SignalR;
using RWPM.Hubs;
using RWPM.Services.Abstraction;

namespace RWPM.Services.Implementation
{
    public class ProgressNotificationService : IProgressNotificationService
    {
        private readonly IHubContext<ApplicationHub> _hubContext;
        public ProgressNotificationService(IHubContext<ApplicationHub> hubContext) 
        { 
            _hubContext = hubContext; 
        }

        public Task UpdateProgressMessage(string userId, string message)
            => _hubContext.Clients.User(userId).SendAsync("ProgressNotificationService_UpdateProgressMessage", message);

        public Task UpdateProgressBar(string userId, byte percent)
            => _hubContext.Clients.User(userId).SendAsync("ProgressNotificationService_UpdateProgressBar", percent);

        public async Task UpdateProgressBar(string userId, int dataHandled, int totalData)
        {
            if (dataHandled == totalData)
                 await UpdateProgressBar(userId, 100);
            else if (dataHandled % Math.Ceiling(totalData / 10f) == 0)
            {
                byte percent = (byte)(dataHandled / (float)totalData * 100);
                await UpdateProgressBar(userId, percent);
            }
        }
    }
}
