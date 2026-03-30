using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using Wolverine.Configuration;
using Wolverine.Runtime.Handlers;

namespace BankingApi.Application.Common.Behaviors;

/// <summary>
/// Wolverine IHandlerPolicy that applies ValidationMiddleware to
/// EVERY message handler in the application.
///
/// Registered in Program.cs:
///   opts.Policies.Add<ValidationPolicy>();
/// </summary>
public class ValidationPolicy : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains)
        {
            var middlewareCall = new MethodCall(
                typeof(ValidationMiddleware),
                nameof(ValidationMiddleware.Before));

            chain.Middleware.Insert(0, middlewareCall);
        }
    }
}