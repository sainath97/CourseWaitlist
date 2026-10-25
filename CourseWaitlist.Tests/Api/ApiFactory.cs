using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CourseWaitlist.Tests.Api;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly IDictionary<string, string?> _overrides;

    public ApiFactory(IDictionary<string, string?>? overrides = null)
    {
        _overrides = overrides ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Offering:Id"] = "CS201-TEST",
                ["Offering:CourseCode"] = "CS201",
                ["Offering:Capacity"] = "2",
                ["Offering:PrerequisiteCourseCode"] = "CS101",
                ["LearnerRecords:learner-001:0"] = "CS101",
                ["LearnerRecords:learner-002:0"] = "CS101",
                ["LearnerRecords:learner-003:0"] = "CS101"
            };

            foreach (var (key, value) in _overrides)
            {
                settings[key] = value;
            }

            configuration.AddInMemoryCollection(settings);
        });
    }
}
