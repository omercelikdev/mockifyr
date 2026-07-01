// Mockifyr standalone host — the single composition root.
// Facades and the engine are wired here starting at G0/G7; for now this is a minimal
// placeholder so the host builds and runs. See docs/decisions/0001-transport-agnostic-core.md.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Mockifyr host — not yet configured. See docs/roadmap.md.");

app.Run();
