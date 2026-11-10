namespace StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

public enum ConsumptionPlanErrorCode
{
    Validation,
    NotFound,
    DuplicatePlan,
    StaleVersion,
    EditPeriodClosed,
    TargetMonthInPast,
    StockNotActive,
    AmbiguousStock,
    QuantityExceedsStock,
    PlanOrphaned
}

public sealed record ConsumptionPlanError(ConsumptionPlanErrorCode Code, string Message)
{
    public int StatusCode => Code switch
    {
        ConsumptionPlanErrorCode.Validation => StatusCodes.Status400BadRequest,
        ConsumptionPlanErrorCode.NotFound => StatusCodes.Status404NotFound,
        ConsumptionPlanErrorCode.DuplicatePlan or ConsumptionPlanErrorCode.StaleVersion => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity
    };

    public IResult ToHttpResult()
    {
        return Results.Problem(
            detail: Message,
            statusCode: StatusCode,
            title: Code.ToString(),
            extensions: new Dictionary<string, object?> { ["code"] = Code.ToString() });
    }

    public static ConsumptionPlanError Validation(string message) => new(ConsumptionPlanErrorCode.Validation, message);

    public static ConsumptionPlanError NotFound(int id) =>
        new(ConsumptionPlanErrorCode.NotFound, $"Consumption plan {id} was not found.");

    public static ConsumptionPlanError StaleVersion() =>
        new(ConsumptionPlanErrorCode.StaleVersion, "The plan was changed by someone else. Reload it and try again.");

    public static ConsumptionPlanError DuplicatePlan(
        StockDocumentKey key,
        PlanTargetMonth target) =>
        new(
            ConsumptionPlanErrorCode.DuplicatePlan,
            $"A plan for stock record {key} in {target} already exists.");

    public static ConsumptionPlanError TargetMonthInPast(PlanTargetMonth target) =>
        new(
            ConsumptionPlanErrorCode.TargetMonthInPast,
            $"Plans cannot be created for {target}, which has already ended.");

    public static ConsumptionPlanError InvalidTarget() =>
        Validation(
            "Target month must be a month 1–12 of a year "
            + $"{PlanTargetMonth.MinYear}–{PlanTargetMonth.MaxYear}.");
}

public sealed record ConsumptionPlanResult<T>(T? Value, ConsumptionPlanError? Error)
{
    public bool IsSuccess => Error is null;

    public static ConsumptionPlanResult<T> Success(T value) => new(value, null);

    public static ConsumptionPlanResult<T> Failure(ConsumptionPlanError error) => new(default, error);

    public static implicit operator ConsumptionPlanResult<T>(ConsumptionPlanError error) => Failure(error);
}
