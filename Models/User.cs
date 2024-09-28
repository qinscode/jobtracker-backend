using System;
using System.Collections.Generic;
using BCrypt.Net;
using System.ComponentModel.DataAnnotations;

namespace JobTracker.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<UserJob>? UserJobs { get; set; }
        
        public void SetPassword(string password)
        {
            Password = BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool VerifyPassword(string password)
        {
            return BCrypt.Net.BCrypt.Verify(password, Password);
        }
    }
}