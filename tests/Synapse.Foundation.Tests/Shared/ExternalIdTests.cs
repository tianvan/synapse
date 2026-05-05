using FluentAssertions;
using Synapse.Foundation.Shared;

namespace Synapse.Foundation.Tests.Shared;

public class ExternalIdTests
{
    [Fact]
    public void Should_store_value()
    {
        var id = new ExternalId("github:rust-lang/rust");
        id.Value.Should().Be("github:rust-lang/rust");
    }

    [Fact]
    public void Same_value_should_be_equal()
    {
        var a = new ExternalId("hn:12345");
        var b = new ExternalId("hn:12345");
        a.Should().Be(b);
    }

    [Fact]
    public void Different_value_should_not_be_equal()
    {
        var a = new ExternalId("hn:12345");
        var b = new ExternalId("hn:67890");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Should_reject_null_or_empty()
    {
        Action act = () => new ExternalId("");
        act.Should().Throw<ArgumentException>();
    }
}
