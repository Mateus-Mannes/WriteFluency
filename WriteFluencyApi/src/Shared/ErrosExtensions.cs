﻿using FluentResults;

namespace WriteFluencyApi.Shared;

public static class ErrosExtensions
{
    public static string Message(this IEnumerable<IError> errors) => string.Join("\n", errors.Select(x => x.Message));
}
