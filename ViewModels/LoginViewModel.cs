using System.ComponentModel.DataAnnotations;

namespace FinSyncNexus.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }
}
