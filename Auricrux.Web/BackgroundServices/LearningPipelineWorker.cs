using Auricrux.Web.Services;

namespace Auricrux.Web.BackgroundServices;

/// <summary>
/// Background worker for automated learning pipeline analysis.
/// Runs weekly analysis, generates auto-proposals, and stores quality metrics.
/// Implements IHostedService for scheduled execution.
/// </summary>
public sealed class LearningPipelineWorker : IHostedService, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger<LearningPipelineWorker> _logger;
    private Timer? _timer;

    public LearningPipelineWorker(
        IServiceProvider services,
        ILogger<LearningPipelineWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Learning Pipeline Worker starting");

        // Run weekly on Sunday at midnight UTC
        var now = DateTime.UtcNow;
        var nextSunday = now.AddDays((7 - (int)now.DayOfWeek) % 7);
        var nextRun = nextSunday.Date; // Midnight
        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(7);
        }

        var delay = nextRun - now;

        _logger.LogInformation("Learning Pipeline Worker scheduled for: {NextRun} (in {Delay})",
            nextRun, delay);

        _timer = new Timer(
            ExecuteAsync,
            null,
            delay,
            TimeSpan.FromDays(7)); // Run weekly

        return Task.CompletedTask;
    }

    private async void ExecuteAsync(object? state)
    {
        _logger.LogInformation("Learning Pipeline Worker executing weekly analysis");

        try
        {
            using var scope = _services.CreateScope();
            var improvementService = scope.ServiceProvider.GetRequiredService<ContinuousImprovementService>();

            // Run weekly analysis
            var analysisReport = await improvementService.RunWeeklyAnalysisAsync(CancellationToken.None);
            if (analysisReport.Success)
            {
                _logger.LogInformation("Weekly analysis complete: {Interactions} interactions, {Gaps} gaps, {Approvals} approvals",
                    analysisReport.TotalInteractions,
                    analysisReport.NewKnowledgeGaps,
                    analysisReport.CorpusEntriesApproved);
            }
            else
            {
                _logger.LogWarning("Weekly analysis failed: {Error}", analysisReport.Error);
            }

            // Generate auto-proposals from high-confidence gaps
            var autoProposalResult = await improvementService.GenerateAutoProposalsAsync(CancellationToken.None);
            if (autoProposalResult.Success)
            {
                _logger.LogInformation("Auto-proposals generated: {Count} proposals from {Gaps} high-confidence gaps",
                    autoProposalResult.ProposalsCreated,
                    autoProposalResult.HighConfidenceGaps);
            }
            else
            {
                _logger.LogWarning("Auto-proposal generation failed: {Error}", autoProposalResult.Error);
            }

            // TODO: Send email/notification with improvement report
            // This would require email service configuration
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Learning Pipeline Worker execution failed");
        }
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Learning Pipeline Worker stopping");

        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
