using MongoDB.Driver;
using SmartRoute.API.Data;
using SmartRoute.API.Services;

var builder = WebApplication.CreateBuilder(args);


// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Mongo client + DbContext
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(builder.Configuration.GetConnectionString("MongoDB")));
builder.Services.AddScoped<MongoDbContext>();

// Services - make ShortestPathService scoped to avoid DI lifetime mismatch
builder.Services.AddScoped<IShortestPathService, ShortestPathService>();
builder.Services.AddScoped<ITrafficService, TrafficService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
    var importer = new OSMDataImporter(context);

    Console.WriteLine("===========================================");
    Console.WriteLine("Checking road data...");
    await importer.ImportRajkotRoadsAsync();
    Console.WriteLine("===========================================");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.Run();
