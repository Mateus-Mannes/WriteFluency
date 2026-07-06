using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using WriteFluency.Data;
using WriteFluency.TextComparisons;

namespace WriteFluency.Infrastructure.TextComparisons;

public sealed class EfAiUsageLimiter : IAiUsageLimiter
{
    private const int MaxReservationAttempts = 3;

    private readonly AppDbContext _dbContext;
    private readonly AiUsageOptions _options;
    private readonly ILogger<EfAiUsageLimiter> _logger;

    public EfAiUsageLimiter(
        AppDbContext dbContext,
        IOptions<AiUsageOptions> options,
        ILogger<EfAiUsageLimiter> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiUsageReservation> TryReserveAsync(
        AiUsageReservationRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var dailyPeriodKey = now.ToString("yyyy-MM-dd");
        var monthlyPeriodKey = now.ToString("yyyy-MM");

        if (!_options.Enabled)
        {
            return AiUsageReservation.Allowed(
                request.UserId,
                request.Feature,
                dailyPeriodKey,
                monthlyPeriodKey);
        }

        return await _dbContext.Database
            .CreateExecutionStrategy()
            .ExecuteAsync(async () =>
            {
                for (var attempt = 1; attempt <= MaxReservationAttempts; attempt++)
                {
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        var dailyCounter = await GetOrCreateLockedCounterAsync(
                            request.UserId,
                            request.Feature,
                            AiUsagePeriodKinds.Day,
                            dailyPeriodKey,
                            now,
                            cancellationToken);
                        var monthlyCounter = await GetOrCreateLockedCounterAsync(
                            request.UserId,
                            request.Feature,
                            AiUsagePeriodKinds.Month,
                            monthlyPeriodKey,
                            now,
                            cancellationToken);

                        var denial = GetLimitDenial(
                            request,
                            dailyPeriodKey,
                            monthlyPeriodKey,
                            dailyCounter,
                            monthlyCounter);
                        if (denial is not null)
                        {
                            await transaction.CommitAsync(cancellationToken);
                            return denial;
                        }

                        dailyCounter.ReservedRequestCount++;
                        monthlyCounter.ReservedRequestCount++;
                        dailyCounter.UpdatedAtUtc = now;
                        monthlyCounter.UpdatedAtUtc = now;

                        await _dbContext.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);

                        return AiUsageReservation.Allowed(
                            request.UserId,
                            request.Feature,
                            dailyPeriodKey,
                            monthlyPeriodKey);
                    }
                    catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < MaxReservationAttempts)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _dbContext.ChangeTracker.Clear();
                    }
                }

                throw new InvalidOperationException(
                    "Unable to reserve AI usage after retrying concurrent counter creation.");
            });
    }

    public Task RecordCompletionAsync(
        AiUsageReservation reservation,
        AiUsageCompletion completion,
        CancellationToken cancellationToken) =>
        RecordOutcomeAsync(reservation, completion, isFailure: false, cancellationToken);

    public Task RecordFailureAsync(
        AiUsageReservation reservation,
        CancellationToken cancellationToken) =>
        RecordOutcomeAsync(reservation, completion: null, isFailure: true, cancellationToken);

    private async Task RecordOutcomeAsync(
        AiUsageReservation reservation,
        AiUsageCompletion? completion,
        bool isFailure,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !reservation.IsAllowed)
        {
            return;
        }

        try
        {
            await _dbContext.Database
                .CreateExecutionStrategy()
                .ExecuteAsync(async () =>
                {
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                    var now = DateTimeOffset.UtcNow;
                    var dailyCounter = await GetOrCreateLockedCounterAsync(
                        reservation.UserId,
                        reservation.Feature,
                        AiUsagePeriodKinds.Day,
                        reservation.DailyPeriodKey,
                        now,
                        cancellationToken);
                    var monthlyCounter = await GetOrCreateLockedCounterAsync(
                        reservation.UserId,
                        reservation.Feature,
                        AiUsagePeriodKinds.Month,
                        reservation.MonthlyPeriodKey,
                        now,
                        cancellationToken);

                    ApplyOutcome(dailyCounter, completion, isFailure, now);
                    ApplyOutcome(monthlyCounter, completion, isFailure, now);

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _dbContext.ChangeTracker.Clear();
            _logger.LogWarning(
                ex,
                "Unable to record AI usage outcome. UserId={UserId}, Feature={Feature}, IsFailure={IsFailure}",
                reservation.UserId,
                reservation.Feature,
                isFailure);
        }
    }

    private async Task<AiUsageCounter> GetOrCreateLockedCounterAsync(
        string userId,
        string feature,
        string periodKind,
        string periodKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var counter = await _dbContext.AiUsageCounters
            .FromSqlInterpolated(
                $"""
                 SELECT * FROM "AiUsageCounters"
                 WHERE "UserId" = {userId}
                   AND "Feature" = {feature}
                   AND "PeriodKind" = {periodKind}
                   AND "PeriodKey" = {periodKey}
                 FOR UPDATE
                 """)
            .SingleOrDefaultAsync(cancellationToken);

        if (counter is not null)
        {
            return counter;
        }

        counter = new AiUsageCounter
        {
            UserId = userId,
            Feature = feature,
            PeriodKind = periodKind,
            PeriodKey = periodKey,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await _dbContext.AiUsageCounters.AddAsync(counter, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return counter;
    }

    private AiUsageReservation? GetLimitDenial(
        AiUsageReservationRequest request,
        string dailyPeriodKey,
        string monthlyPeriodKey,
        AiUsageCounter dailyCounter,
        AiUsageCounter monthlyCounter)
    {
        if (dailyCounter.ReservedRequestCount >= _options.DailySubmissionLimit)
        {
            return AiUsageReservation.Denied(
                "daily_limit_exceeded",
                request.UserId,
                request.Feature,
                dailyPeriodKey,
                monthlyPeriodKey);
        }

        if (monthlyCounter.ReservedRequestCount >= _options.MonthlySubmissionLimit)
        {
            return AiUsageReservation.Denied(
                "monthly_limit_exceeded",
                request.UserId,
                request.Feature,
                dailyPeriodKey,
                monthlyPeriodKey);
        }

        if (monthlyCounter.EstimatedCostUsd >= _options.MonthlyEstimatedCostLimitUsd)
        {
            return AiUsageReservation.Denied(
                "monthly_cost_limit_exceeded",
                request.UserId,
                request.Feature,
                dailyPeriodKey,
                monthlyPeriodKey);
        }

        return null;
    }

    private void ApplyOutcome(
        AiUsageCounter counter,
        AiUsageCompletion? completion,
        bool isFailure,
        DateTimeOffset now)
    {
        if (isFailure)
        {
            counter.FailedRequestCount++;
        }
        else
        {
            counter.CompletedRequestCount++;
            counter.InputTokenCount += completion?.InputTokenCount ?? 0;
            counter.OutputTokenCount += completion?.OutputTokenCount ?? 0;
            counter.EstimatedCostUsd += EstimateCostUsd(completion);
        }

        counter.UpdatedAtUtc = now;
    }

    private decimal EstimateCostUsd(AiUsageCompletion? completion)
    {
        if (completion is null)
        {
            return 0;
        }

        return ((completion.InputTokenCount ?? 0) / 1_000_000m * _options.InputUsdPerMillionTokens)
            + ((completion.OutputTokenCount ?? 0) / 1_000_000m * _options.OutputUsdPerMillionTokens);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
}
