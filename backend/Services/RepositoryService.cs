using DevelopmentHub.Api.Configuration;
using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Models;
using DevelopmentHub.Api.Models.Dtos;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace DevelopmentHub.Api.Services;

public interface IRepositoryService
{
    Task<List<RepositoryDto>> GetAllAsync();
    Task<List<RepositoryDto>> ScanAsync();
    Task<RepositoryDto?> ToggleFavoriteAsync(string id);
    Task<RepositoryDto?> OpenAsync(string id, OpenRepositoryRequest request);
    Task<(bool Success, string Output)> SyncAsync(string id, CancellationToken cancellationToken);
}

public class RepositoryService(
    DashboardDatabase db,
    IGitService gitService,
    ILauncherService launcher,
    IOptions<AppSettings> settings,
    ILogger<RepositoryService> logger) : IRepositoryService
{
    private readonly AppSettings _settings = settings.Value;

    public async Task<List<RepositoryDto>> GetAllAsync()
    {
        var entities = await db.Repositories.Find(_ => true).ToListAsync();

        return entities
            .Select(MapToDto)
            .OrderByDescending(r => r.IsFavorite)
            .ThenByDescending(r => r.UsageScore)
            .ToList();
    }

    public async Task<List<RepositoryDto>> ScanAsync()
    {
        logger.LogInformation("Starting repository scan across {Count} root(s)", _settings.RepositoryRoots.Length);

        var discovered = await gitService.ScanDirectoriesAsync(
            _settings.RepositoryRoots,
            _settings.EntryPointMaxDepth);

        var now = DateTime.UtcNow;

        foreach (var found in discovered)
        {
            var filter = Builders<RepositoryEntity>.Filter.Eq(r => r.Path, found.Path);
            var existing = await db.Repositories.Find(filter).FirstOrDefaultAsync();

            if (existing is null)
            {
                found.CreatedAt = now;
                found.LastSeenAt = now;
                await db.Repositories.InsertOneAsync(found);
            }
            else
            {
                var update = Builders<RepositoryEntity>.Update
                    .Set(r => r.Name, found.Name)
                    .Set(r => r.CurrentBranch, found.CurrentBranch)
                    .Set(r => r.AheadBy, found.AheadBy)
                    .Set(r => r.BehindBy, found.BehindBy)
                    .Set(r => r.EntryPoints, found.EntryPoints)
                    .Set(r => r.LastSeenAt, now);
                await db.Repositories.UpdateOneAsync(filter, update);
            }
        }

        logger.LogInformation("Scan complete. Found {Count} repositories.", discovered.Count);
        return await GetAllAsync();
    }

    public async Task<RepositoryDto?> ToggleFavoriteAsync(string id)
    {
        var filter = Builders<RepositoryEntity>.Filter.Eq(r => r.Id, id);
        var entity = await db.Repositories.Find(filter).FirstOrDefaultAsync();
        if (entity is null) return null;

        var update = Builders<RepositoryEntity>.Update.Set(r => r.IsFavorite, !entity.IsFavorite);
        await db.Repositories.UpdateOneAsync(filter, update);
        entity.IsFavorite = !entity.IsFavorite;
        return MapToDto(entity);
    }

    public async Task<RepositoryDto?> OpenAsync(string id, OpenRepositoryRequest request)
    {
        var filter = Builders<RepositoryEntity>.Filter.Eq(r => r.Id, id);
        var entity = await db.Repositories.Find(filter).FirstOrDefaultAsync();
        if (entity is null) return null;

        if (!IsUnderKnownRoot(entity.Path))
        {
            logger.LogWarning("Blocked open for path outside known roots: {Path}", entity.Path);
            return null;
        }

        bool launched;

        if (request.OpenWith == OpenWith.VisualStudio && request.EntryPointPath is not null)
        {
            launched = await launcher.OpenWithVisualStudioAsync(request.EntryPointPath);
        }
        else if (request.EntryPointPath is not null)
        {
            launched = await launcher.OpenWithVsCodeAsync(request.EntryPointPath);
        }
        else
        {
            launched = await launcher.OpenWithVsCodeAsync(entity.Path);
        }

        if (launched)
        {
            var now = DateTime.UtcNow;
            var update = Builders<RepositoryEntity>.Update
                .Inc(r => r.OpenCount, 1)
                .Set(r => r.LastOpenedAt, now);
            await db.Repositories.UpdateOneAsync(filter, update);
            entity.OpenCount++;
            entity.LastOpenedAt = now;
        }

        return MapToDto(entity);
    }

    public async Task<(bool Success, string Output)> SyncAsync(string id, CancellationToken cancellationToken)
    {
        var filter = Builders<RepositoryEntity>.Filter.Eq(r => r.Id, id);
        var entity = await db.Repositories.Find(filter).FirstOrDefaultAsync();
        if (entity is null) return (false, "Repository not found.");

        if (!IsUnderKnownRoot(entity.Path))
            return (false, "Path is outside known repository roots.");

        var (success, output) = await gitService.SyncRepositoryAsync(entity.Path, cancellationToken);

        if (success)
        {
            var (branch, ahead, behind) = await gitService.GetBranchStatusAsync(entity.Path);
            var update = Builders<RepositoryEntity>.Update
                .Set(r => r.LastSyncedAt, DateTime.UtcNow)
                .Set(r => r.CurrentBranch, branch)
                .Set(r => r.AheadBy, ahead)
                .Set(r => r.BehindBy, behind);
            await db.Repositories.UpdateOneAsync(filter, update);
        }

        return (success, output);
    }

    private bool IsUnderKnownRoot(string path)
    {
        return _settings.RepositoryRoots.Any(root =>
            path.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    private static RepositoryDto MapToDto(RepositoryEntity entity)
    {
        var entryPoints = entity.EntryPoints.Select(p => new EntryPointDto
        {
            FilePath = p,
            FileName = Path.GetFileName(p),
            Type = Path.GetExtension(p).ToLowerInvariant() switch
            {
                ".sln" => EntryPointType.Solution,
                ".code-workspace" => EntryPointType.CodeWorkspace,
                _ => EntryPointType.Folder
            }
        }).ToList();

        var daysSinceOpen = entity.LastOpenedAt.HasValue
            ? (DateTime.UtcNow - entity.LastOpenedAt.Value).TotalDays
            : double.MaxValue;

        var usageScore = entity.OpenCount + (daysSinceOpen < 7 ? 10.0 : 0.0);

        return new RepositoryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Path = entity.Path,
            IsFavorite = entity.IsFavorite,
            CurrentBranch = entity.CurrentBranch,
            AheadBy = entity.AheadBy,
            BehindBy = entity.BehindBy,
            EntryPoints = entryPoints,
            OpenCount = entity.OpenCount,
            LastOpenedAt = entity.LastOpenedAt,
            LastSyncedAt = entity.LastSyncedAt,
            UsageScore = usageScore
        };
    }
}
