using Mihon.ExtensionsBridge.Models.Extensions;
using RensaioBackend.Services.Series;
using Xunit;

namespace RensaioBackend.Paging.Tests;

public class LatestPagingPolicyTests
{
    [Theory]
    [InlineData(1, 20, true, false, false, true)]
    [InlineData(9, 20, true, false, false, true)]
    [InlineData(10, 20, true, false, false, false)]
    [InlineData(11, 20, true, false, false, false)]
    [InlineData(1, 0, true, false, false, false)]
    [InlineData(1, 20, false, false, false, false)]
    [InlineData(1, 20, true, true, false, false)]
    [InlineData(1, 20, true, false, true, false)]
    public void ContinuationHonorsEveryStopCondition(int page, int count, bool hasNextPage,
        bool upToDate, bool neverDone, bool expected)
    {
        Assert.Equal(expected, LatestPagingPolicy.ShouldFetchNextPage(
            page, count, hasNextPage, upToDate, neverDone));
    }

    [Theory]
    [InlineData(20, true, false, false, 10)]
    [InlineData(0, true, false, false, 1)]
    [InlineData(20, false, false, false, 1)]
    [InlineData(20, true, true, false, 1)]
    [InlineData(20, true, false, true, 1)]
    public void RealMangaListPagesHaveBoundedRequests(int count, bool hasNextPage,
        bool upToDate, bool neverDone, int expectedRequests)
    {
        // A policy-level loop, not an integration test of UpdateSourceAsync/database writes.
        var result = new MangaList
        {
            Mangas = Enumerable.Range(0, count).Select(i => new ParsedManga
            {
                Url = $"/manga/{i}", Title = $"Manga {i}"
            }).ToList(),
            HasNextPage = hasNextPage
        };
        int requests = 0;
        bool fetchNext;
        do
        {
            requests++;
            fetchNext = LatestPagingPolicy.ShouldFetchNextPage(requests,
                result.Mangas.Count, result.HasNextPage, upToDate, neverDone);
            Assert.True(requests <= LatestPagingPolicy.MaxPagesPerRun);
        } while (fetchNext);

        Assert.Equal(expectedRequests, requests);
    }
}
