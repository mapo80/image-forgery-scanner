using System;
using FluentAssertions;
using ImageForensics.Core.Algorithms;
using ImageForensics.Core.Models;
using Xunit;

namespace ImageForensics.Tests;

public class DecisionEngineTests
{
    private static ForensicsOptions DefaultOptions => new()
    {
        ElaWeight = 1.0,
        CopyMoveWeight = 1.0,
        SplicingWeight = 1.0,
        InpaintingWeight = 1.0,
        ExifWeight = 1.0,
        CleanThreshold = 0.3,
        TamperedThreshold = 0.7
    };

    private static ForensicsResult MakeResult(double score)
        => new ForensicsResult(score, Array.Empty<byte>(), string.Empty, score, Array.Empty<byte>(), string.Empty)
        {
            SplicingScore = score,
            InpaintingScore = score,
            ExifScore = score
        };

    [Fact]
    public void Low_scores_are_clean()
    {
        var res = MakeResult(0.05);
        var (total, verdict) = DecisionEngine.Decide(res, DefaultOptions);
        verdict.Should().Be("Clean");
        total.Should().BeLessThan(DefaultOptions.CleanThreshold);
    }

    [Fact]
    public void Mid_scores_are_suspicious()
    {
        var res = MakeResult(0.12);
        var (total, verdict) = DecisionEngine.Decide(res, DefaultOptions);
        verdict.Should().Be("Suspicious");
        total.Should().BeGreaterThanOrEqualTo(DefaultOptions.CleanThreshold);
        total.Should().BeLessThan(DefaultOptions.TamperedThreshold);
    }

    [Fact]
    public void High_scores_are_tampered()
    {
        var res = MakeResult(0.9);
        var (total, verdict) = DecisionEngine.Decide(res, DefaultOptions);
        verdict.Should().Be("Tampered");
        total.Should().BeGreaterThanOrEqualTo(DefaultOptions.TamperedThreshold);
    }
}
