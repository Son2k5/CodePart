using Xunit;

namespace CodePath.Integration.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<CodePathApiFixture>
{
    public const string Name = "CodePath API integration tests";
}
