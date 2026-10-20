using AccessRequestHub.Api.Domain;

namespace AccessRequestHub.Tests;

public class ApprovalPolicyTests
{
    [Theory]
    [InlineData(RequestEnvironment.NonProduction, AccessLevel.Read, false)]
    [InlineData(RequestEnvironment.Production, AccessLevel.Read, true)]
    [InlineData(RequestEnvironment.NonProduction, AccessLevel.Admin, true)]
    [InlineData(RequestEnvironment.Production, AccessLevel.Admin, true)]
    public void IsHighRisk_MatchesMatrix(RequestEnvironment env, AccessLevel level, bool expected)
    {
        var result = ApprovalPolicy.IsHighRisk("v1", env, level);
        Assert.Equal(expected, result);
    }
}
