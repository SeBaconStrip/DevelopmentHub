using DevelopmentHub.Api.Services;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Tests.Services;

public sealed class AzureDevOpsContributorStatsProviderTests
{
    [Fact]
    public void ReadReviewers_SkipsGroupsAndReviewersWhoDidNotVote()
    {
        var pr = JsonNode.Parse("""
            {
              "reviewers": [
                { "uniqueName": "ada@example.com", "displayName": "Ada", "vote": 10 },
                { "uniqueName": "vstfs:///Classification/TeamProject/1", "displayName": "[Project]\\Project Team", "vote": 10, "isContainer": true },
                { "uniqueName": "linus@example.com", "displayName": "Linus", "vote": 0 },
                { "uniqueName": "grace@example.com", "displayName": "Grace", "vote": -5, "isContainer": false }
              ]
            }
            """)!;

        var reviewers = AzureDevOpsContributorStatsProvider.ReadReviewers(pr);

        reviewers.Select(r => r.Login).Should().Equal("ada@example.com", "grace@example.com");
    }

    [Fact]
    public void ReadReviewers_ReturnsEmpty_WhenThereAreNoReviewers()
    {
        AzureDevOpsContributorStatsProvider.ReadReviewers(JsonNode.Parse("""{ "pullRequestId": 1 }""")!)
            .Should().BeEmpty();
    }
}
