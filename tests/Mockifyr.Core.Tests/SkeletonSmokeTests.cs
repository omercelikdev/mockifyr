using Mockifyr.Core;

namespace Mockifyr.Core.Tests;

/// <summary>
/// Smoke tests that assert the skeleton is wired: the core assembly loads and the
/// StubEngine coordinator type is present. Real behavioral coverage arrives with the
/// differential suite from G1a onward.
/// </summary>
public class SkeletonSmokeTests
{
    [Fact]
    public void CoreAssembly_ExposesStubEngine()
    {
        Assert.NotNull(typeof(StubEngine).Assembly);
    }

    [Fact]
    public void TenantId_IsValueType()
    {
        var a = new TenantId("tenant-a");
        var b = new TenantId("tenant-a");
        Assert.Equal(a, b);
    }
}
