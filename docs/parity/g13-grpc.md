# G13 — gRPC extension

gRPC parity by **reuse, not reimplementation**: a gRPC call is decoded to JSON, run through the exact
same stub engine (matching + templating) as HTTP, and the response JSON is re-encoded to protobuf. The
engine never learns about protobuf; gRPC is a codec + transport at the facade edge — the design
ARCHITECTURE.md called for. Validated against the **official WireMock gRPC extension** as the oracle.

## Unary serving (G13a)

- **Group / item:** G13a — validated over the wire against WireMock + its gRPC extension.
- **A gRPC stub is an ordinary stub.** WireMock's gRPC extension (and Mockifyr) map a call to a POST to
  `/{package.Service}/{Method}` with the request message as a JSON body — so a stub is just
  `urlPath` + an `equalToJson` body pattern → a `jsonBody` response. The whole matching/response
  machinery is unchanged; only the message ↔ JSON conversion is new. Confirmed: the same stub JSON,
  loaded into both sides, yields byte-identical replies for the same call.
- **Descriptor-driven codec.** Unlike Java, C#'s Google.Protobuf has **no runtime `DynamicMessage`**, so
  Mockifyr can't parse an arbitrary user message from a descriptor out of the box. `ProtobufJsonCodec`
  fills the gap: given only a `MessageDescriptor` (loaded from a compiled `.dsc`), it walks the wire
  format with `CodedInputStream`/`CodedOutputStream`, mapping each field by its descriptor to/from
  proto3-canonical JSON (the same protobuf-java-util shape the extension emits) — so the JSON matches
  what a hand-written stub expects. Covered: the proto3 scalars, `string`/`bytes` (base64), nested
  messages, and non-packed repeated fields; 64-bit ints render as JSON strings (proto3 JSON).
- **Descriptors.** `ProtoDescriptors` loads `*.dsc` (from `<root-dir>/grpc/`, the same place WireMock's
  extension reads) and indexes services/methods by the gRPC path. Serving is enabled when descriptors
  are present.
- **Transport.** The `GrpcServingMiddleware` handles the gRPC HTTP/2 framing (`[flag][len][message]`)
  and the `grpc-status` trailer, ahead of the HTTP mock-serving fallback. gRPC requires HTTP/2; it is
  driven **over TLS (ALPN-negotiated h2)**, since plaintext h2c is nondeterministic on WireMock
  (`HTTP_1_1_REQUIRED` — see g11-tls-http2.md). Mockifyr serves gRPC on its `--https-port` listener.
- **Validation.** The oracle is the real WireMock image with the official
  `wiremock-grpc-extension-standalone` jar loaded (downloaded once and cached, mounted with the
  descriptor + stub). The same unary `SayHello` call is driven against the oracle and Mockifyr with a
  real gRPC client; the replies must be equal.
- **Deferred (explicitly tracked — not a silent gap):** client/server **streaming**; packed repeated
  scalars, **maps, enums, oneofs**, and the well-known wrapper types in the codec; gRPC **status/error**
  responses and no-match semantics; and gRPC admin reset. Each builds on this codec + middleware.
- **Regression case:** `G13aGrpcTests.UnaryCall_MatchesTheOracle`.
