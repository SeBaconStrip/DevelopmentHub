using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DevelopmentHub.Api.Models;

public class RepositoryDao
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public string? CurrentBranch { get; set; }
    public int AheadBy { get; set; }
    public int BehindBy { get; set; }

    /// <summary>Entry point file paths (.sln / .code-workspace)</summary>
    public List<string> EntryPoints { get; set; } = [];

    public int OpenCount { get; set; }
    public DateTime? LastOpenedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
