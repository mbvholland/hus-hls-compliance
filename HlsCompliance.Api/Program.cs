using HlsCompliance.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<AssessmentService>();
builder.Services.AddSingleton<DpiaQuickscanService>();
builder.Services.AddSingleton<MdrService>();
builder.Services.AddSingleton<AiActService>();
builder.Services.AddSingleton<KoppelingenService>();
builder.Services.AddSingleton<SecurityProfileService>();
builder.Services.AddSingleton<ToetsVooronderzoekService>();
builder.Services.AddSingleton<AlgemeenService>();

builder.Services.AddScoped<DueDiligenceService>();

builder.Services.AddSingleton<IChecklistDefinitionRepository, JsonChecklistDefinitionRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
