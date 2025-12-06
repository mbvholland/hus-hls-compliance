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

// Persistente opslag van due diligence-beslissingen (kolom K/M)
builder.Services.AddSingleton<IAssessmentDueDiligenceDecisionRepository, JsonDueDiligenceDecisionRepository>();

// Due diligence-engine
builder.Services.AddSingleton<DueDiligenceService>();

// Statische checklistdefinities (tab 7)
builder.Services.AddSingleton<IChecklistDefinitionRepository, JsonChecklistDefinitionRepository>();

// Persistente opslag tab 8 (answers) en tab 11 (evidence)
builder.Services.AddSingleton<IAssessmentAnswersRepository, JsonAssessmentAnswersRepository>();
builder.Services.AddSingleton<IAssessmentEvidenceRepository, JsonAssessmentEvidenceRepository>();

// NIEUW: statische bewijs-definities en koppeling checklist↔bewijs (tab 9/10)
builder.Services.AddSingleton<IEvidenceDefinitionRepository, JsonEvidenceDefinitionRepository>();
builder.Services.AddSingleton<IChecklistEvidenceLinkRepository, JsonChecklistEvidenceLinkRepository>();

// Bewijsuitvraag-service (combineert checklist + answers + evidence + definities)
builder.Services.AddSingleton<EvidenceRequestService>();

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
