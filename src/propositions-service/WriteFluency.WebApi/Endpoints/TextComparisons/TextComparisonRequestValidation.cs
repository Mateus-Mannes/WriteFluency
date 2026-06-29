namespace WriteFluency.TextComparisons;

public static class TextComparisonRequestValidation
{
    public static TextComparisonRequestValidationResult ValidateUserText(
        string? userText,
        int maxUserTextLength)
    {
        if (userText is null)
        {
            return TextComparisonRequestValidationResult.Invalid(
                "missing_user_text",
                "UserText is required.",
                null);
        }

        if (string.IsNullOrWhiteSpace(userText))
        {
            return TextComparisonRequestValidationResult.Invalid(
                "empty_user_text",
                "UserText cannot be empty.",
                userText.Length);
        }

        if (userText.Length > maxUserTextLength)
        {
            return TextComparisonRequestValidationResult.Invalid(
                "user_text_too_long",
                $"UserText cannot exceed {maxUserTextLength} characters.",
                userText.Length);
        }

        return TextComparisonRequestValidationResult.Valid(userText.Length);
    }
}

public sealed record TextComparisonRequestValidationResult(
    bool IsValid,
    string ReasonCode,
    string? ErrorMessage,
    int? UserTextLength)
{
    public static TextComparisonRequestValidationResult Valid(
        int userTextLength) =>
        new(true, "valid", null, userTextLength);

    public static TextComparisonRequestValidationResult Invalid(
        string reasonCode,
        string errorMessage,
        int? userTextLength) =>
        new(false, reasonCode, errorMessage, userTextLength);
}
