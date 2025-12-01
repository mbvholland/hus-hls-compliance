using HlsCompliance.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registratie van onze in-memory AssessmentService
builder.Services.AddSingleton<AssessmentService>();
builder.Services.AddSingleton<DpiaQuickscanService>();
builder.Services.AddSingleton<MdrService>();
builder.Services.AddSingleton<AiActService>();
builder.Services.AddSingleton<KoppelingenService>();
builder.Services.AddSingleton<SecurityProfileService>();
builder.Services.AddSingleton<ToetsVooronderzoekService>();
builder.Services.AddSingleton<AlgemeenService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

// Zorg dat de controllers (zoals AssessmentsController) worden gemapped
app.MapControllers();

app.Run();
