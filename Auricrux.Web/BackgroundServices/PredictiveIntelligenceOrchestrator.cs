using Auricrux.Web.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Auricrux.Web.BackgroundServices;

/// <summary>
/// Phase 9A: Predictive Intelligence Orchestrator
/// Continuously monitors new outcomes and triggers cross-project knowledge transfer
/// This is the autonomous intelligence engine that makes Auricrux "see into the future"
/// </summary>
public class PredictiveIntelligenceOrchestrator : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PredictiveIntelligenceOrchestrator> _logger;
    private readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(5);

    public PredictiveIntelligenceOrchestrator(
        IServiceProvider services,
        ILogger<PredictiveIntelligenceOrchestrator> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔮 Predictive Intelligence Orchestrator started");

        // Wait for initial startup
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNewOutcomesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in predictive intelligence orchestration cycle");
            }

            try
            {
                await Task.Delay(_scanInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Predictive Intelligence Orchestrator stopped");
    }

    private async Task ProcessNewOutcomesAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var atlas = scope.ServiceProvider.GetRequiredService<AtlasService>();
        var predictive = scope.ServiceProvider.GetRequiredService<PredictiveIntelligenceService>();

        if (!atlas.IsConfigured)
        {
            _logger.LogDebug("Atlas not configured - skipping predictive intelligence cycle");
            return;
        }

        try
        {
            // Find outcomes that haven't been processed for predictive intelligence yet
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("validation_status", "verified"),
                Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Eq("predictive_processed", false),
                    Builders<BsonDocument>.Filter.Exists("predictive_processed", false)
                ),
                // Only process significant outcomes (failures, delays, major successes)
                Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Eq("status", "failed"),
                    Builders<BsonDocument>.Filter.Eq("status", "delayed"),
                    Builders<BsonDocument>.Filter.Eq("status", "success")
                )
            );

            var outcomes = await atlas.ConstructionOutcomes
                .Find(filter)
                .Limit(10) // Process in batches
                .ToListAsync(ct);

            if (outcomes.Count == 0)
            {
                _logger.LogDebug("No new verified outcomes to process for predictive intelligence");
                return;
            }

            _logger.LogInformation("🔮 Processing {Count} verified outcomes for predictive intelligence", outcomes.Count);

            foreach (var outcome in outcomes)
            {
                try
                {
                    var outcomeId = outcome["outcome_id"].AsString;
                    var projectId = GetProjectIdFromOutcome(outcome, atlas, ct).Result;

                    if (string.IsNullOrEmpty(projectId))
                    {
                        _logger.LogWarning("Cannot determine project for outcome {OutcomeId} - skipping", outcomeId);
                        await MarkOutcomeProcessedAsync(atlas, outcomeId, 0, ct);
                        continue;
                    }

                    // Trigger predictive intelligence transfer
                    var transferredCount = await predictive.PredictAndTransferKnowledgeAsync(
                        outcomeId,
                        projectId,
                        ct);

                    // Mark as processed
                    await MarkOutcomeProcessedAsync(atlas, outcomeId, transferredCount, ct);

                    _logger.LogInformation(
                        "✨ Predictive intelligence transfer completed for outcome {OutcomeId}: {Count} projects notified",
                        outcomeId,
                        transferredCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing outcome for predictive intelligence");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in predictive intelligence processing");
        }
    }

    private async Task<string> GetProjectIdFromOutcome(
        BsonDocument outcome,
        AtlasService atlas,
        CancellationToken ct)
    {
        // Try to get project ID from outcome metadata
        if (outcome.Contains("project_id"))
        {
            var projectId = outcome["project_id"].AsString;
            if (!string.IsNullOrEmpty(projectId)) return projectId;
        }

        // Fall back to event linkage
        var eventId = outcome.GetValue("event_id", BsonNull.Value);
        if (eventId == BsonNull.Value) return string.Empty;

        var evt = await atlas.ConstructionEvents
            .Find(Builders<BsonDocument>.Filter.Eq("event_id", eventId.AsString))
            .FirstOrDefaultAsync(ct);

        if (evt == null) return string.Empty;

        return evt.GetValue("project_id", "").AsString;
    }

    private async Task MarkOutcomeProcessedAsync(
        AtlasService atlas,
        string outcomeId,
        int transferCount,
        CancellationToken ct)
    {
        var update = Builders<BsonDocument>.Update
            .Set("predictive_processed", true)
            .Set("predictive_processed_at", DateTime.UtcNow)
            .Set("predictive_transfer_count", transferCount);

        await atlas.ConstructionOutcomes.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("outcome_id", outcomeId),
            update,
            cancellationToken: ct);
    }
}
