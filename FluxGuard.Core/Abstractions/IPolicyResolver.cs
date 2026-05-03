namespace FluxGuard.Core.Abstractions;

using FluxGuard.Core.Models;
using Microsoft.AspNetCore.Http;

public interface IPolicyResolver
{
    RateLimitPolicy Resolve(HttpContext context);
}