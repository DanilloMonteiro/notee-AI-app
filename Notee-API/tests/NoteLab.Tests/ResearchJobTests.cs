using NoteLab.Domain;

namespace NoteLab.Tests;

public class ResearchJobTests
{
    // Depois da criação do job (NextAttemptAt = agora).
    private static readonly DateTimeOffset T0 = DateTimeOffset.UtcNow.AddMinutes(1);

    private static ResearchJob NewJob(int max = 3) => new(Guid.NewGuid(), "owner", null, max);

    [Fact]
    public void Claim_sets_lease_and_increments_attempt_and_fencing()
    {
        ResearchJob job = NewJob();

        job.Claim("w1", T0);

        Assert.Equal(ResearchJobStatus.Running, job.Status);
        Assert.Equal(1, job.Attempt);
        Assert.Equal(1, job.FencingToken);
        Assert.Equal(T0 + ResearchJob.LeaseDuration, job.LeaseExpiresAt);
    }

    [Fact]
    public void Running_job_with_valid_lease_cannot_be_claimed_again()
    {
        ResearchJob job = NewJob();
        job.Claim("w1", T0);

        Assert.Throws<DomainException>(() => job.Claim("w2", T0.AddSeconds(30)));
    }

    [Fact]
    public void Expired_lease_can_be_reclaimed_with_new_fencing_token()
    {
        ResearchJob job = NewJob();
        job.Claim("w1", T0);

        job.Claim("w2", T0 + ResearchJob.LeaseDuration + TimeSpan.FromSeconds(1));

        Assert.Equal("w2", job.LeaseOwner);
        Assert.Equal(2, job.FencingToken);
    }

    [Fact]
    public void Failure_retries_with_exponential_backoff_then_fails()
    {
        ResearchJob job = NewJob(max: 2);

        job.Claim("w", T0);
        Assert.True(job.FailAttempt("HttpRequestException", T0));
        Assert.Equal(ResearchJobStatus.Queued, job.Status);
        Assert.Equal(T0 + TimeSpan.FromSeconds(10), job.NextAttemptAt);

        job.Claim("w", job.NextAttemptAt);
        Assert.False(job.FailAttempt("HttpRequestException", T0));
        Assert.Equal(ResearchJobStatus.Failed, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void Backoff_doubles()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), ResearchJob.Backoff(1));
        Assert.Equal(TimeSpan.FromSeconds(20), ResearchJob.Backoff(2));
        Assert.Equal(TimeSpan.FromSeconds(40), ResearchJob.Backoff(3));
    }

    [Fact]
    public void Cancel_queued_is_immediate_running_is_requested()
    {
        ResearchJob queued = NewJob();
        Assert.True(queued.RequestCancel(T0));
        Assert.Equal(ResearchJobStatus.Cancelled, queued.Status);

        ResearchJob running = NewJob();
        running.Claim("w", T0);
        Assert.False(running.RequestCancel(T0));
        Assert.True(running.CancelRequested);
        Assert.Equal(ResearchJobStatus.Running, running.Status);
    }

    [Fact]
    public void Finished_job_cannot_be_cancelled()
    {
        ResearchJob job = NewJob();
        job.Claim("w", T0);
        job.Succeed("fake", T0);

        Assert.Throws<DomainException>(() => job.RequestCancel(T0));
    }

    [Fact]
    public void Board_doubt_has_no_anchor_and_validates_question()
    {
        Doubt doubt = Doubt.FromBoard("owner", Guid.NewGuid(), 3, "  Por quê?  ");

        Assert.Null(doubt.Anchor);
        Assert.Equal(DoubtOrigin.BoardQuestion, doubt.Origin);
        Assert.Equal("Por quê?", doubt.Question);
        Assert.Throws<DomainException>(() => Doubt.FromBoard("owner", Guid.NewGuid(), 1, " "));
    }
}
