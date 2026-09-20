using FluentValidation;

namespace CEMS.Application.Common.Validation;

/// <summary>
/// The money and score columns are numeric(10,2) / numeric(6,2). Without a scale check a value such as
/// 100.005 is accepted by validation, used as-is in calculations (status, totals), and then silently rounded
/// by the database -- so what was computed and what was stored disagree. These reject it up front.
/// </summary>
public static class MoneyRules
{
    public static IRuleBuilderOptions<T, decimal> IsMoney<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0).PrecisionScale(10, 2, true);

    public static IRuleBuilderOptions<T, decimal?> IsMoney<T>(this IRuleBuilder<T, decimal?> rule) =>
        rule.GreaterThan(0).PrecisionScale(10, 2, true);

    /// <summary>A score / maximum score (numeric(6,2)); <paramref name="allowZero"/> is for a score, not a maximum.</summary>
    public static IRuleBuilderOptions<T, decimal> IsScore<T>(this IRuleBuilder<T, decimal> rule, bool allowZero) =>
        (allowZero ? rule.GreaterThanOrEqualTo(0) : rule.GreaterThan(0)).PrecisionScale(6, 2, true);
}
