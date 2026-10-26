using RWPM.Models.ViewModels.Dashboard;

namespace RWPM.Services.Abstraction
{
    public interface IDashboardService
    {
        /// <summary>
        /// Builds the home dashboard for the given user.
        /// isChainManager: Admin/HR (chain-wide view).
        /// isStoreManager: StoreManager (store view when the user has a profile with a store).
        /// canUseAttendance: false for a pure system Admin (Attendance/Index redirects them away).
        /// </summary>
        Task<DashboardVM> GetDashboardAsync(string username, bool isChainManager, bool isStoreManager, bool canUseAttendance);
    }
}
