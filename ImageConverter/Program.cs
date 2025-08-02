using ImageConverter.Services.DocumentProcessor;
using ImageConverter.Services.FileStorage;
using ImageConverter.Services.ImageProcessor;
using ImageConverter.Services.JobStatus;
using ImageConverter.Services.RabbitMq;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();
builder.Services.AddSingleton<IJobStatusService, JobStatusService>();
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();
builder.Services.AddHostedService<DocumentProcessorWorker>();
builder.Services.AddHostedService<ImageProcessorWorker>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseCors("AllowAngular");
app.UseAuthorization();
app.MapControllers();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
var resultsDir = Path.Combine(Directory.GetCurrentDirectory(), "results");
Directory.CreateDirectory(uploadsDir);
Directory.CreateDirectory(resultsDir);

app.Run();
