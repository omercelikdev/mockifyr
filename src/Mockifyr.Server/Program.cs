// Mockifyr standalone host — the single composition root. Wires the shared engine + stores + the
// Mediant management path (AddMockifyr), then maps the WireMock-compatible admin HTTP surface.
// The mock-serving HTTP path (catch-all → engine) lands with the transport facade (G12).
using Mockifyr.Facade.Admin;
using Mockifyr.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMockifyr();

var app = builder.Build();
app.MapAdminEndpoints();
app.MapGet("/", () => "Mockifyr host — admin at /__admin. Mock serving (HTTP) arrives with G12.");

app.Run();

// Exposed so the differential test host (WebApplicationFactory) can boot the admin surface.
public partial class Program;
