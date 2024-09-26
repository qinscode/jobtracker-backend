using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JobTracker.Models
{
    public class Company
    {
        [Key]
        public Guid Id { get; set; }

        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Website { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<Job>? Jobs { get; set; }
    }
}