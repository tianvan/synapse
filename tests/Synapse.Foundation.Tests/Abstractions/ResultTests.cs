using FluentAssertions;
using Synapse.Foundation.Abstractions;

namespace Synapse.Foundation.Tests.Abstractions;

public class ResultTests
{
    [Fact]
    public void Success_should_be_successful()
    {
        var result = Result<int>.Success(42);
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_should_store_error()
    {
        var result = Result<int>.Failure("something went wrong");
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("something went wrong");
    }

    [Fact]
    public void Accessing_value_on_failure_should_throw()
    {
        var result = Result<int>.Failure("error");
        Action act = () => { var _ = result.Value; };
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Implicit_conversion_from_value()
    {
        Result<string> result = "hello";
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }
}
