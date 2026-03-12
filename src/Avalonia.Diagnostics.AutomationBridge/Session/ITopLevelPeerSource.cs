using System.Collections.Generic;
using Avalonia.Automation.Peers;

namespace Avalonia.Diagnostics.AutomationBridge.Session;

/// <summary>
/// Provides the current enumeration of top-level automation root peers for a bridge session.
/// </summary>
/// <remarks>
/// <para>
/// Implementations read from a live source (e.g. the application lifetime) so that
/// each call to <see cref="GetPeers"/> reflects the current state of the source.
/// Results are never cached by the session.
/// </para>
/// <para>
/// The interface is not thread-safe.  Callers must serialise access if the source can
/// change concurrently.
/// </para>
/// </remarks>
public interface ITopLevelPeerSource
{
    /// <summary>
    /// Returns the current set of top-level automation root peers.
    /// </summary>
    /// <returns>
    /// A non-null, possibly empty list.  The list is valid for the current call only;
    /// callers must not cache it across calls.
    /// </returns>
    IReadOnlyList<AutomationPeer> GetPeers();
}
