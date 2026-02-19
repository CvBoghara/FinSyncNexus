using System.ComponentModel.DataAnnotations;

namespace FinSyncNexus.ViewModels;

public class LoginViewModel
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }
}
