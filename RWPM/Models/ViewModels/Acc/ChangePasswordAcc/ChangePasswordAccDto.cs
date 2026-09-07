namespace RWPM.Models.ViewModels.Acc.ChangePasswordAcc
{
    public class ChangePasswordAccDto
    {
        public string Username { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty; 
        public string ConfirmPassword { get; set; } = string.Empty;    
    }
}
