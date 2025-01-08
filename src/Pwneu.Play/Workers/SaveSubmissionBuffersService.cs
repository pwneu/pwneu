using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Workers;

/// <summary>
/// Worker for saving submission buffers.
/// </summary>
/// <param name="serviceProvider">The service provider.</param>
/// <param name="logger">The logger.</param>
public class SaveSubmissionBuffersService(
    IServiceProvider serviceProvider,
    ILogger<SaveSubmissionBuffersService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SaveSubmissionBuffers(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError("Something went wrong saving submission buffers {Message}", ex.Message);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }

    /// <summary>
    /// Saves submission buffers in the actual database.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>If there are submission buffers found.</returns>
    private async Task SaveSubmissionBuffers(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var bufferDbContext = scope.ServiceProvider.GetRequiredService<BufferDbContext>();

        var submissionBuffers = await bufferDbContext
            .SubmissionBuffers
            .ToListAsync(cancellationToken);

        if (submissionBuffers.Count == 0)
            return;

        logger.LogInformation("Saving {Count} submission buffer(s)...", submissionBuffers.Count);

        var appDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IFusionCache>();
        var memberAccess = scope.ServiceProvider.GetRequiredService<IMemberAccess>();

        var memberIds = await memberAccess.GetMemberIdsAsync(cancellationToken);

        var challengeIds = await cache.GetOrSetAsync(Keys.ChallengeIds(), async _ =>
                await appDbContext
                    .Challenges
                    .Select(ch => ch.Id)
                    .ToListAsync(cancellationToken),
            new FusionCacheEntryOptions { Duration = TimeSpan.FromSeconds(30) },
            cancellationToken);

        logger.LogInformation("Challenges count: ({ChallengeIdsCount})", challengeIds.Count);

        // Remove all invalid submissions.
        var validSubmissions = submissionBuffers
            .Where(sb => memberIds.Contains(sb.UserId) && challengeIds.Contains(sb.ChallengeId))
            .Select(sb => new Submission
            {
                Id = sb.Id,
                UserId = sb.UserId,
                UserName = sb.UserName,
                ChallengeId = sb.ChallengeId,
                Flag = sb.Flag,
                SubmittedAt = sb.SubmittedAt,
            })
            .ToList();

        appDbContext.AddRange(validSubmissions);

        await appDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Saved {Count} submission(s)", validSubmissions.Count);

        // Clear the buffered submissions.
        bufferDbContext.SubmissionBuffers.RemoveRange(submissionBuffers);

        await bufferDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Cleared the stored submission buffer(s)");
    }
}