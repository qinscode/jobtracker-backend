using System.ComponentModel.DataAnnotations;

namespace JobTracker.Models;

public class AddEmailConfigDto
{
    [Required] [EmailAddress] public string EmailAddress { get; set; }

    [Required]
    [MaxLength(20)]
    [Display(Name = "App Password")]
    public string Password { get; set; }

    [Required] public string Provider { get; set; } = "Gmail";
}

public class GeminiResponse
{
    public bool IsRejection { get; set; }
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
}