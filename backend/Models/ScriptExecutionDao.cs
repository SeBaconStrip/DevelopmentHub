using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DevelopmentHub.Api.Models;

public enum ExecutionStatus
{
    Running,
    Success,
    Failed,
    Cancelled
}

public class ScriptExecutionDao
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string ScriptDefinitionId { get; set; } = string.Empty;
    public string ScriptName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public int? ExitCode { get; set; }

    [BsonRepresentation(BsonType.String)]
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Running;

    public string OutputLog { get; set; } = string.Empty;
}
