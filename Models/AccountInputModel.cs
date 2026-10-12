using System.ComponentModel.DataAnnotations;

namespace RssAsp.Models;

public class AccountInputModel
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool IsRegistering { get; set; }
}
