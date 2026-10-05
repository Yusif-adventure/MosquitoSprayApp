using System.ComponentModel.DataAnnotations;

namespace SmartMosquitoControl.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Email or phone number is required")]
    [Display(Name = "Email or phone number")]
    public string EmailOrPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string Password { get; set; } = string.Empty;
}
