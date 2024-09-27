using System.Text.Json.Serialization;

namespace JobTracker.Models;

public class CreateUserJobDto
{
    public int JobId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UserJobStatus Status { get; set; }
}