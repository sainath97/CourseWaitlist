using CourseWaitlist.Api.Contracts;
using CourseWaitlist.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace CourseWaitlist.Api.Endpoints;

public static class EnrollmentEndpoints
{
    public static IEndpointRouteBuilder MapEnrollmentEndpoints(this IEndpointRouteBuilder app)
    {
        var offerings = app.MapGroup("/offerings/{offeringId}");
        offerings.MapPost("/enrollments", (
                string offeringId,
                EnrollRequest? request,
                EnrollmentService service,
                IOptions<OfferingOptions> options,
                ILogger<Program> logger) =>
            {
                var learnerId = request?.LearnerId?.Trim() ?? string.Empty;
                if (!string.Equals(offeringId, options.Value.Id, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation(
                        "Enroll outcome {Outcome} for learner {LearnerId}",
                        "OfferingNotFound",
                        learnerId);
                    return Results.Problem(statusCode: 404, title: "Offering not found");
                }

                var result = service.Enroll(request?.LearnerId);
                var outcome = result.Outcome.ToString();
                logger.LogInformation(
                    "Enroll outcome {Outcome} for learner {LearnerId}",
                    outcome,
                    learnerId);

#pragma warning disable CS8524
                return result.Outcome switch
                {
                    EnrollOutcome.Enrolled => Results.Json(
                        new MutationResponse(outcome, learnerId, result.WaitlistPosition, null),
                        statusCode: 201),
                    EnrollOutcome.Waitlisted => Results.Json(
                        new MutationResponse(outcome, learnerId, result.WaitlistPosition, null),
                        statusCode: 202),
                    EnrollOutcome.AlreadyEnrolled => Results.Json(
                        new MutationResponse(outcome, learnerId, result.WaitlistPosition, null),
                        statusCode: 200),
                    EnrollOutcome.AlreadyWaitlisted => Results.Json(
                        new MutationResponse(outcome, learnerId, result.WaitlistPosition, null),
                        statusCode: 200),
                    EnrollOutcome.PrerequisiteNotMet => Results.Problem(
                        statusCode: 422,
                        title: "Prerequisite not met",
                        extensions: new Dictionary<string, object?>
                        {
                            ["outcome"] = outcome,
                            ["learnerId"] = learnerId
                        }),
                    EnrollOutcome.InvalidRequest => Results.Problem(
                        statusCode: 400,
                        title: "Invalid request",
                        extensions: new Dictionary<string, object?>
                        {
                            ["outcome"] = outcome
                        })
                };
#pragma warning restore CS8524
            });

        offerings.MapDelete("/enrollments/{learnerId}", (
            string offeringId,
            string learnerId,
            EnrollmentService service,
            IOptions<OfferingOptions> options,
            ILogger<Program> logger) =>
        {
            var trimmedLearnerId = learnerId.Trim();
            if (!string.Equals(offeringId, options.Value.Id, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "Drop outcome {Outcome} for learner {LearnerId}",
                    "OfferingNotFound",
                    trimmedLearnerId);
                return Results.Problem(statusCode: 404, title: "Offering not found");
            }

            var result = service.Drop(learnerId);
            var outcome = result.Outcome.ToString();
            logger.LogInformation(
                "Drop outcome {Outcome} for learner {LearnerId}",
                outcome,
                trimmedLearnerId);

#pragma warning disable CS8524
            return result.Outcome switch
            {
                DropOutcome.Dropped => Results.Json(
                    new MutationResponse(outcome, trimmedLearnerId, null, result.PromotedLearnerIds),
                    statusCode: 200),
                DropOutcome.RemovedFromWaitlist => Results.Json(
                    new MutationResponse(outcome, trimmedLearnerId, null, result.PromotedLearnerIds),
                    statusCode: 200),
                DropOutcome.NotEnrolled => Results.Json(
                    new MutationResponse(outcome, trimmedLearnerId, null, result.PromotedLearnerIds),
                    statusCode: 200),
                DropOutcome.InvalidRequest => Results.Problem(
                    statusCode: 400,
                    title: "Invalid request",
                    extensions: new Dictionary<string, object?>
                    {
                        ["outcome"] = outcome
                    })
            };
#pragma warning restore CS8524
        });

        return app;
    }
}
