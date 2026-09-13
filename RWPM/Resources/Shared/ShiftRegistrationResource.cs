namespace RWPM.Resources.Shared
{
    using System;
    
    public class ShiftRegistrationResource {
        private static global::System.Resources.ResourceManager resourceMan;
        private static global::System.Globalization.CultureInfo resourceCulture;

        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("RWPM.Resources.Shared.ShiftRegistrationResource", typeof(ShiftRegistrationResource).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }

        public static global::System.Globalization.CultureInfo Culture {
            get { return resourceCulture; }
            set { resourceCulture = value; }
        }

        public static string ShiftRegistration => ResourceManager.GetString("ShiftRegistration", resourceCulture);
        public static string FilterByEmployeeId => ResourceManager.GetString("FilterByEmployeeId", resourceCulture);
        public static string Filter => ResourceManager.GetString("Filter", resourceCulture);
        public static string RegisterShift => ResourceManager.GetString("RegisterShift", resourceCulture);
        public static string Employee => ResourceManager.GetString("Employee", resourceCulture);
        public static string SelectEmployee => ResourceManager.GetString("SelectEmployee", resourceCulture);
        public static string WorkDate => ResourceManager.GetString("WorkDate", resourceCulture);
        public static string Shift => ResourceManager.GetString("Shift", resourceCulture);
        public static string SelectShift => ResourceManager.GetString("SelectShift", resourceCulture);
        public static string Note => ResourceManager.GetString("Note", resourceCulture);
        public static string Close => ResourceManager.GetString("Close", resourceCulture);
        public static string Save => ResourceManager.GetString("Save", resourceCulture);
        public static string ShiftDetails => ResourceManager.GetString("ShiftDetails", resourceCulture);
        public static string Time => ResourceManager.GetString("Time", resourceCulture);
        public static string Status => ResourceManager.GetString("Status", resourceCulture);
        public static string Approve => ResourceManager.GetString("Approve", resourceCulture);
        public static string Reject => ResourceManager.GetString("Reject", resourceCulture);
        public static string Cancel => ResourceManager.GetString("Cancel", resourceCulture);
        public static string PastDateError => ResourceManager.GetString("PastDateError", resourceCulture);
        public static string ConfirmCancel => ResourceManager.GetString("ConfirmCancel", resourceCulture);
        public static string SuccessStatus => ResourceManager.GetString("SuccessStatus", resourceCulture);
        public static string ErrorPrefix => ResourceManager.GetString("ErrorPrefix", resourceCulture);
        public static string UpdateFailed => ResourceManager.GetString("UpdateFailed", resourceCulture);
    }
}
