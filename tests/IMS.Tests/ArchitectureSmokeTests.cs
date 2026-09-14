using System.Reflection;

namespace IMS.Tests;

public class ArchitectureSmokeTests
{
    [Fact]
    public void CoreAssembliesCanBeLoadedAndDomainHasNoImsDependencies()
    {
        var application = Assembly.Load("IMS.Application");
        var domain = Assembly.Load("IMS.Domain");

        Assert.Equal("IMS.Application", application.GetName().Name);
        Assert.Equal("IMS.Domain", domain.GetName().Name);
        Assert.DoesNotContain(domain.GetReferencedAssemblies(),
            reference => reference.Name?.StartsWith("IMS.", StringComparison.Ordinal) == true);
    }
}
