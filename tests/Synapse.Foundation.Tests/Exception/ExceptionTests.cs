using FluentAssertions;
using AppException = Synapse.Foundation.Exception.AppException;
using DomainException = Synapse.Foundation.Exception.DomainException;

namespace Synapse.Foundation.Tests.Exception;

public class ExceptionTests
{
    [Fact]
    public void AppException_should_store_message()
    {
        var ex = new AppException("test error");
        ex.Message.Should().Be("test error");
    }

    [Fact]
    public void DomainException_should_store_message()
    {
        var ex = new DomainException("invalid state");
        ex.Message.Should().Be("invalid state");
    }

    [Fact]
    public void AppException_should_be_a_System_Exception()
    {
        typeof(AppException).Should().BeDerivedFrom<System.Exception>();
    }

    [Fact]
    public void DomainException_should_be_a_System_Exception()
    {
        typeof(DomainException).Should().BeDerivedFrom<System.Exception>();
    }
}
