using EvoContext.Infrastructure.Models;

namespace EvoContext.Infrastructure.Services;

public interface IGateAProbeWriter
{
    Task WriteAsync(GateAProbeArtifact artifact, CancellationToken cancellationToken = default);
}
