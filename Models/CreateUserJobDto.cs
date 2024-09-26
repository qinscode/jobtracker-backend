using System.Text.Json.Serialization;

namespace JobTracker.Models;

public class CreateUserJobDto
{
    public Guid UserId { get; set; }
    public Guid JobId { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UserJobStatus Status { get; set; }
}