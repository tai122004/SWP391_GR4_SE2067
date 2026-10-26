namespace RWPM.Services.Abstraction
{
    public interface IProgressNotificationService
    {
        Task UpdateProgressBar(string userId, byte percent);
        Task UpdateProgressBar(string userId, int dataHandled, int totalData);
    }
}
