// The settlement panel on the world map: click a settlement to open it over the map's side column. It shows how the settlement feels about
// you and, if it will trade, what it sells and buys and at what price, with buttons to send a caravan (World.SettlementTrade.cs), a button
// to send a gift, and the caravans on the road. Back returns to the map's usual side column.
using System;
using System.Linq;
using System.Text;
using CrownAndHearth.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace CrownAndHearth.Game
{
    public partial class Hud
    {
        RectTransform _townPanel;
        Text _townTitle, _townInfo, _townSells, _townBuys, _townMessage, _townRoad;
        UnityEngine.UI.Image[] _buyButtons, _sellButtons;
        UnityEngine.UI.Image _giftButton, _attackButton, _reinforceButton, _withdrawButton;
        Settlement _town;
        static readonly int[] TradeAmounts = { 10, 50, 100 };

        void BuildSettlementPanel(RectTransform mapPanel)
        {
            _townPanel = MakePanel(mapPanel, "Settlement", Vector2.zero, Vector2.one, new Vector2(708, 14), new Vector2(-14, -66));
            _townPanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.09f, 0.075f, 0.06f, 1f);
            _townTitle = MakeText(_townPanel, "Title", 18, TextAnchor.UpperLeft); _townTitle.fontStyle = FontStyle.Bold;
            TopStretch(_townTitle.rectTransform, 4, 30);
            _townInfo = MakeText(_townPanel, "Info", 14, TextAnchor.UpperLeft); TopStretch(_townInfo.rectTransform, 32, 170);

            _townSells = MakeText(_townPanel, "Sells", 14, TextAnchor.UpperLeft); TopStretch(_townSells.rectTransform, 176, 214);
            _buyButtons = new UnityEngine.UI.Image[TradeAmounts.Length];
            for (int i = 0; i < TradeAmounts.Length; i++)
            {
                int amount = TradeAmounts[i];
                _buyButtons[i] = MakeButton(_townPanel, $"Buy {amount}", Vector2.up, new Vector2(8 + 58 + i * 124, -232), new Vector2(116, 28), () => TradeWithTown(true, amount));
            }
            _townBuys = MakeText(_townPanel, "Buys", 14, TextAnchor.UpperLeft); TopStretch(_townBuys.rectTransform, 256, 294);
            _sellButtons = new UnityEngine.UI.Image[TradeAmounts.Length];
            for (int i = 0; i < TradeAmounts.Length; i++)
            {
                int amount = TradeAmounts[i];
                _sellButtons[i] = MakeButton(_townPanel, $"Sell {amount}", Vector2.up, new Vector2(8 + 58 + i * 124, -312), new Vector2(116, 28), () => TradeWithTown(false, amount));
            }
            _giftButton = MakeButton(_townPanel, $"Send a gift ({World.GiftGold} gold)", Vector2.up, new Vector2(8 + 110, -356), new Vector2(220, 30), GiftToTown);
            _attackButton = MakeButton(_townPanel, "Attack", Vector2.up, new Vector2(8 + 228 + 8 + 80, -356), new Vector2(160, 30), AttackTown);
            // a settlement you hold: send more soldiers, or bring the garrison home (it then pays tribute)
            _reinforceButton = MakeButton(_townPanel, "Send 5 more soldiers", Vector2.up, new Vector2(8 + 110, -356), new Vector2(220, 30), () =>
            {
                var r = _world.ReinforceGarrison(_town, 5);
                _townMessage.text = r.Ok ? $"{r.Count} soldiers march to join the garrison of {_town.Name}." : r.Reason;
                RefreshSettlementPanel();
            });
            _withdrawButton = MakeButton(_townPanel, "Bring them home", Vector2.up, new Vector2(8 + 228 + 8 + 80, -356), new Vector2(160, 30), () =>
            {
                var r = _world.WithdrawGarrison(_town);
                _townMessage.text = r.Ok ? $"The garrison leaves {_town.Name}, which will pay you tribute instead." : r.Reason;
                RefreshSettlementPanel();
            });
            _townMessage = MakeText(_townPanel, "Message", 13, TextAnchor.UpperLeft); _townMessage.color = new Color(1f, 0.7f, 0.5f);
            TopStretch(_townMessage.rectTransform, 378, 430);
            _townRoad = MakeText(_townPanel, "Road", 13, TextAnchor.UpperLeft); _townRoad.color = Dim;
            TopStretch(_townRoad.rectTransform, 434, 620);
            MakeButton(_townPanel, "Back to the map", Vector2.zero, new Vector2(8 + 80, 14 + 15), new Vector2(160, 30), CloseSettlement);
            _townPanel.gameObject.SetActive(false);
        }

        public bool SettlementOpen => _town != null && _townPanel != null && _townPanel.gameObject.activeSelf;

        void OpenSettlement(Settlement s)
        {
            _town = s; _townMessage.text = "";
            if (_world != null && _world.CanAttack(s).Ok) _world.TownCamp(s).PrepareSite();   // its battle ground is made in the background, in case you attack
            _townPanel.gameObject.SetActive(true);
            _townPanel.SetAsLastSibling(); _tip.SetAsLastSibling(); _cursorTip.SetAsLastSibling();
            RefreshSettlementPanel();
        }

        void CloseSettlement()
        {
            _town = null;
            if (_townPanel != null) _townPanel.gameObject.SetActive(false);
        }

        /// <summary>Plan an attack: the settlement opens in the game view like a raider camp, with its garrison and its lord.</summary>
        void AttackTown()
        {
            if (_town == null || _world == null) return;
            var ok = _world.CanAttack(_town);
            if (!ok.Ok) { _townMessage.text = ok.Reason; return; }
            var camp = _world.TownCamp(_town);
            if (!camp.SiteReady) { camp.PrepareSite(); _townMessage.text = $"Scouts are mapping {_town.Name}. Try again in a moment."; return; }
            CloseSettlement();
            OpenCamp(camp);
        }

        void TradeWithTown(bool buy, int amount)
        {
            if (_town == null || _world == null) return;
            var r = buy ? _world.BuyFrom(_town, amount) : _world.SellTo(_town, amount);
            _townMessage.text = r.Ok
                ? (buy ? $"A caravan sets out to fetch {r.Count} {World.GoodName(_town.Makes)}. It will be home in {_world.SettlementMarchDays(_town)} days."
                       : $"A caravan sets out with {r.Count} {World.GoodName(_town.Needs)}. The gold comes home in {_world.SettlementMarchDays(_town)} days.")
                : r.Reason;
            RefreshSettlementPanel();
        }

        void GiftToTown()
        {
            if (_town == null || _world == null) return;
            var r = _world.SendGift(_town);
            _townMessage.text = r.Ok ? $"A messenger sets out with {World.GiftGold} gold. {_town.Name} will have it in {_world.SettlementMarchDays(_town)} days." : r.Reason;
            RefreshSettlementPanel();
        }

        /// <summary>Called every frame (cheap when the panel is closed): keeps prices, stocks and the caravans on the road up to date.</summary>
        void RefreshSettlementPanel()
        {
            if (!SettlementOpen || _world == null) return;
            var s = _town;
            string hex = ColorUtility.ToHtmlStringRGB(WorldMapRenderer.RealmColor(s));
            _townTitle.text = $"<color=#{hex}>{s.Tier.ToString().ToUpper()}</color> {s.Name}";
            var sb = new StringBuilder();
            sb.AppendLine($"{s.Population:0} people · {_world.SettlementKm(s)} km away, {_world.SettlementMarchDays(s)} days by caravan");
            var liege = _world.SettlementById(s.Liege); var seat = _world.SettlementById(s.Realm);
            if (s.Allegiance == Allegiance.Occupied)
                sb.AppendLine($"<color=#FFC428>Yours</color>: held by {s.Garrison} of your soldiers" + (s.Garrison < World.GarrisonNeeded(s) ? $" (<color=#FF8C70>needs {World.GarrisonNeeded(s)}: it may rise up</color>)" : $" (needs {World.GarrisonNeeded(s)})"));
            else if (s.Allegiance == Allegiance.Tributary)
                sb.AppendLine("<color=#FFC428>Pays you tribute</color> every month (it may stop paying)");
            else
                sb.AppendLine(liege != null ? $"Answers to {liege.Name}" + (seat != null && seat != liege ? $", in the realm of {seat.Name}" : "")
                    : seat == s ? $"Seat of a realm of {_world.AllSubjects(s)} settlements" : "Free: answers to nobody");
            if (s.Allegiance == Allegiance.Own) sb.AppendLine($"Garrison: about {World.Garrison(s)} defenders, its lord and his guard");
            sb.AppendLine($"{Settlements.PersonalityName(s.Personality)}: {Settlements.PersonalityNote(s.Personality)}");
            sb.AppendLine($"Towards you: <b>{Settlements.AttitudeName(s.Attitude)}</b> ({s.Attitude:+0;-0;0})");
            var can = _world.CanTrade(s);
            sb.Append(s.WillTrade ? (can.Ok ? "Willing to trade. Each finished trade warms it a little." : can.Reason) : can.Reason);
            _townInfo.text = sb.ToString();

            bool trading = s.WillTrade;
            _townSells.text = trading
                ? $"<b>Sells {World.GoodName(s.Makes)}</b> at {World.PriceToBuy(s):0.##} gold each · {World.LeftToBuy(s)} left this month · you have {_world.ResAmt[Res.Gold]:0} gold"
                : $"Makes {World.GoodName(s.Makes)}";
            _townBuys.text = trading
                ? $"<b>Buys {World.GoodName(s.Needs)}</b> at {World.PriceToSell(s):0.##} gold each · wants {World.LeftToSell(s)} more this month · you have {_world.ResAmt[s.Needs]:0}"
                : $"Needs {World.GoodName(s.Needs)}";
            foreach (var b in _buyButtons) b.gameObject.SetActive(trading);
            foreach (var b in _sellButtons) b.gameObject.SetActive(trading);
            foreach (var b in _buyButtons.Concat(_sellButtons)) b.color = can.Ok ? Btn : new Color(0.2f, 0.17f, 0.13f, 1f);
            _giftButton.color = _world.ResAmt[Res.Gold] >= World.GiftGold && s.Attitude < World.GiftCap ? Btn : new Color(0.2f, 0.17f, 0.13f, 1f);
            bool held = s.Allegiance == Allegiance.Occupied;
            _giftButton.gameObject.SetActive(!held); _attackButton.gameObject.SetActive(!held);
            _reinforceButton.gameObject.SetActive(held); _withdrawButton.gameObject.SetActive(held);
            _attackButton.color = _world.CanAttack(s).Ok ? new Color(0.45f, 0.16f, 0.12f, 1f) : new Color(0.2f, 0.17f, 0.13f, 1f);

            var road = _world.Caravans.OrderBy(c => c.ArriveDay).ToList();
            var lines = new StringBuilder($"<b>On the road</b> ({_world.TradeCaravansOut} of {_world.Markets} market caravans out)\n");
            if (road.Count == 0) lines.Append("Nothing.");
            foreach (var c in road.Take(8))
            {
                var to = _world.SettlementById(c.SettlementId); string where = to?.Name ?? "?"; int days = Math.Max(0, c.ArriveDay - _world.Day);
                lines.AppendLine(c.Kind switch
                {
                    CaravanKind.Buy => $"{c.Amount} {World.GoodName(c.Res)} coming from {where}: {days} days",
                    CaravanKind.Sell => $"{c.Amount} {World.GoodName(c.Res)} going to {where} for {c.Gold} gold: {days} days",
                    _ => $"A gift of {c.Gold} gold going to {where}: {days} days",
                });
            }
            _townRoad.text = lines.ToString();
        }
    }
}
