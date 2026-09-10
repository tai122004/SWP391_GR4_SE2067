using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace RWPM.Common
{
    public static class AlertHelper
    {
        public static void AddSuccessMessage(ITempDataDictionary tempData, string successMessage)
        {
            tempData["SuccessMessage"] = successMessage;
        }

        public static void AddErrorMessage(ITempDataDictionary tempData, string errorMessage)
        {
            tempData["ErrorMessage"] = errorMessage;
        }

        public static void CreateSuccess(ITempDataDictionary tempData)
        {
            tempData["SuccessMessage"] = Resources.Shared.SharedResource.Notification_CreatedSuccessfully;
        }

        public static void EditSuccess(ITempDataDictionary tempData)
        {
            tempData["SuccessMessage"] = Resources.Shared.SharedResource.Notification_EditedSuccessfully;
        }

        public static void DeleteSuccess(ITempDataDictionary tempData)
        {
            tempData["SuccessMessage"] = Resources.Shared.SharedResource.Notification_DeletedSuccessfully;
        }
    }
}
