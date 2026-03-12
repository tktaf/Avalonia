using System;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Platform;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Session;

internal sealed class StubRootAutomationPeer : StubAutomationPeer, IRootProvider
{
    private AutomationPeer? _focusPeer;

    public StubRootAutomationPeer()
    {
        RegisterProvider<IRootProvider>(this);
    }

    public ITopLevelImpl? PlatformImpl => null;

    public event EventHandler? FocusChanged;

    public AutomationPeer? GetFocus() => _focusPeer;

    public AutomationPeer? GetPeerFromPoint(Point p) => null;

    public void SetFocusPeer(AutomationPeer? peer) => _focusPeer = peer;

    public void RaiseFocusChanged() => FocusChanged?.Invoke(this, EventArgs.Empty);
}
