using RedisDemo.Api.Data;
using RedisDemo.Api.Repositories;
using RedisDemo.Api.Services;
using RedisDemo.Api.Workers;

var builder = WebApplication.CreateBuilder(args);

const string frontendCorsPolicy = "FrontendCors";

builder.Services.AddControllers();
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection("Redis"));

builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(
                "http://127.0.0.1:5173",
                "http://localhost:5173",
                "http://127.0.0.1:5174",
                "http://localhost:5174")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<MySqlConnectionFactory>();
builder.Services.AddSingleton<RedisConnectionFactory>();
builder.Services.AddScoped<AuthRepository>();
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<OrderRepository>();
builder.Services.AddScoped<RedisRepository>();
builder.Services.AddSingleton<RedisPubSubBroker>();
builder.Services.AddHostedService<OrderStreamWorker>();

var app = builder.Build();

app.UseCors(frontendCorsPolicy);
app.MapControllers();

app.MapGet("/", () => new
{
    name = "Redis Demo API",
    status = "Running",
    health = "/api/health"
});

app.Run();
