using System.Text.Json.Serialization;

namespace JobTracker.Models;

public class CreateUserJobDto
{
    public Guid UserId { get; set; }
    public int JobId { get; set; }  // Changed from Guid to int
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UserJobStatus Status { get; set; }
}