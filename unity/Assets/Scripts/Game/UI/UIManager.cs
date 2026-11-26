// What the management interface is doing right now: which menu is open, which tab, and what Escape should close next.
// Pure C# (no UnityEngine) so it is tested directly; the Hud draws what this says and calls back into it.
using System;
using System.Collections.Generic;
using CrownAndHearth.Sim;

namespace CrownAndHearth.Game.UI
{
    /// <summary>What Escape did, in the brief's order: a dialog first, then a placement in progress, then the open menu, then nothing (the caller may pause).</summary>
    public enum EscapeResult { None, ClosedModal, CancelledPlacement, ClosedMenu }

    public sealed class UIManager
    {
        public MenuId? Open { get; private set; }
        public int Tab { get; private set; }
        readonly List<string> _modals = new List<string>();
        public event Action Changed;

        public bool HasModal => _modals.Count > 0;
        public string TopModal => _modals.Count > 0 ? _modals[_modals.Count - 1] : null;

        /// <summary>Open a menu (or close it when it is already open). False, with the reason, when the menu is locked.</summary>
        public bool Toggle(MenuId id, World w, out string reason)
        {
            reason = MenuRegistry.WhyLocked(id, w);
            if (reason != null) return false;
            if (Open == id) Close(); else { Open = id; Tab = 0; Changed?.Invoke(); }
            return true;
        }

        public void Close() { if (Open == null) return; Open = null; Changed?.Invoke(); }
        public void SetTab(int tab) { if (tab == Tab) return; Tab = tab; Changed?.Invoke(); }

        public void PushModal(string id) { _modals.Remove(id); _modals.Add(id); Changed?.Invoke(); }
        public void PopModal(string id) { if (_modals.Remove(id)) Changed?.Invoke(); }

        /// <summary>Escape. The caller says whether a placement tool is active; when this returns CancelledPlacement the caller cancels it.</summary>
        public EscapeResult Escape(bool placementActive)
        {
            if (_modals.Count > 0) { _modals.RemoveAt(_modals.Count - 1); Changed?.Invoke(); return EscapeResult.ClosedModal; }
            if (placementActive) return EscapeResult.CancelledPlacement;
            if (Open != null) { Close(); return EscapeResult.ClosedMenu; }
            return EscapeResult.None;
        }
    }
}
