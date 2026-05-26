using FluentAssertions;
using Main.Features.Apps.Domain;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Apps;

public sealed class InMemoryOAuthStateStoreTests
{
    private static readonly TenantId TenantId = TenantId.NewId();
    private static readonly UserId UserId = UserId.NewId();
    private static readonly AppSlug Slug = new("test-app");

    [Fact]
    public void Issue_ThenConsume_ShouldReturnEntry()
    {
        var time = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var store = new InMemoryOAuthStateStore(time);
        var entry = new OAuthStateEntry(TenantId, UserId, Slug);

        var token = store.Issue(entry);

        var consumed = store.Consume(token);
        consumed.Should().Be(entry);
    }

    [Fact]
    public void Consume_Twice_ShouldReturnNullOnSecondCall()
    {
        var time = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var store = new InMemoryOAuthStateStore(time);
        var token = store.Issue(new OAuthStateEntry(TenantId, UserId, Slug));

        store.Consume(token).Should().NotBeNull();
        store.Consume(token).Should().BeNull();
    }

    [Fact]
    public void Consume_AfterTtl_ShouldReturnNull()
    {
        var time = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var store = new InMemoryOAuthStateStore(time);
        var token = store.Issue(new OAuthStateEntry(TenantId, UserId, Slug));

        time.Advance(IOAuthStateStore.StateTtl + TimeSpan.FromSeconds(1));

        store.Consume(token).Should().BeNull();
    }

    [Fact]
    public void Consume_UnknownToken_ShouldReturnNull()
    {
        var store = new InMemoryOAuthStateStore(new MutableTimeProvider(DateTimeOffset.UtcNow));
        store.Consume("does-not-exist").Should().BeNull();
    }

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }

        public void Advance(TimeSpan delta)
        {
            _now = _now.Add(delta);
        }
    }
}
