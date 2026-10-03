using System.ComponentModel.DataAnnotations;

namespace SmartMosquitoControl.Models;

public class LoginViewModel
{
    [Required]
    [Display(Name = "Email or phone number")]
    public string EmailOrPhone { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
