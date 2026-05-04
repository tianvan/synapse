using FluentAssertions;
using ApplicationException = Synapse.Foundation.Exception.ApplicationException;
using DomainException = Synapse.Foundation.Exception.DomainException;

namespace Synapse.Foundation.Tests.Exception;

public class ExceptionTests
{
    [Fact]
    public void ApplicationException_should_store_message()
    {
        var ex = new ApplicationException("test error");
        ex.Message.Should().Be("test error");
    }

    [Fact]
    public void DomainException_should_store_message()
    {
        var ex = new DomainException("invalid state");
        ex.Message.Should().Be("invalid state");
    }
}
