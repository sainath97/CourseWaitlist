using CourseWaitlist.Core;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<OfferingOptions>()
    .BindConfiguration("Offering")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.Id)
            && !string.IsNullOrWhiteSpace(options.CourseCode)
            && options.Capacity >= 1,
        "Offering requires Id, CourseCode and Capacity >= 1.")
    .ValidateOnStart();

builder.Services.AddSingleton<InMemoryLearnerRecords>(serviceProvider =>
{
    var records = new InMemoryLearnerRecords();
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    foreach (var learnerSection in configuration.GetSection("LearnerRecords").GetChildren())
    {
        foreach (var courseSection in learnerSection.GetChildren())
        {
            if (courseSection.Value is { } courseCode)
            {
                records.Add(learnerSection.Key, courseCode);
            }
        }
    }

    return records;
});
builder.Services.AddSingleton<ILearnerRecords>(
    serviceProvider => serviceProvider.GetRequiredService<InMemoryLearnerRecords>());
builder.Services.AddSingleton<EnrollmentService>(serviceProvider =>
    new EnrollmentService(
        serviceProvider.GetRequiredService<IOptions<OfferingOptions>>().Value,
        serviceProvider.GetRequiredService<ILearnerRecords>()));

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();

public partial class Program { }
