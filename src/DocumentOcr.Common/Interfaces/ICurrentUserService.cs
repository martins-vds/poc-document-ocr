namespace DocumentOcr.Common.Interfaces;

/// <summary>
/// Exposes the authenticated principal's UPN to services that need to stamp
/// reviewer identity on Cosmos records. Implemented in the WebApp using
/// <c>HttpContextAccessor</c>; tests provide a fake.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>UPN of the currently authenticated reviewer.</summary>
    /// <exception cref="InvalidOperationException">When the request is not authenticated.</exception>
    string GetCurrentUserUpn();
}
