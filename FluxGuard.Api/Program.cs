using FluxGuard.Api.Middleware;
using FluxGuard.Core.Abstractions;
using FluxGuard.Core.Algorithms;
using FluxGuard.Core.Policies;
using FluxGuard.Infrastructure.Redis;
using FluxGuard.Infrastructure.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Infrastructure
builder.Services.AddSingleton<RedisConnectionFactory>();

//builder.Services.AddSingleton<IRateLimiter, TokenBucketRateLimiter>();
builder.Services.AddSingleton<IRateLimiter, RedisTokenBucketRateLimiter>();
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