using HlsCompliance.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Kernservices
builder.Services.AddSingleton<AssessmentService>();
builder.Services.AddSingleton<DpiaQuickscanService>();
builder.Services.AddSingleton<MdrService>();
builder.Services.AddSingleton<AiActService>();
builder.Services.AddSingleton<KoppelingenService>();
builder.Services.AddSingleton<SecurityProfileService>();
builder.Services.AddSingleton<ToetsVooronderzoekService>();
builder.Services.AddSingleton<AlgemeenService>();

// Due diligence-engine
builder.Services.AddSingleton<DueDiligenceService>();

// Statische checklistdefinities uit JSON (tab 7)
builder.Services.AddSingleton<IChecklistDefinitionRepository, JsonChecklistDefinitionRepository>();

// NIEUW: persistente opslag voor tab 8 (answers) en tab 11 (evidence)
builder.Services.AddSingleton<IAssessmentAnswersRepository, JsonAssessmentAnswersRepository>();
builder.Services.AddSingleton<IAssessmentEvidenceRepository, JsonAssessmentEvidenceRepository>();

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
