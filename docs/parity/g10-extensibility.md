# Notes — G10 Extensibility

Unlike the other groups, G10 is an **API-design** item, not a parity one: custom extensions have no
WireMock equivalent to diff against, so they are validated **in-process** (like G7a), not against the
oracle.

## Public extension API (G10)

- **Group / item:** G10 — validated in-process (`Mockifyr.Application.Tests.ExtensibilityTests`).
- **Seams already existed.** `Mockifyr.Core.Extensibility` has carried the extension interfaces from
  the start (`IResponseTransformer`, `IResponseDefinitionTransformer`, `ITemplateHelper(Provider)`,
  `ITemplateModelProvider`, `IMatcherRegistry`, `IAdminApiExtension`, `IMappingsLoader`), and the
  built-ins dogfood them. G10 adds the **registration mechanism** and proves a user extension runs.
- **Registration:** `AddMockifyr(cfg => …)` with a `MockifyrExtensions` builder —
  `AddMatcher(name, matcher)`, `AddServeEventListener(listener)`, `AddTemplateHelper(name, render)`,
  `AddResponseTransformer(transformer)`. The composition wires them into DI so the same shared engine
  serves them.
- **Four extension types validated end-to-end:**
  - **Custom serve-event listener** — registered as an `IServeEventListener`, invoked after each
    serve (the built-in webhook is itself one). Verified a user listener's counter increments.
  - **Custom template helper** — the engine-agnostic `Func<IReadOnlyList<object?>, string>` is adapted
    into the Handlebars engine (`HandlebarsFactory`), usable as `{{name …}}`. Verified
    `{{shout 'hello'}}` → `HELLO!`.
  - **Custom response transformer** — an `IResponseTransformer` runs after rendering (globally, or
    when the stub names it in `transformers`), applied in `StubEngine`. Verified a suffix transformer.
  - **Custom matcher** — registered by name in the `IMatcherRegistry`, referenced from stub JSON via
    `customMatcher: {"name": …}`, resolved by the adapter into `RequestPattern.Custom`, and evaluated
    by the engine alongside the built-in matchers. Verified an even-body-length matcher gates matching
    (and it flows through the admin/CQRS create path, which now injects the registry).
- **Deferred (seams public, wired incrementally):** `IResponseDefinitionTransformer`,
  `ITemplateModelProvider`, `IAdminApiExtension` (`/__admin/ext/*`, with the transport facade at G12),
  `IMappingsLoader`, and `IRequestFilter`; template-helper *hash* arguments and helper *providers*.
- **Regression cases:** `ExtensibilityTests.{CustomServeEventListener_IsInvoked,
  CustomTemplateHelper_IsUsable, CustomResponseTransformer_IsApplied, CustomMatcher_GatesMatching}`.
