using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BankingApi.Application.Common.Behaviors;

/// <summary>
/// Wolverine IMessageMiddleware that runs all registered FluentValidation
/// validators for the incoming message before the handler executes.
///
/// Registration in Program.cs (inside UseWolverine options):
///   opts.Policies.AddMiddleware&lt;ValidationMiddleware&gt;();
///
/// How it works:
///   1. Resolves IEnumerable&lt;IValidator&lt;T&gt;&gt; from the DI container.
///   2. If no validators are registered for T, the message passes through.
///   3. Runs all validators in parallel via ValidateAsync.
///   4. Collects all failures and throws ValidationException (422) if any exist.
/// </summary>
public class ValidationMiddleware
{
    /// <summary>
    /// Wolverine discovers this method by convention:
    /// the first parameter must be the message type (T),
    /// second must be IMessageContext or a CancellationToken or both,
    /// and it must call "next" to continue the pipeline.
    /// We use the overload that accepts a MessageContext and
    /// IServiceProvider so we can resolve validators lazily.
    /// </summary>
    public static async Task Before<T>(
        T message,
        IServiceProvider services,
        CancellationToken ct) where T : class
    {
        // Resolve all validators registered for this message type
        var validators = services
            .GetServices<IValidator<T>>()
            .ToList();

        if (validators.Count == 0)
            return; // No validators registered — pass through

        // Run all validators concurrently
        var validationTasks = validators
            .Select(v => v.ValidateAsync(message, ct));

        var results = await Task.WhenAll(validationTasks);

        // Collect every failure from every validator
        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .Select(f => (f.PropertyName, f.ErrorMessage))
            .ToList();

        if (failures.Count > 0)
            throw new Exceptions.ValidationException(failures);
    }
}