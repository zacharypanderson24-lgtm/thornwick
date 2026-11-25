// The Vessels window: the town's boats and the boatyard's order list. Big management window with exact numeric entry (how many to order).
// Assignment is automatic (a ferry boat goes to a ferry route, fishing boats to docks, a cargo barge to a port); the list says where each is.
using System.Text;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        RectTransform _vesselPanel; Text _vesselYard, _vesselOrderList, _vesselList; InputField _vesselQty; World _vesselWorld; int _vesselShown = -1;
        static readonly VesselType[] VesselTypes = { VesselType.FishingBoat, VesselType.FerryBoat, VesselType.CargoBarge };

        void BuildVessels(RectTransform root)
        {
            MakeButton(root, "Vessels", Vector2.one, new Vector2(-82, -275), new Vector2(148, 30), () => { _vesselPanel.gameObject.SetActive(!_vesselPanel.gameObject.activeSelf); _vesselShown = -1; });
            _vesselPanel = MakePanel(root, "Vessels", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _vesselPanel.GetComponent<Image>().color = new Color(0.04f, 0.03f, 0.02f, 0.92f);
            var box = MakePanel(_vesselPanel, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-480, -300), new Vector2(480, 300));
            var title = MakeText(box, "Title", 26, TextAnchor.UpperCenter); title.fontStyle = FontStyle.Bold; title.text = "Vessels";
            Stretch(title.rectTransform, new Vector2(20, 540), new Vector2(-20, -10));
            _vesselYard = MakeText(box, "Yard", 16, TextAnchor.UpperLeft); _vesselYard.color = Dim;
            Stretch(_vesselYard.rectTransform, new Vector2(24, 470), new Vector2(-24, -48));
            // orders: how many (exact), then one button per kind of vessel
            var qtyLabel = MakeText(box, "QtyLabel", 17, TextAnchor.MiddleLeft); qtyLabel.text = "How many";
            var ql = qtyLabel.rectTransform; ql.anchorMin = ql.anchorMax = new Vector2(0, 1); ql.anchoredPosition = new Vector2(110, -110); ql.sizeDelta = new Vector2(180, 30);
            _vesselQty = MilField(box, 18, true, TextAnchor.MiddleCenter); _vesselQty.text = "1";
            var qr = (RectTransform)_vesselQty.transform; qr.anchorMin = qr.anchorMax = new Vector2(0, 1); qr.anchoredPosition = new Vector2(260, -110); qr.sizeDelta = new Vector2(80, 30);
            for (int i = 0; i < VesselTypes.Length; i++)
            {
                var t = VesselTypes[i]; var spec = World.Spec(t);
                MakeButton(box, $"Order {spec.Name.ToLower()}", new Vector2(0, 1), new Vector2(170, -152 - i * 44), new Vector2(300, 36), () =>
                {
                    int.TryParse(_vesselQty.text, out int n); n = Mathf.Clamp(n, 1, 20);
                    var r = _vesselWorld?.OrderVessel(t, n); Toast(r == null ? "No settlement" : r.Value.Ok ? $"Ordered {n} {spec.Name.ToLower()}{(n == 1 ? "" : "s")}." : r.Value.Reason, 5);
                    _vesselShown = -1;
                });
                var info = MakeText(box, "Cost" + i, 14, TextAnchor.MiddleLeft); info.color = Dim;
                info.text = $"wood {spec.Wood}, flax {spec.Flax}" + (spec.Metal > 0 ? $", metal {spec.Metal}" : "") + $"; {spec.BuildDays} yard-days; crew {spec.Crew}";
                var ir = info.rectTransform; ir.anchorMin = ir.anchorMax = new Vector2(0, 1); ir.anchoredPosition = new Vector2(500, -152 - i * 44); ir.sizeDelta = new Vector2(320, 36);
            }
            MakeButton(box, "Cancel last order", new Vector2(0, 1), new Vector2(110, -296), new Vector2(180, 32), () => { if (_vesselWorld != null && _vesselWorld.VesselOrders.Count > 0) { _vesselWorld.CancelOrder(_vesselWorld.VesselOrders.Count - 1); _vesselShown = -1; } });
            MakeButton(box, "Cancel all", new Vector2(0, 1), new Vector2(300, -296), new Vector2(160, 32), () => { if (_vesselWorld != null) { while (_vesselWorld.VesselOrders.Count > 0) _vesselWorld.CancelOrder(0); _vesselShown = -1; } });
            _vesselOrderList = MakeText(box, "Orders", 15, TextAnchor.UpperLeft);
            Stretch(_vesselOrderList.rectTransform, new Vector2(24, 20), new Vector2(-500, -330));
            _vesselList = MakeText(box, "List", 15, TextAnchor.UpperLeft);
            Stretch(_vesselList.rectTransform, new Vector2(470, 20), new Vector2(-24, -310));
            MakeButton(box, "Close", new Vector2(1, 0), new Vector2(-70, 28), new Vector2(110, 34), () => _vesselPanel.gameObject.SetActive(false));
            _vesselPanel.gameObject.SetActive(false);
        }

        void UpdateVessels(World w)
        {
            _vesselWorld = w;
            if (_vesselPanel == null || !_vesselPanel.gameObject.activeSelf) return;
            int stamp = w.Version * 31 + w.Day * 7 + w.Vessels.Count * 3 + w.VesselOrders.Count;
            if (stamp == _vesselShown) return; _vesselShown = stamp;
            var sb = new StringBuilder();
            if (w.Waters == null) sb.Append("This world has no surface water, so there are no boats.");
            else sb.Append($"Boatyards: {w.Boatyards} on the road network, {w.YardCapacity:0.#} crews at work. Materials in store: wood {w.ResAmt[Res.Wood]:0}, flax {w.ResAmt[Res.Flax]:0}, metal {w.ResAmt[Res.Metal]:0}.   Boats are assigned automatically: a ferry boat to a ferry route, fishing boats to docks, a cargo barge to a port.");
            _vesselYard.text = sb.ToString();
            var o = new StringBuilder("<b>Orders</b> (built one at a time, in this order)\n");
            if (w.VesselOrders.Count == 0) o.Append("none");
            for (int i = 0; i < w.VesselOrders.Count; i++) o.Append($"{i + 1}. {World.Spec(w.VesselOrders[i].Type).Name}: {w.OrderStatus(w.VesselOrders[i])}\n");
            _vesselOrderList.supportRichText = true; _vesselOrderList.text = o.ToString();
            var l = new StringBuilder($"<b>Vessels</b> ({w.Vessels.Count})\n");
            if (w.Vessels.Count == 0) l.Append("none yet");
            foreach (var v in w.Vessels) l.Append($"#{v.Id} {World.Spec(v.Type).Name}, condition {v.Condition:0}%{(w.InService(v) ? "" : " (out of service)")}: {w.AssignmentText(v)}\n");
            _vesselList.supportRichText = true; _vesselList.text = l.ToString();
        }
    }
}
