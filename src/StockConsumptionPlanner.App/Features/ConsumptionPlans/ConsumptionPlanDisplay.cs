using System.Globalization;
using Microsoft.Extensions.Localization;
using StockConsumptionPlanner.App.Features.ConsumptionPlans.Shared;

namespace StockConsumptionPlanner.App.Features.ConsumptionPlans;

/// <summary>Culture-aware display text for the consumption plan pages; API contracts stay invariant.</summary>
public static class ConsumptionPlanDisplay
{
    public static string FormatMonth(int year, int month)
    {
        return new DateTime(year, month, 1).ToString("Y", CultureInfo.CurrentCulture);
    }

    public static string FormatMonth(PlanTargetMonth month)
    {
        return FormatMonth(month.Year, month.Month);
    }

    public static string FormatQuantity(decimal value)
    {
        return value.ToString("#,0.####", CultureInfo.CurrentCulture);
    }

    public static string FormatQuantity(decimal? value)
    {
        return value is decimal quantity ? FormatQuantity(quantity) : "—";
    }

    public static string FormatMoney(decimal? value)
    {
        return value?.ToString("N2", CultureInfo.CurrentCulture) ?? "—";
    }

    public static string FormatDate(DateOnly? date)
    {
        return date?.ToString("d", CultureInfo.CurrentCulture) ?? "—";
    }

    public static string FormatDays(int? days)
    {
        return days?.ToString("#,0", CultureInfo.CurrentCulture) ?? "—";
    }

    public static string FormatBusinessTime(PlanEditPolicy editPolicy, DateTimeOffset? utc)
    {
        ArgumentNullException.ThrowIfNull(editPolicy);

        return utc is null
            ? "—"
            : editPolicy.ToBusinessTime(utc.Value).ToString("g", CultureInfo.CurrentCulture);
    }

    /// <summary>Translates a handler error by its code, falling back to the handler's message for unknown codes.</summary>
    public static string LocalizeError(
        IStringLocalizer localizer,
        ConsumptionPlanError error,
        StockDocumentKey key,
        PlanTargetMonth target,
        decimal? plannedQuantity = null)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(error);

        LocalizedString message = localizer[
            $"PlanError_{error.Code}",
            key.ToString(),
            FormatMonth(target),
            plannedQuantity is null ? string.Empty : FormatQuantity(plannedQuantity.Value),
            ConsumptionPlan.QuantityScale];
        return message.ResourceNotFound ? error.Message : message.Value;
    }
}
