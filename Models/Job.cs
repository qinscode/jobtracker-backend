using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobTracker.Models
{
    public class Job
    {
        [Key]
        public Guid Id { get; set; }

        public string? JobTitle { get; set; }
        public string? BusinessName { get; set; }
        public string? WorkType { get; set; }
        public string? JobType { get; set; }
        public string? PayRange { get; set; }
        public string? Suburb { get; set; }
        public string? Area { get; set; }
        public string? Url { get; set; }
        public DateTime? PostedDate { get; set; }
        public string? JobDescription { get; set; }
        public Guid? AdvertiserId { get; set; }

        [ForeignKey("AdvertiserId")]
        public Company? Advertiser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}