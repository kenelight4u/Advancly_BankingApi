using JasperFx.CodeGeneration;
using Wolverine.Configuration;
using Wolverine.Runtime.Handlers;

namespace BankingApi.Application.Common.Behaviors;

/// <summary>
/// Wolverine IHandlerPolicy that applies ValidationMiddleware to
/// EVERY message handler in the application.
///
/// Registered in Program.cs:
///   opts.Policies.Add&lt;ValidationPolicy&gt;();
///
/// This is preferred over AddMiddleware&lt;T&gt;() when you want fine-grained
/// control — e.g. skipping system/internal messages.
/// </summary>
public class ValidationPolicy : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules,
        IServiceContainer container)
    {
        foreach (var chain in chains)
        {
            // Add ValidationMiddleware as the FIRST step in every chain
            // so validation always runs before the handler body executes.
            chain.Middleware.Insert(0,
                new Wolverine.Runtime.Handlers.MiddlewareCall(
                    typeof(ValidationMiddleware),
                    nameof(ValidationMiddleware.Before)));
        }
    }
}