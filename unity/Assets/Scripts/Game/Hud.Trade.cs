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
        // which of its goods the buy and sell rows are on (cycled with the "Other good" buttons)
        int _buyGood, _sellGood;
        UnityEngine.UI.Image _nextBuyGood, _nextSellGood;

        Res BuyGood(Settlement s) => s.Produce.Count == 0 ? s.Makes : s.Produce[_buyGood % s.Produce.Count].res;
        Res SellGood(Settlement s) => s.Wants.Count == 0 ? s.Needs : s.Wants[_sellGood % s.Wants.Count].res;

        static string OtherGoods(System.Collections.Generic.List<(Res res, double share)> goods, Res shown) =>
            Settlements.GoodsList(goods.Where(g => g.res != shown));

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
                _buyButtons[i] = MakeButton(_townPanel, $"Buy {amount}", Vector2.up, new Vector2(8 + 48 + i * 104, -232), new Vector2(96, 28), () => TradeWithTown(true, amount));
            }
            _nextBuyGood = MakeButton(_townPanel, "Other good ›", Vector2.up, new Vector2(8 + 48 + 3 * 104, -232), new Vector2(96, 28), () => { _buyGood++; _townMessage.text = ""; RefreshSettlementPanel(); });
            _townBuys = MakeText(_townPanel, "Buys", 14, TextAnchor.UpperLeft); TopStretch(_townBuys.rectTransform, 256, 294);
            _sellButtons = new UnityEngine.UI.Image[TradeAmounts.Length];
            for (int i = 0; i < TradeAmounts.Length; i++)
            {
                int amount = TradeAmounts[i];
                _sellButtons[i] = MakeButton(_townPanel, $"Sell {amount}", Vector2.up, new Vector2(8 + 48 + i * 104, -312), new Vector2(96, 28), () => TradeWithTown(false, amount));
            }
            _nextSellGood = MakeButton(_townPanel, "Other good ›", Vector2.up, new Vector2(8 + 48 + 3 * 104, -312), new Vector2(96, 28), () => { _sellGood++; _townMessage.text = ""; RefreshSettlementPanel(); });
            _giftButton = MakeButton(_townPanel, $"Send a gift ({World.GiftGold} gold)", Vector2.up, new Vector2(8 + 110, -356), new Vector2(220, 30), GiftToTown);
            _attackButton = MakeButton(_townPanel, "Attack", Vector2.up, new Vector2(8 + 228 + 8 + 80, -356), new Vector2(160, 30), AttackTown);
            // a settlement you hold: send more soldiers, or bring the garrison home (it then pays tribute)
            _reinforceButton = MakeButton(_townPanel, "Send 5 more soldiers", Vector2.up, new Vector2(8 + 110, -356), new Vector2(220, 30), () =>
            {
                var r = _world.ReinforceGarrison(_town, 5);
                _townMessage.text = r.Ok ? $"{r.Count} soldiers march to join the garrison of {_town.Name}: they arrive in {_world.SettlementMarchDays(_town)} days." : r.Reason;
                RefreshSettlementPanel();
            });
            _withdrawButton = MakeButton(_townPanel, "Bring them home", Vector2.up, new Vector2(8 + 228 + 8 + 80, -356), new Vector2(160, 30), () =>
            {
                var r = _world.WithdrawGarrison(_town);
                _townMessage.text = r.Ok ? $"The garrison leaves {_town.Name} and marches home ({_world.SettlementMarchDays(_town)} days); it will pay you tribute instead." : r.Reason;
                RefreshSettlementPanel();
            });
            // your army waiting here after taking it: the same four choices as the battle report (they replace the trade rows)
            var fates = new[]
            {
                (ConquestChoice.Leave, "Take the loot, leave", 0, 0), (ConquestChoice.Sack, "Sack it", 1, 0),
                (ConquestChoice.Tribute, "Demand tribute", 0, 1), (ConquestChoice.Occupy, "Occupy it", 1, 1),
            };
            _fateButtons = new UnityEngine.UI.Image[fates.Length];
            for (int i = 0; i < fates.Length; i++)
            {
                var (choice, label, col, row) = fates[i];
                _fateButtons[i] = MakeButton(_townPanel, label, Vector2.up, new Vector2(8 + 98 + col * 204, -(232 + row * 36)), new Vector2(196, 30), () => DecideFate(choice));
            }
            _fateLess = MakeButton(_townPanel, "-", Vector2.up, new Vector2(8 + 230, -310), new Vector2(34, 26), () => { _fateGarrison = Math.Max(0, _fateGarrison - 1); RefreshSettlementPanel(); });
            _fateMore = MakeButton(_townPanel, "+", Vector2.up, new Vector2(8 + 380, -310), new Vector2(34, 26), () => { _fateGarrison++; RefreshSettlementPanel(); });
            _fateLabel = MakeText(_townPanel, "FateLabel", 13, TextAnchor.MiddleLeft);
            var fr = _fateLabel.rectTransform; fr.anchorMin = fr.anchorMax = Vector2.up; fr.pivot = new Vector2(0, 0.5f); fr.sizeDelta = new Vector2(210, 26); fr.anchoredPosition = new Vector2(8, -310);
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
            _town = s; _townMessage.text = ""; _buyGood = 0; _sellGood = 0;
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
            var good = buy ? BuyGood(_town) : SellGood(_town);
            var r = buy ? _world.BuyFrom(_town, amount, good) : _world.SellTo(_town, amount, good);
            _townMessage.text = r.Ok
                ? (buy ? $"A caravan sets out to fetch {r.Count} {World.GoodName(good)}. It will be home in {_world.SettlementMarchDays(_town)} days."
                       : $"A caravan sets out with {r.Count} {World.GoodName(good)}. The gold comes home in {_world.SettlementMarchDays(_town)} days.")
                : r.Reason;
            RefreshSettlementPanel();
        }

        UnityEngine.UI.Image[] _fateButtons;
        UnityEngine.UI.Image _fateLess, _fateMore;
        Text _fateLabel;
        int _fateGarrison = -1;

        bool FateHere(Settlement s) => _world?.PendingConquest is PendingConquest p && p.SettlementId == s.Id;

        void DecideFate(ConquestChoice choice)
        {
            if (_town == null || _world == null || !FateHere(_town)) return;
            var r = _world.ResolveConquest(choice, _fateGarrison);
            _townMessage.text = r.Ok ? _world.LastConquestNews : r.Reason;
            _fateGarrison = -1;
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
            {
                int marching = _world.MarchingTo(s);
                sb.AppendLine($"<color=#FFC428>Yours</color>: held by {s.GarrisonInfantry} infantry and {s.GarrisonArchers} archers" +
                    (s.Garrison < World.GarrisonNeeded(s) ? $" (<color=#FF8C70>needs {World.GarrisonNeeded(s)}: it may rise up</color>)" : $" (needs {World.GarrisonNeeded(s)})") +
                    (marching > 0 ? $"; {marching} more on the way" : ""));
            }
            if (s.Allegiance == Allegiance.Occupied)
            {
                var why = _world.UnrestReasons(s);
                double change = why.Sum(r => r.change);
                string col = s.Unrest >= 75 ? "FF6050" : s.Unrest >= 50 ? "FFB080" : "D8D0B8";
                sb.AppendLine($"Unrest: <color=#{col}>{s.Unrest:0} ({World.UnrestName(s.Unrest)})</color>, {(change >= 0 ? "rising" : "falling")} {Math.Abs(change):0.#} a month. " +
                              string.Join("; ", why.Where(r => Math.Abs(r.change) >= 0.5).OrderByDescending(r => Math.Abs(r.change)).Take(2).Select(r => $"{r.why} {r.change:+0.#;-0.#}")));
            }
            else if (s.Allegiance == Allegiance.Tributary)
                sb.AppendLine("<color=#FFC428>Pays you tribute</color> every month (it may stop paying)");
            else
                sb.AppendLine(liege != null ? $"Answers to {liege.Name}" + (seat != null && seat != liege ? $", in the realm of {seat.Name}" : "")
                    : seat == s ? $"Seat of a realm of {_world.AllSubjects(s)} settlements" : "Free: answers to nobody");
            if (s.Allegiance == Allegiance.Own)
            {
                int now = World.DefendersOf(s), full = World.Garrison(s);
                sb.AppendLine(now < full ? $"Garrison: about {now} defenders of its usual {full} (it lost men fighting you and is replacing them), its lord and his guard"
                                         : $"Garrison: about {full} defenders, its lord and his guard");
            }
            if (s.Devastated) sb.AppendLine($"<color=#FF8C70>Laid waste by a sack</color>: it trades and pays at a fraction and does not grow for {Math.Max(0, s.DevastatedUntil - _world.Day)} more days");
            sb.AppendLine($"{Settlements.PersonalityName(s.Personality)}: {Settlements.PersonalityNote(s.Personality)}");
            string army = _world.ArmyNews(s);
            if (army.Length > 0) sb.AppendLine($"<color=#FF8C70>{army}</color>");
            sb.AppendLine($"Towards you: <b>{Settlements.AttitudeName(s.Attitude)}</b> ({s.Attitude:+0;-0;0})");
            var can = _world.CanTrade(s);
            sb.Append(s.WillTrade ? (can.Ok ? "Willing to trade. Each finished trade warms it a little." : can.Reason) : can.Reason);
            _townInfo.text = sb.ToString();

            bool trading = s.WillTrade;
            Res bg = BuyGood(s), sg = SellGood(s);
            string moreSold = s.Produce.Count > 1 ? $"\nAlso sells {OtherGoods(s.Produce, bg)}" : "";
            string moreBought = s.Wants.Count > 1 ? $"\nAlso buys {OtherGoods(s.Wants, sg)}" : "";
            _townSells.text = trading
                ? $"<b>Sells {World.GoodName(bg)}</b> at {World.PriceToBuy(s, bg):0.##} gold each · {World.LeftToBuy(s, bg)} left this month" + moreSold
                : $"Produces {Settlements.GoodsList(s.Produce)}";
            _townBuys.text = trading
                ? $"<b>Buys {World.GoodName(sg)}</b> at {World.PriceToSell(s, sg):0.##} gold each · wants {World.LeftToSell(s, sg)} more · you have {_world.ResAmt[sg]:0}" + moreBought
                : $"Needs {Settlements.GoodsList(s.Wants)}";
            foreach (var b in _buyButtons) b.gameObject.SetActive(trading);
            foreach (var b in _sellButtons) b.gameObject.SetActive(trading);
            _nextBuyGood.gameObject.SetActive(trading && s.Produce.Count > 1);
            _nextSellGood.gameObject.SetActive(trading && s.Wants.Count > 1);
            foreach (var b in _buyButtons.Concat(_sellButtons)) b.color = can.Ok ? Btn : new Color(0.2f, 0.17f, 0.13f, 1f);
            _giftButton.color = _world.ResAmt[Res.Gold] >= World.GiftGold && s.Attitude < World.GiftCap ? Btn : new Color(0.2f, 0.17f, 0.13f, 1f);
            bool held = s.Allegiance == Allegiance.Occupied;
            _giftButton.gameObject.SetActive(!held); _attackButton.gameObject.SetActive(!held);
            _reinforceButton.gameObject.SetActive(held); _withdrawButton.gameObject.SetActive(held);
            _attackButton.color = _world.CanAttack(s).Ok ? new Color(0.45f, 0.16f, 0.12f, 1f) : new Color(0.2f, 0.17f, 0.13f, 1f);

            // your army waiting here: the four choices take the place of the trade rows
            bool fate = FateHere(s);
            foreach (var b in _fateButtons) b.gameObject.SetActive(fate);
            _fateLess.gameObject.SetActive(fate); _fateMore.gameObject.SetActive(fate); _fateLabel.gameObject.SetActive(fate);
            if (fate)
            {
                var p = _world.PendingConquest; int have = p.Infantry + p.Archers;
                if (_fateGarrison < 0) _fateGarrison = Math.Min(have, World.GarrisonNeeded(s));
                _fateGarrison = Math.Clamp(_fateGarrison, 0, have);
                foreach (var b in _buyButtons.Concat(_sellButtons)) b.gameObject.SetActive(false);
                _nextBuyGood.gameObject.SetActive(false); _nextSellGood.gameObject.SetActive(false);
                _giftButton.gameObject.SetActive(false); _attackButton.gameObject.SetActive(false);
                var (gold, goods) = World.SackYield(s);
                _townSells.text = $"<b>Your army holds {s.Name}</b>: {p.Infantry} infantry and {p.Archers} archers, carrying {p.Loot.Values.Sum()} goods. Decide its fate:";
                _townBuys.text = "";
                _fateLabel.text = $"Garrison if you occupy: {_fateGarrison} of {have}";
                var occupy = _world.CanResolve(ConquestChoice.Occupy, _fateGarrison);
                _fateButtons[3].color = occupy.Ok ? Btn : new Color(0.2f, 0.17f, 0.13f, 1f);
                if (_townMessage.text.Length == 0 || !occupy.Ok)
                    _townMessage.text = $"Sacking: about {gold} gold and {goods} goods ({Settlements.GoodsList(s.Produce)}) there; the carts can take {World.SackRoom(p)}, the rest burns." +
                                        (occupy.Ok ? "" : $" Occupy: {occupy.Reason}.");
            }

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
