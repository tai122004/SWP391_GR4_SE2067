namespace RWPM.Services.Abstraction
{
    public interface IProgressNotificationService
    {
        Task UpdateProgressMessage(string userId, string message);
        Task UpdateProgressBar(string userId, byte percent);
        Task UpdateProgressBar(string userId, int dataHandled, int totalData);
    }
}
