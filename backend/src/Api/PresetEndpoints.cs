using QaTestOrchestrator.Application;
namespace QaTestOrchestrator.Api;
public static class PresetEndpoints
{
    public static void MapPresetEndpoints(this WebApplication app)
    {
        var project = app.MapGroup("/api/projects/{projectId:guid}/presets").WithTags("Presets");
        project.MapGet("/", (Guid projectId, IPresets service, CancellationToken ct, string? search = null, string? status = null, int page = 1, int pageSize = 12) => service.ListAsync(projectId, search, status, page, pageSize, ct));
        project.MapPost("/", async (Guid projectId, PresetWrite request, IPresets service, CancellationToken ct) => {
            var preset = await service.SaveAsync(projectId, null, request, ct); return Results.Created($"/api/presets/{preset.Id}", preset);
        });
        project.MapPut("/{id:guid}", (Guid projectId, Guid id, PresetWrite request, IPresets service, CancellationToken ct) => service.SaveAsync(projectId, id, request, ct));
        var presets = app.MapGroup("/api/presets/{id:guid}").WithTags("Presets");
        presets.MapGet("/", (Guid id, IPresets service, CancellationToken ct) => service.GetAsync(id, ct));
        presets.MapGet("/revisions", (Guid id, IPresets service, CancellationToken ct, int page = 1, int pageSize = 12) => service.RevisionsAsync(id, page, pageSize, ct));
        presets.MapGet("/preview", (Guid id, IPresets service, CancellationToken ct) => service.PreviewAsync(id, ct));
        presets.MapPost("/archive", (Guid id, RunCancelRequest request, IPresets service, CancellationToken ct) => service.ArchiveAsync(id, request.Version, ct));
        presets.MapPost("/runs", async (Guid id, PresetUse request, IPresets service, CancellationToken ct) => {
            var run = await service.UseAsync(id, request, ct); return Results.Created($"/api/test-runs/{run.Id}", run);
        });
    }
}
