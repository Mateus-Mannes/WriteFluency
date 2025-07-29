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
        int maxAttempts = 2,
        CancellationToken cancellationToken = default)
    {
        return RequestAsync(
            requestUri.Split('?')[0],
            () => _httpClient.GetAsync(requestUri, cancellationToken),
            validator,
            maxAttempts,
            cancellationToken: cancellationToken
        );
    }

    protected Task<Result<TResponse>> PostAsync<TRequest, TResponse>(
        string requestUri,
        TRequest body,
        IValidator<TResponse>? validator = null,
        int maxAttempts = 2,
        CancellationToken cancellationToken = default)
    {
        return RequestAsync(
            requestUri,
            () => _httpClient.PostAsJsonAsync(requestUri, body, cancellationToken),
            validator,
            maxAttempts,
            cancellationToken: cancellationToken
        );
    }

    private async Task<Result<TResponse>> RequestAsync<TResponse>(
        string context,
        Func<Task<HttpResponseMessage>> sendRequest,
        IValidator<TResponse>? validator,
        int maxAttempts,
        int attempt = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await sendRequest();

            if (!response.IsSuccessStatusCode)
            {
                LogHttpFailure(context, response, attempt, maxAttempts);
                if (attempt == maxAttempts) return Fail($"Failed to fetch data from {context}: {response.ReasonPhrase}");
                await Task.Delay(1000);
                return await RequestAsync(context, sendRequest, validator, maxAttempts, attempt + 1, cancellationToken);
            }

            Result<TResponse> result;
            if (typeof(TResponse) == typeof(byte[]))
            {
                var fileResult = await DeserializeFileAsync(response, context, cancellationToken);
                result = fileResult is { IsSuccess: true }
                    ? Result.Ok((TResponse)(object)fileResult.Value!)
                    : Result.Fail<TResponse>(fileResult.Errors);
            }
            else
            {
                result = await DeserializeResponseAsync<TResponse>(response, context, cancellationToken);
            }

            if (result.IsFailed) return result;

            if (validator != null)
            {
                var validation = await validator.ValidateAsync(result.Value, cancellationToken);
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
            return await RequestAsync(context, sendRequest, validator, maxAttempts, attempt + 1, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during {Context} request", context);
            return Fail($"Unexpected error during {context} request: {ex.Message}");
        }
    }

    private async Task<Result<TResponse>> DeserializeResponseAsync<TResponse>(
        HttpResponseMessage response, string context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<TResponse>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, cancellationToken);

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

    private async Task<Result<byte[]>> DeserializeFileAsync(
        HttpResponseMessage response, string context, CancellationToken cancellationToken = default)
    {
        try
        {
            byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            if (bytes is null)
            {
                _logger.LogWarning("Deserialized {Context} response is null", context);
                return Fail($"Deserialized {context} response is null");
            }

            return Result.Ok(bytes);
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