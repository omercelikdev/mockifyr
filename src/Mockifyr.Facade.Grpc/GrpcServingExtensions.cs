using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Mockifyr.Facade.Grpc;

/// <summary>
/// Composition helpers for the gRPC facade (G13): register the proto descriptors and insert the gRPC
/// serving middleware ahead of the HTTP mock-serving fallback.
/// </summary>
public static class GrpcServingExtensions
{
    /// <summary>Registers the descriptor index built from the given compiled descriptor sets.</summary>
    public static IServiceCollection AddMockifyrGrpc(this IServiceCollection services, IEnumerable<byte[]> descriptorSets) =>
        services.AddSingleton(new ProtoDescriptors(descriptorSets));

    /// <summary>
    /// Inserts the gRPC serving middleware. <c>application/grpc</c> requests are handled here; all
    /// others fall through to the next middleware (the HTTP mock-serving fallback).
    /// </summary>
    public static IApplicationBuilder UseMockifyrGrpc(this IApplicationBuilder app) =>
        app.UseMiddleware<GrpcServingMiddleware>();
}
