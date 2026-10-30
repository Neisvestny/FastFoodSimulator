namespace FastFoodSimulator.Validation;

public sealed record ValidationResult(bool IsValid, int Value, string ErrorMessage)
{
    public static ValidationResult Ok(int value) => new(true, value, string.Empty);
    public static ValidationResult Fail(string message) => new(false, 0, message);
}

public static class IntervalValidator
{
    public static ValidationResult Validate(string? input, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ValidationResult.Fail($"{fieldName}: значение не задано. Введите целое число миллисекунд (например, 500).");

        if (!int.TryParse(input.Trim(), out var value))
            return ValidationResult.Fail($"{fieldName}: «{input.Trim()}» не является целым числом. Допустимы только целые миллисекунды.");

        if (value <= 0)
            return ValidationResult.Fail($"{fieldName}: интервал должен быть строго больше нуля (указано {value}).");

        return ValidationResult.Ok(value);
    }

    public static ValidationResult ValidateCount(string? input, string fieldName, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ValidationResult.Fail($"{fieldName}: значение не задано. Введите число сотрудников от {min} до {max}.");

        if (!int.TryParse(input.Trim(), out var value))
            return ValidationResult.Fail($"{fieldName}: «{input.Trim()}» не является целым числом. Допустимы числа от {min} до {max}.");

        if (value < min || value > max)
            return ValidationResult.Fail($"{fieldName}: допустимо от {min} до {max} сотрудников (указано {value}).");

        return ValidationResult.Ok(value);
    }
}
