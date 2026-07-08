// Mockifyr standalone host entry point. The composition lives in MockifyrHost.Build (G12f): it wires
// the shared engine + stores + Mediant management path (AddMockifyr), maps the admin surface that
// speaks Mockifyr's importable JSON stub dialect plus the mock-serving fallback, binds the port
// (--port, default 8080), and loads any --root-dir/mappings/*.json at startup. Kept thin so the same
// wiring is exercised by tests (verified by the differential suite).
using Mockifyr.Server;

MockifyrHost.Build(args).Run();

// Exposed so the differential test host (WebApplicationFactory) can boot the same composition.
public partial class Program;
