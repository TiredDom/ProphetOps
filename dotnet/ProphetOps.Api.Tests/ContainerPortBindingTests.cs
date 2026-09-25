using ProphetOps.Api;
using Xunit;

namespace ProphetOps.Api.Tests;

public class ContainerPortBindingTests
{
    [Fact]
    public void Port_environment_variable_binds_to_container_interface()
    {
        Assert.Equal("http://0.0.0.0:8080", ContainerPortBinding.BuildUrl("8080"));
    }

    [Fact]
    public void Missing_port_preserves_local_default_binding()
    {
        Assert.Null(ContainerPortBinding.BuildUrl(null));
        Assert.Null(ContainerPortBinding.BuildUrl(""));
        Assert.Null(ContainerPortBinding.BuildUrl("   "));
    }

    [Fact]
    public void Explicit_urls_configuration_wins_over_port()
    {
        Assert.Null(ContainerPortBinding.BuildUrl("8080", "http://127.0.0.1:5099"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public void Invalid_port_fails_startup(string port)
    {
        var error = Assert.Throws<InvalidOperationException>(() => ContainerPortBinding.BuildUrl(port));
        Assert.Contains("PORT", error.Message);
    }
}
