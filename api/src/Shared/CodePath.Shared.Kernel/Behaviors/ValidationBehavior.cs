using System.Reflection;
using CodePath.Shared.Kernel.Common;
using FluentValidation;
using MediatR;

namespace CodePath.Shared.Kernel.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count != 0)
        {
            var errors = failures.Select(f => new ValidationError(f.PropertyName, f.ErrorMessage)).ToList();

            if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var method = typeof(TResponse).GetMethod(
                    nameof(Result<object>.ValidationFailure),
                    BindingFlags.Public | BindingFlags.Static);

                if (method != null)
                {
                    return (TResponse)method.Invoke(null, [errors])!;
                }
            }

            throw new ValidationException(failures);
        }

        return await next();
    }
}
