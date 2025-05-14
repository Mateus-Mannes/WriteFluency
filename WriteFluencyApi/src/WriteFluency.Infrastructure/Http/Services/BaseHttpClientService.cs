using System.Net.Http.Json;
using System.Text.Json;
using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace WriteFluency.Infrastructure.Http.Services;

public abstract class BaseHttpClientService
{
    protected readonly HttpClient _httpClient;
    protected readonly ILogger _logger;

    protected BaseHttpClientService(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    protected Task<Result<TResponse>> GetAsync<TResponse>(
        string requestUri,
        IValidator<TResponse>? validator = null,
        int maxAttempts = 2)
    {
        return RequestAsync(
            requestUri.Split('?')[0],
            () => _httpClient.GetAsync(requestUri),
            validator,
            maxAttempts
        );
    }

    protected Task<Result<TResponse>> PostAsync<TRequest, TResponse>(
        string requestUri,
        TRequest body,
        IValidator<TResponse>? validator = null,
        int maxAttempts = 2)
    {
        return RequestAsync(
            requestUri,
            () => _httpClient.PostAsJsonAsync(requestUri, body),
            validator,
            maxAttempts
        );
    }

    private async Task<Result<TResponse>> RequestAsync<TResponse>(
        string context,
        Func<Task<HttpResponseMessage>> sendRequest,
        IValidator<TResponse>? validator,
        int maxAttempts,
        int attempt = 1)
    {
        try
        {
            var response = await sendRequest();

            if (!response.IsSuccessStatusCode)
            {
                LogHttpFailure(context, response, attempt, maxAttempts);
                if (attempt == maxAttempts) return Fail($"Failed to fetch data from {context}: {response.ReasonPhrase}");
                await Task.Delay(1000);
                return await RequestAsync(context, sendRequest, validator, maxAttempts, attempt + 1);
            }

            var result = await DeserializeResponse<TResponse>(response, context);
            if (result.IsFailed) return result;

            if (validator != null)
            {
                var validation = await validator.ValidateAsync(result.Value);
                if (!validation.IsValid)
                {
                    LogValidationFailure(context, validation);
                    return Result.Fail(validation.Errors.Select(e => new Error(e.ErrorMessage)));
                }
            }

            return Result.Ok(result.Value);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error when calling {Context} API", context);
            if (attempt == maxAttempts) return Fail($"Network error when calling {context} API");
            await Task.Delay(1000);
            return await RequestAsync(context, sendRequest, validator, maxAttempts, attempt + 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during {Context} request", context);
            return Fail($"Unexpected error during {context} request: {ex.Message}");
        }
    }

    private async Task<Result<TResponse>> DeserializeResponse<TResponse>(HttpResponseMessage response, string context)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync<TResponse>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result is null)
            {
                _logger.LogWarning("Deserialized {Context} response is null", context);
                return Fail($"Deserialized {context} response is null");
            }

            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize {Context} response", context);
            return Fail($"Failed to deserialize response: {ex.Message}");
        }
    }

    private void LogHttpFailure(string context, HttpResponseMessage response, int attempt, int maxAttempts)
    {
        var level = attempt == maxAttempts ? LogLevel.Error : LogLevel.Warning;
        _logger.Log(level, "HTTP failure on {Context}: {StatusCode} {ReasonPhrase} (attempt {Attempt}/{Max})",
            context, response.StatusCode, response.ReasonPhrase, attempt, maxAttempts);
    }

    private void LogValidationFailure(string context, ValidationResult validation)
    {
        var messages = string.Join(", ", validation.Errors.Select(e => e.ErrorMessage));
        _logger.LogWarning("{Context} response validation failed: {Errors}", context, messages);
    }

    private Result Fail(string message)
    {
        return Result.Fail(new Error(message));
    }
}