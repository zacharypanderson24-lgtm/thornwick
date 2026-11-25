# Happiness: how it works and how to add to it

Happiness is one number from 0 to 100 (`World.Happiness`), shown as the **Mood** card at the right end of the strip under the build bar. It drifts a little each day toward a **target**: 50, plus every cause in force. It falls faster than it rises.

- Content: 60 and above. Uneasy: 35 to 60.
- **Below 35 an uprising becomes possible**, and the lower it goes the likelier it gets. A town that stays at 8 or lower for 10 days always rises.
- Players lift it with the **Festival** button (25 gold, 30 food, once every 40 days) or by fixing what is wrong.

## The three kinds of cause

| Kind | Use it for | How |
|---|---|---|
| **Factor** | something that is true right now: people starving, homes without water, jobs for everyone | add one line to the `Factors` table in `World.Happiness.cs` |
| **Mood** | an effect that lasts some days: a festival (+10 for 20 days) | `AddMood(id, delta, days, text)` where it begins |
| **Shift** | a one-off jump: a building burned (-4) | `HappinessShift(delta, text)` where it happens |

The bar, the hover list and the uprising risk all read the same ledger (`HappinessLedger()`), so a new cause shows up everywhere with no other change.

### Adding a factor

1. Write a method that returns an effect, or `null` when it does not apply:

```csharp
static HappinessEffect? ChapelFactor(World w)
{
    if (!w.Blds.Any(b => b.T == "chapel")) return null;
    return new HappinessEffect(+8, "A chapel to pray in");
}
```

2. Add it to the table:

```csharp
("chapel", ChapelFactor),
```

To remove a cause, delete its line. The text is what the player reads, so write it as a short sentence.

### Adding a mood or a shift

```csharp
AddMood("harvest-feast", +6, 15, "A good harvest");   // lasts 15 days; the same id replaces an earlier one
HappinessShift(-4, "A building burned");              // moves it now, then it drifts back
```

## Numbers

All the numbers live in `HappinessTuning` (start, baseline, fall and rise rates, thresholds) in `World.Happiness.cs`, the festival numbers beside `HoldFestival`, and the uprising numbers at the top of `World.Uprising.cs`.

## Uprisings

`World.Uprising.cs`. The workers rise out of their workplaces and march on the Town Hall using the raid system: your soldiers fight them, the Hall can be hit, civilians hide inside it. Rebels are drawn with their trade's militia sprites. Each rebel is one peasant fewer. If the Hall is destroyed the game is lost. Soldiers at home are the defence.

## Tests

`HappinessTests.cs` covers the number, all three kinds of cause, festivals, uprisings and saving. Happiness only moves when the campaign is on (`World.StartCampaign()`), so tests that tick plain worlds are not affected.
