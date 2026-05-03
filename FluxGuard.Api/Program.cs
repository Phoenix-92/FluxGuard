using FluxGuard.Api.Middleware;
using FluxGuard.Core.Abstractions;
using FluxGuard.Core.Algorithms;
using FluxGuard.Core.Policies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<IRateLimiter, TokenBucketRateLimiter>();
builder.Services.AddSingleton<IPolicyResolver, DefaultPolicyResolver>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseMiddleware<RateLimitMiddleware>();
app.MapControllers();

app.Run();