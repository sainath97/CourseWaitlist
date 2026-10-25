using CourseWaitlist.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CourseWaitlist.Tests.Api;

public class CompositionTests
{
    [Fact]
    [Trait("Slice", "A0")]
    [Trait("Case", "T-43")]
    public void Composition_DefaultConfiguration_ResolvesOfferingService()
    {
        // Arrange
        using var factory = new ApiFactory();

        // Act
        var service = factory.Services.GetRequiredService<EnrollmentService>();
        var snapshot = service.GetSnapshot();

        // Assert
        snapshot.OfferingId.Should().Be("CS201-TEST");
        snapshot.CourseCode.Should().Be("CS201");
        snapshot.Capacity.Should().Be(2);
        snapshot.Enrolled.Should().BeEmpty();
        snapshot.Waitlist.Should().BeEmpty();
    }

    [Fact]
    [Trait("Slice", "A0")]
    [Trait("Case", "T-44")]
    public void Composition_ZeroCapacity_ThrowsOptionsValidationExceptionAtStartup()
    {
        // Arrange
        using var factory = new ApiFactory(new Dictionary<string, string?>
        {
            ["Offering:Capacity"] = "0"
        });

        // Act
        Action act = () => factory.CreateClient();

        // Assert
        var exception = act.Should().Throw<Exception>().Which;
        HasOptionsValidationException(exception).Should().BeTrue();
    }

    [Fact]
    [Trait("Slice", "A0")]
    [Trait("Case", "T-45")]
    public void Composition_BlankOfferingId_ThrowsOptionsValidationExceptionAtStartup()
    {
        // Arrange
        using var factory = new ApiFactory(new Dictionary<string, string?>
        {
            ["Offering:Id"] = ""
        });

        // Act
        Action act = () => factory.CreateClient();

        // Assert
        var exception = act.Should().Throw<Exception>().Which;
        HasOptionsValidationException(exception).Should().BeTrue();
    }

    private static bool HasOptionsValidationException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OptionsValidationException)
            {
                return true;
            }
        }

        return false;
    }
}
