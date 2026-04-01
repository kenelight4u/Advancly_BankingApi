using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

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
public static class ValidationMiddleware
{
    /// <summary>
    /// Wolverine discovers this method by convention:
    /// the first parameter must be the message type (T),
    /// second must be IMessageContext or a CancellationToken or both,
    /// and it must call "next" to continue the pipeline.
    /// We use the overload that accepts a MessageContext and
    /// IServiceProvider so we can resolve validators lazily.
    /// </summary>
    public static async Task Before(
        IServiceScopeFactory scopeFactory,
        Envelope envelope)
    {
        if (envelope.Message is null) return;

        var messageType = envelope.Message.GetType();
        var validatorType = typeof(IValidator<>).MakeGenericType(messageType);

        using var scope = scopeFactory.CreateScope();
        var validator = scope.ServiceProvider.GetService(validatorType);

        if (validator is null) return;

        var contextType = typeof(ValidationContext<>).MakeGenericType(messageType);
        var validationContext = (IValidationContext)Activator
            .CreateInstance(contextType, envelope.Message)!;

        var result = await ((IValidator)validator).ValidateAsync(validationContext);

        if (!result.IsValid)
            throw new Exceptions.ValidationException(result);
    }
}