using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using WriteFluency.Data;

namespace WriteFluency.Propositions;

public sealed class CatalogAccessTeaserService
{
    private const string AnonymousSubjectType = "anonymous";
    private const string UserSubjectType = "user";
    private const string FreeWindowSource = "free_window";
    private const string ProSource = "pro";

    private readonly IAppDbContext _context;
    private readonly CatalogAccessTeaserOptions _options;

    public CatalogAccessTeaserService(
        IAppDbContext context,
        IOptions<CatalogAccessTeaserOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public async Task<CatalogAccessDecision> DecidePreviewAsync(
        PropositionAccessContext accessContext,
        int propositionId,
        bool requiresPro,
        CancellationToken cancellationToken)
    {
        if (!requiresPro)
        {
            return CatalogAccessDecision.AllowPreview(CatalogAccessStatuses.GrantedFreeWindow);
        }

        if (accessContext.IsPro)
        {
            return CatalogAccessDecision.AllowPreview(CatalogAccessStatuses.GrantedPro);
        }

        var subject = ResolveSubject(accessContext);
        if (subject is null)
        {
            return CatalogAccessDecision.Deny(CatalogAccessStatuses.LoginRequiredToUnlockExercise);
        }

        if (await HasGrantAsync(subject, propositionId, cancellationToken))
        {
            return CatalogAccessDecision.AllowPreview(CatalogAccessStatuses.GrantedCatalogTeaser);
        }

        if (!_options.Enabled)
        {
            return DenyForSubject(subject);
        }

        var quota = GetQuota(subject);
        var usedCount = await GetUsedCountAsync(subject, quota.Feature, cancellationToken);
        return usedCount < quota.Limit
            ? CatalogAccessDecision.AllowPreview(GetPreviewAvailableStatus(subject))
            : DenyForSubject(subject);
    }

    public async Task<CatalogAccessDecision> ClaimBeginAsync(
        PropositionAccessContext accessContext,
        int propositionId,
        bool requiresPro,
        CancellationToken cancellationToken)
    {
        if (!requiresPro)
        {
            return CatalogAccessDecision.AllowBegin(CatalogAccessStatuses.Granted, FreeWindowSource);
        }

        if (accessContext.IsPro)
        {
            return CatalogAccessDecision.AllowBegin(CatalogAccessStatuses.Granted, ProSource);
        }

        var subject = ResolveSubject(accessContext);
        if (subject is null)
        {
            return CatalogAccessDecision.Deny(CatalogAccessStatuses.LoginRequiredToUnlockExercise);
        }

        if (await HasGrantAsync(subject, propositionId, cancellationToken))
        {
            return CatalogAccessDecision.AllowBegin(CatalogAccessStatuses.Granted, CatalogAccessStatuses.GrantedCatalogTeaser);
        }

        if (!_options.Enabled)
        {
            return DenyForSubject(subject);
        }

        if (_context is not DbContext dbContext)
        {
            return await ClaimBeginForSubjectAsync(subject, propositionId, cancellationToken);
        }

        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var decision = await ClaimBeginForSubjectAsync(subject, propositionId, cancellationToken);

            if (decision.AllowsAudio)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return decision;
        });
    }

    private async Task<CatalogAccessDecision> ClaimBeginForSubjectAsync(
        SubjectIdentity subject,
        int propositionId,
        CancellationToken cancellationToken)
    {
        if (await HasGrantAsync(subject, propositionId, cancellationToken))
        {
            return CatalogAccessDecision.AllowBegin(CatalogAccessStatuses.Granted, CatalogAccessStatuses.GrantedCatalogTeaser);
        }

        if (!_options.Enabled)
        {
            return DenyForSubject(subject);
        }

        var quota = GetQuota(subject);
        var now = DateTimeOffset.UtcNow;
        var counter = await GetOrCreateCounterAsync(subject, quota.Feature, now, cancellationToken);
        if (counter.UsedCount >= quota.Limit)
        {
            return DenyForSubject(subject);
        }

        counter.UsedCount++;
        counter.UpdatedAtUtc = now;
        _context.CatalogExerciseGrants.Add(new CatalogExerciseGrant
        {
            SubjectType = subject.SubjectType,
            SubjectKey = subject.SubjectKey,
            AnonymousClientIpAddress = subject.AnonymousClientIpAddress,
            PropositionId = propositionId,
            Source = quota.Feature,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await _context.SaveChangesAsync(cancellationToken);
        return CatalogAccessDecision.AllowBegin(CatalogAccessStatuses.Granted, quota.Feature);
    }

    public async Task<bool> CanCompareAsync(
        PropositionAccessContext accessContext,
        int propositionId,
        bool requiresPro,
        CancellationToken cancellationToken)
    {
        if (!requiresPro || accessContext.IsPro)
        {
            return true;
        }

        var subject = ResolveSubject(accessContext);
        return subject is not null
            && await HasGrantAsync(subject, propositionId, cancellationToken);
    }

    private SubjectIdentity? ResolveSubject(PropositionAccessContext accessContext)
    {
        if (accessContext.IsAuthenticated)
        {
            return string.IsNullOrWhiteSpace(accessContext.UserId)
                ? null
                : new SubjectIdentity(UserSubjectType, accessContext.UserId, AnonymousClientIpAddress: null);
        }

        return string.IsNullOrWhiteSpace(accessContext.AnonymousFingerprintHash)
            ? null
            : new SubjectIdentity(
                AnonymousSubjectType,
                accessContext.AnonymousFingerprintHash,
                accessContext.AnonymousClientIpAddress);
    }

    private async Task<bool> HasGrantAsync(
        SubjectIdentity subject,
        int propositionId,
        CancellationToken cancellationToken) =>
        await _context.CatalogExerciseGrants.AnyAsync(grant =>
            grant.SubjectType == subject.SubjectType
            && grant.SubjectKey == subject.SubjectKey
            && grant.PropositionId == propositionId,
            cancellationToken);

    private async Task<int> GetUsedCountAsync(
        SubjectIdentity subject,
        string feature,
        CancellationToken cancellationToken) =>
        await _context.CatalogAccessCounters
            .Where(counter =>
                counter.SubjectType == subject.SubjectType
                && counter.SubjectKey == subject.SubjectKey
                && counter.Feature == feature)
            .Select(counter => counter.UsedCount)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<CatalogAccessCounter> GetOrCreateCounterAsync(
        SubjectIdentity subject,
        string feature,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var counter = await _context.CatalogAccessCounters.SingleOrDefaultAsync(item =>
            item.SubjectType == subject.SubjectType
            && item.SubjectKey == subject.SubjectKey
            && item.Feature == feature,
            cancellationToken);
        if (counter is not null)
        {
            return counter;
        }

        counter = new CatalogAccessCounter
        {
            SubjectType = subject.SubjectType,
            SubjectKey = subject.SubjectKey,
            AnonymousClientIpAddress = subject.AnonymousClientIpAddress,
            Feature = feature,
            UsedCount = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _context.CatalogAccessCounters.Add(counter);
        return counter;
    }

    private Quota GetQuota(SubjectIdentity subject) =>
        subject.SubjectType == AnonymousSubjectType
            ? new Quota(CatalogAccessFeatures.AnonymousSample, _options.AnonymousSampleLifetimeLimit)
            : new Quota(CatalogAccessFeatures.FreeIntro, _options.FreeIntroLifetimeLimit);

    private static string GetPreviewAvailableStatus(SubjectIdentity subject) =>
        subject.SubjectType == AnonymousSubjectType
            ? CatalogAccessStatuses.PreviewAvailableAnonymousSample
            : CatalogAccessStatuses.PreviewAvailableFreeIntro;

    private static CatalogAccessDecision DenyForSubject(SubjectIdentity subject) =>
        subject.SubjectType == AnonymousSubjectType
            ? CatalogAccessDecision.Deny(CatalogAccessStatuses.LoginRequiredToUnlockExercise)
            : CatalogAccessDecision.Deny(CatalogAccessStatuses.UpgradeRequiredToUnlockExercise);

    private sealed record SubjectIdentity(
        string SubjectType,
        string SubjectKey,
        string? AnonymousClientIpAddress);
    private sealed record Quota(string Feature, int Limit);
}

public sealed record CatalogAccessDecision(
    string AccessStatus,
    bool AllowsAudio,
    string? GrantSource)
{
    public static CatalogAccessDecision AllowPreview(string accessStatus) =>
        new(accessStatus, AllowsAudio: true, GrantSource: null);

    public static CatalogAccessDecision AllowBegin(string accessStatus, string grantSource) =>
        new(accessStatus, AllowsAudio: true, grantSource);

    public static CatalogAccessDecision Deny(string accessStatus) =>
        new(accessStatus, AllowsAudio: false, GrantSource: null);
}
