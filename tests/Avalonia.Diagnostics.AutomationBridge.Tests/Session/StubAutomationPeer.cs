using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace Avalonia.Diagnostics.AutomationBridge.Tests.Session;

/// <summary>
/// Minimal <see cref="AutomationPeer"/> implementation for session unit tests.
/// All abstract members return safe no-op or configurable values.
/// </summary>
internal class StubAutomationPeer : AutomationPeer
{
    private AutomationPeer? _parent;
    private readonly List<AutomationPeer> _children = new();
    private readonly Dictionary<Type, object> _providers = new();

    // Configurable properties
    public AutomationControlType ControlType { get; set; } = AutomationControlType.Custom;
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool KeyboardFocus { get; set; }
    public bool KeyboardFocusable { get; set; }
    public Rect BoundingRectangle { get; set; }
    public int SetFocusCallCount { get; private set; }

    /// <summary>
    /// Registers a provider that the peer can later return for the requested provider type.
    /// </summary>
    public void RegisterProvider<T>(T provider) where T : class
        => _providers[typeof(T)] = provider;

    /// <summary>Sets a parent peer for parent-walking tests.</summary>
    public void SetParent(AutomationPeer? parent) => _parent = parent;

    /// <summary>Adds a child peer for child-enumeration tests.</summary>
    public void AddChild(StubAutomationPeer child)
    {
        _children.Add(child);
        child._parent = this;
    }

    public bool RemoveChild(AutomationPeer child)
    {
        if (_children.Remove(child))
        {
            if (child is StubAutomationPeer stubChild)
                stubChild._parent = null;

            return true;
        }

        return false;
    }

    public void RaiseChildrenChanged() => RaiseChildrenChangedEvent();

    // ---------- abstract overrides ----------

    protected override void BringIntoViewCore() { }
    protected override string? GetAcceleratorKeyCore() => null;
    protected override string? GetAccessKeyCore() => null;
    protected override AutomationControlType GetAutomationControlTypeCore() => ControlType;
    protected override string? GetAutomationIdCore() => AutomationId;
    protected override Rect GetBoundingRectangleCore() => BoundingRectangle;
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => _children;
    protected override string GetClassNameCore() => ClassName;
    protected override AutomationPeer? GetLabeledByCore() => null;
    protected override string? GetNameCore() => Name;
    protected override AutomationPeer? GetParentCore() => _parent;
    protected override bool HasKeyboardFocusCore() => KeyboardFocus;
    protected override bool IsContentElementCore() => true;
    protected override bool IsControlElementCore() => true;
    protected override bool IsEnabledCore() => Enabled;
    protected override bool IsKeyboardFocusableCore() => KeyboardFocusable;
    protected override void SetFocusCore() => SetFocusCallCount++;
    protected override bool ShowContextMenuCore() => false;
    // Cross-assembly override: the base declares 'protected internal abstract'.
    // From outside the declaring assembly, 'protected' is the correct override modifier.
    protected override bool TrySetParent(AutomationPeer? parent)
    {
        _parent = parent;
        return true;
    }

    protected override object? GetProviderCore(Type providerType)
        => _providers.TryGetValue(providerType, out var p) ? p : null;
}
