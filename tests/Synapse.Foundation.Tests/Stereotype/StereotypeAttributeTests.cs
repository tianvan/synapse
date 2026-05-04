using FluentAssertions;
using Synapse.Foundation.Stereotype;

namespace Synapse.Foundation.Tests.Stereotype;

public class StereotypeAttributeTests
{
    [Fact]
    public void Port_attribute_should_exist()
    {
        var attr = new PortAttribute();
        attr.Should().BeOfType<PortAttribute>();
    }

    [Fact]
    public void Adapter_attribute_should_inherit_from_attribute()
    {
        typeof(AdapterAttribute).Should().BeDerivedFrom<Attribute>();
    }

    [Fact]
    public void Port_attribute_should_target_interfaces()
    {
        var usage = Attribute.GetCustomAttribute(
            typeof(PortAttribute), typeof(AttributeUsageAttribute)) as AttributeUsageAttribute;
        usage.Should().NotBeNull();
        usage!.ValidOn.Should().Be(AttributeTargets.Interface);
    }
}
