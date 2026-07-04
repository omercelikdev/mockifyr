// Mockifyr standalone host — the single composition root. Wires the shared engine + stores + the
// Mediant management path (AddMockifyr), maps the WireMock-compatible admin HTTP surface, then the
// mock-serving fallback (G12): every non-admin request goes through the engine and is served over the
// wire.
using Mockifyr.Facade.Admin;
using Mockifyr.Facade.Http;
using Mockifyr.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMockifyr();

var app = builder.Build();
app.MapAdminEndpoints();
app.MapMockServing();

app.Run();

// Exposed so the differential test host (WebApplicationFactory) can boot the admin surface.
public partial class Program;
