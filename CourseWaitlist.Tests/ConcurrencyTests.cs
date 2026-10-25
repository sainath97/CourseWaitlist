using System.Collections.Concurrent;
using CourseWaitlist.Core;
using CourseWaitlist.Tests.Support;
using FluentAssertions;

namespace CourseWaitlist.Tests;

public class ConcurrencyTests
{
    [Fact]
    [Trait("Slice", "C9")]
    [Trait("Case", "T-39")]
    public void Enroll_DistinctLearnersConcurrently_PreservesCapacityAndUniquePositions()
    {
        for (var iteration = 0; iteration < 30; iteration++)
        {
            // Arrange
            var service = new TestServiceBuilder()
                .WithCapacity(10)
                .WithPrerequisite(null)
                .Build();
            var learnerIds = Enumerable.Range(1, 100)
                .Select(index => $"L{index:D3}")
                .ToArray();
            var results = new EnrollResult[learnerIds.Length];
            var actions = learnerIds
                .Select((learnerId, index) => (Action)(() => results[index] = service.Enroll(learnerId)))
                .ToArray();

            // Act
            var exceptions = RunConcurrently(actions);

            // Assert
            exceptions.Should().BeEmpty("iteration {0}", iteration);
            results.Count(result => result.Outcome == EnrollOutcome.Enrolled)
                .Should().Be(10, "iteration {0}", iteration);
            results.Count(result => result.Outcome == EnrollOutcome.Waitlisted)
                .Should().Be(90, "iteration {0}", iteration);
            var positions = results
                .Where(result => result.Outcome == EnrollOutcome.Waitlisted)
                .Select(result => result.WaitlistPosition!.Value)
                .ToArray();
            positions.Should().HaveCount(90, "iteration {0}", iteration);
            positions.Distinct().Should().HaveCount(90, "iteration {0}", iteration);
            positions.Should().BeEquivalentTo(
                Enumerable.Range(1, 90),
                because: "iteration {0}",
                becauseArgs: [iteration]);

            var snapshot = service.GetSnapshot();
            InvariantAssertions.AssertInvariants(snapshot);
            snapshot.Enrolled.Should().HaveCount(10, "iteration {0}", iteration);
            snapshot.Waitlist.Should().HaveCount(90, "iteration {0}", iteration);
            var actualLearners = snapshot.Enrolled.Concat(snapshot.Waitlist).ToArray();
            actualLearners.Distinct(StringComparer.OrdinalIgnoreCase)
                .Should().HaveCount(100, "iteration {0}", iteration);
            actualLearners.Should().BeEquivalentTo(learnerIds, because: "iteration {0}", becauseArgs: [iteration]);
        }
    }

    [Fact]
    [Trait("Slice", "C9")]
    [Trait("Case", "T-40")]
    public void Enroll_SameLearnerConcurrently_CreatesOneEnrolledRecord()
    {
        for (var iteration = 0; iteration < 300; iteration++)
        {
            // Arrange
            var service = new TestServiceBuilder()
                .WithCapacity(10)
                .WithPrerequisite(null)
                .Build();
            var results = new EnrollResult[50];
            var actions = Enumerable.Range(0, results.Length)
                .Select(index => (Action)(() => results[index] = service.Enroll("A")))
                .ToArray();

            // Act
            var exceptions = RunConcurrently(actions);

            // Assert
            exceptions.Should().BeEmpty("iteration {0}", iteration);
            results.Count(result => result.Outcome == EnrollOutcome.Enrolled)
                .Should().Be(1, "iteration {0}", iteration);
            results.Count(result => result.Outcome == EnrollOutcome.AlreadyEnrolled)
                .Should().Be(49, "iteration {0}", iteration);
            var snapshot = service.GetSnapshot();
            InvariantAssertions.AssertInvariants(snapshot);
            snapshot.ShouldMatch(
                new TestServiceBuilder().WithCapacity(10).ExpectedSnapshot(["A"]));
        }
    }

    [Fact]
    [Trait("Slice", "C9")]
    [Trait("Case", "T-41")]
    public void MixedOperationsConcurrently_PreserveWaitlistAndSnapshotInvariants()
    {
        for (var iteration = 0; iteration < 30; iteration++)
        {
            // Arrange
            var builder = new TestServiceBuilder()
                .WithCapacity(10)
                .WithPrerequisite(null);
            var service = builder.Build();

            for (var index = 1; index <= 10; index++)
            {
                service.Enroll($"E{index:D2}").Outcome.Should().Be(EnrollOutcome.Enrolled);
            }

            for (var index = 1; index <= 40; index++)
            {
                service.Enroll($"W{index:D2}").Outcome.Should().Be(EnrollOutcome.Waitlisted);
            }

            var drops = new DropResult[10];
            var enrollments = new EnrollResult[40];
            var actions = new List<Action>(54);
            for (var index = 0; index < drops.Length; index++)
            {
                var slot = index;
                var learnerId = $"E{index + 1:D2}";
                actions.Add(() => drops[slot] = service.Drop(learnerId));
            }

            for (var index = 0; index < enrollments.Length; index++)
            {
                var slot = index;
                var learnerId = $"N{index + 1:D2}";
                actions.Add(() => enrollments[slot] = service.Enroll(learnerId));
            }

            for (var reader = 0; reader < 4; reader++)
            {
                actions.Add(() =>
                {
                    for (var read = 0; read < 200; read++)
                    {
                        InvariantAssertions.AssertInvariants(service.GetSnapshot());
                    }
                });
            }

            // Act
            var exceptions = RunConcurrently(actions);

            // Assert
            exceptions.Should().BeEmpty("iteration {0}", iteration);
            drops.Should().OnlyContain(
                result => result.Outcome == DropOutcome.Dropped,
                "iteration {0}",
                iteration);
            enrollments.Should().OnlyContain(
                result => result.Outcome == EnrollOutcome.Waitlisted,
                "iteration {0}",
                iteration);
            var snapshot = service.GetSnapshot();
            InvariantAssertions.AssertInvariants(snapshot);
            InvariantAssertions.AssertNoFreeSeatBesideEligibleWaiter(snapshot, _ => true);
            snapshot.Enrolled.Should().Equal(
                Enumerable.Range(1, 10).Select(index => $"W{index:D2}"),
                because: "iteration {0}",
                becauseArgs: [iteration]);
            snapshot.Waitlist.Should().HaveCount(70, "iteration {0}", iteration);
            snapshot.Waitlist.Take(30).Should().Equal(
                Enumerable.Range(11, 30).Select(index => $"W{index:D2}"),
                because: "iteration {0}",
                becauseArgs: [iteration]);
            snapshot.Waitlist.Skip(30).Should().BeEquivalentTo(
                Enumerable.Range(1, 40).Select(index => $"N{index:D2}"),
                because: "iteration {0}",
                becauseArgs: [iteration]);
        }
    }

    [Fact]
    [Trait("Slice", "C9")]
    [Trait("Case", "T-42")]
    public void Drop_SameEnrolledLearnerConcurrently_PromotesExactlyOnce()
    {
        for (var iteration = 0; iteration < 1000; iteration++)
        {
            // Arrange
            var builder = new TestServiceBuilder()
                .WithCapacity(1)
                .WithPrerequisite(null);
            var service = builder.Build();
            service.Enroll("A").Outcome.Should().Be(EnrollOutcome.Enrolled);
            service.Enroll("B").Outcome.Should().Be(EnrollOutcome.Waitlisted);
            var results = new DropResult[20];
            var actions = Enumerable.Range(0, results.Length)
                .Select(index => (Action)(() => results[index] = service.Drop("A")))
                .ToArray();

            // Act
            var exceptions = RunConcurrently(actions);

            // Assert
            exceptions.Should().BeEmpty("iteration {0}", iteration);
            results.Count(result => result.Outcome == DropOutcome.Dropped)
                .Should().Be(1, "iteration {0}", iteration);
            results.Count(result => result.Outcome == DropOutcome.NotEnrolled)
                .Should().Be(19, "iteration {0}", iteration);
            results.SelectMany(result => result.PromotedLearnerIds)
                .Should().Equal(["B"], because: "iteration {0}", becauseArgs: [iteration]);
            var snapshot = service.GetSnapshot();
            InvariantAssertions.AssertInvariants(snapshot);
            snapshot.ShouldMatch(builder.ExpectedSnapshot(["B"]));
        }
    }

    private static IReadOnlyCollection<Exception> RunConcurrently(IReadOnlyList<Action> actions)
    {
        var gate = new SpinGate();
        var exceptions = new ConcurrentBag<Exception>();
        var tasks = actions
            .Select(action => Task.Factory.StartNew(
                () =>
                {
                    Interlocked.Increment(ref gate.Ready);
                    SpinWait.SpinUntil(() => Volatile.Read(ref gate.Go) == 1);
                    try
                    {
                        action();
                    }
                    catch (Exception exception)
                    {
                        exceptions.Add(exception);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        if (!SpinWait.SpinUntil(
                () => Volatile.Read(ref gate.Ready) == actions.Count,
                TimeSpan.FromSeconds(10)))
        {
            Volatile.Write(ref gate.Go, 1);
            throw new TimeoutException("Concurrent test tasks did not become ready within 10 seconds.");
        }

        Volatile.Write(ref gate.Go, 1);
        try
        {
            Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException("Concurrent test actions did not complete within 30 seconds.", exception);
        }

        return exceptions;
    }

    private sealed class SpinGate
    {
        public int Ready;
        public int Go;
    }
}
