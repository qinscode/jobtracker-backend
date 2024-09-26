using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JobTracker.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? PasswordHash { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<UserJob>? UserJobs { get; set; }
    }
}