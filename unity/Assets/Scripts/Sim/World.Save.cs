using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public partial class Building
    {
        // Delivery progress in saves from before the walking work crews.
        public double CrewAcc;
    }

    public partial class World
    {
        public string BannerColor;

        static string SaveKey<T>(T value) => value.ToString().ToLowerInvariant();

        static JArray EncodeGrid<T>(T[,] grid, Func<T, JToken> encode)
        {
            var rows = new JArray();
            for (int y = 0; y < N; y++)
            {
                var row = new JArray();
                for (int x = 0; x < N; x++) row.Add(encode(grid[y, x]));
                rows.Add(row);
            }
            return rows;
        }

        public string SaveJson()
        {
            return new JObject
            {
                ["v"] = 1, ["v2"] = true, ["unity"] = true, ["seed"] = Seed, ["day"] = Day,
                ["peasants"] = Peasants, ["soldiers"] = Soldiers, ["trained"] = Trained,
                ["banner"] = BannerColor, ["popAcc"] = PopAcc, ["drafting"] = Drafting, ["starving"] = Starving,
                ["prio"] = JArray.FromObject(Prio), ["hv"] = JArray.FromObject(Hv),
                ["res"] = JObject.FromObject(ResAmt.ToDictionary(p => SaveKey(p.Key), p => p.Value)),
                ["rate"] = JObject.FromObject(Rate.ToDictionary(p => SaveKey(p.Key), p => p.Value)),
                ["deco"] = EncodeGrid(DecoMap, value => value == Deco.None ? JValue.CreateNull() : new JValue(SaveKey(value))),
                ["zone"] = EncodeGrid(ZoneMap, value => value == Zone.None ? JValue.CreateNull() : new JValue(SaveKey(value))),
                ["fert"] = EncodeGrid(Fert, value => new JValue(value)),
                ["rockAmt"] = JObject.FromObject(RockAmtMap), ["elev"] = JObject.FromObject(Elev),
                ["stumpT"] = JObject.FromObject(StumpT), ["sapT"] = JObject.FromObject(SapT),
                ["chop"] = JObject.FromObject(Chop), ["dig"] = JObject.FromObject(Dig),
                ["oreLeft"] = JObject.FromObject(OreLeftMap),
                ["blds"] = new JArray(Blds.Select(b => new JObject
                {
                    ["t"] = b.T, ["x"] = b.X, ["y"] = b.Y, ["sz"] = b.Sz, ["prog"] = b.Prog,
                    ["var"] = b.Var, ["face"] = b.Face, ["zoned"] = b.Zoned, ["crewAcc"] = b.CrewAcc,
                })),
            }.ToString(Formatting.None);
        }

        public static World LoadJson(string json)
        {
            var save = JObject.Parse(json);
            var deco = save["deco"] as JArray;
            var buildings = save["blds"] as JArray;
            int size = deco?.Count ?? 0;
            if ((save.Value<int?>("v") ?? 1) != 1 || size <= 0 || size > N || buildings == null)
                throw new InvalidDataException("Unsupported settlement save.");

            int offset = (N - size) / 2;
            var world = new World(save.Value<int?>("seed") ?? 7, false)
            {
                BannerColor = save.Value<string>("banner"),
                Day = save.Value<int?>("day") ?? 1,
                Peasants = save.Value<int?>("peasants") ?? 6,
                Soldiers = save.Value<int?>("soldiers") ?? 0,
                Trained = save.Value<int?>("trained") ?? 0,
                PopAcc = save.Value<double?>("popAcc") ?? 0,
                Drafting = save.Value<bool?>("drafting") ?? true,
                Starving = save.Value<bool?>("starving") ?? false,
            };
            var priority = save["prio"]?.ToObject<string[]>() ?? Array.Empty<string>();
            world.Prio = priority.Where(Trades.Contains).Concat(Trades).Distinct().ToArray();
            if (size < N) world.GenTerrain((offset, offset + size));
            ReadGrid(deco, world.DecoMap, size, offset, ReadEnum<Deco>);
            ReadGrid(save["zone"] as JArray, world.ZoneMap, size, offset, ReadEnum<Zone>);
            ReadGrid(save["fert"] as JArray, world.Fert, size, offset, token => token.Value<bool>());
            ReadTiles(save["rockAmt"], world.RockAmtMap, size, offset);
            ReadTiles(save["elev"], world.Elev, size, offset);
            ReadTiles(save["stumpT"], world.StumpT, size, offset);
            ReadTiles(save["sapT"], world.SapT, size, offset);
            ReadTiles(save["chop"], world.Chop, size, offset);
            ReadTiles(save["dig"], world.Dig, size, offset);
            ReadTiles(save["oreLeft"], world.OreLeftMap, size, offset);
            foreach (Res resource in Enum.GetValues(typeof(Res)))
            {
                string key = SaveKey(resource);
                world.ResAmt[resource] = save["res"]?[key]?.Value<double>() ?? (resource == Res.Logs ? 0 : world.ResAmt[resource]);
                world.Rate[resource] = save["rate"]?[key]?.Value<double>() ?? 0;
            }

            foreach (var entry in buildings)
            {
                var b = new Building
                {
                    T = entry.Value<string>("t"), X = entry.Value<int>("x"), Y = entry.Value<int>("y"),
                    Sz = entry.Value<int?>("sz") ?? 1, Prog = entry.Value<double?>("prog") ?? 0,
                    Var = entry.Value<int?>("var") ?? 0, Face = entry.Value<int?>("face") ?? -1,
                    Zoned = entry.Value<bool?>("zoned") ?? false, CrewAcc = entry.Value<double?>("crewAcc") ?? 0,
                };
                if (b.T == null || !Names.ContainsKey(b.T) || b.Sz < 1 || b.Sz > 3 ||
                    b.X < 0 || b.Y < 0 || b.X > size - b.Sz || b.Y > size - b.Sz ||
                    b.Face < -1 || b.Face > 3 || b.Var < 0 || b.Var > HutVariants)
                    throw new InvalidDataException("Invalid building in settlement save.");
                b.X += offset;
                b.Y += offset;
                foreach (var (x, y) in b.Tiles())
                {
                    if (world.Grid[y, x] != null) throw new InvalidDataException("Overlapping buildings in settlement save.");
                    world.Grid[y, x] = b;
                }
                if (b.T == "hall")
                {
                    if (world.Hall != null) throw new InvalidDataException("Multiple Town Halls in settlement save.");
                    world.Hall = b;
                }
                world.Blds.Add(b);
            }
            if (world.Hall == null) throw new InvalidDataException("Settlement save has no Town Hall.");

            world.RestoreHeights(save["hv"] as JArray, size, offset);
            if (save.Value<bool?>("unity") != true) world.UpgradeBarracks();
            bool legacyZones = save.Value<bool?>("v2") != true;
            foreach (var b in world.Blds)
            {
                if (b.T == "house" && b.Var == 0) b.Var = world.PickHutVar(b.X, b.Y);
                if (!legacyZones || (b.T != "wood" && b.T != "quarry")) continue;
                for (int y = b.Y - WorkR; y <= b.Y + WorkR; y++)
                    for (int x = b.X - WorkR; x <= b.X + WorkR; x++)
                    {
                        if (!Inb(x, y) || world.ZoneMap[y, x] != Zone.None) continue;
                        if (b.T == "wood" && world.DecoMap[y, x] == Deco.Tree) world.ZoneMap[y, x] = Zone.Wood;
                        if (b.T == "quarry" && world.DecoMap[y, x] == Deco.Rock) world.ZoneMap[y, x] = Zone.Mine;
                    }
            }
            world.Recalc();
            world.ComputeDemand();
            return world;
        }

        static T ReadEnum<T>(JToken token) where T : struct, Enum
        {
            if (token.Type == JTokenType.Null) return default;
            if (Enum.TryParse(token.Value<string>(), true, out T value) && Enum.IsDefined(typeof(T), value)) return value;
            throw new InvalidDataException("Unknown tile type in settlement save.");
        }

        static void ReadGrid<T>(JArray rows, T[,] grid, int size, int offset, Func<JToken, T> decode)
        {
            if (rows == null || rows.Count != size) throw new InvalidDataException("Invalid map in settlement save.");
            for (int y = 0; y < size; y++)
            {
                if (!(rows[y] is JArray row) || row.Count != size) throw new InvalidDataException("Invalid map row in settlement save.");
                for (int x = 0; x < size; x++) grid[y + offset, x + offset] = decode(row[x]);
            }
        }

        static void ReadTiles<T>(JToken token, Dictionary<int, T> map, int size, int offset)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) map.Remove(Tk(x + offset, y + offset));
            if (token == null || token.Type == JTokenType.Null) return;
            foreach (var pair in token.ToObject<Dictionary<int, T>>())
            {
                if (pair.Key < 0 || pair.Key >= size * size) throw new InvalidDataException("Invalid tile index in settlement save.");
                map[Tk(pair.Key % size + offset, pair.Key / size + offset)] = pair.Value;
            }
        }

        void RestoreHeights(JArray heights, int size, int offset)
        {
            int stride = size + 1;
            if (heights?.Count == stride * stride)
            {
                for (int y = 0; y <= size; y++)
                    for (int x = 0; x <= size; x++)
                        Hv[(y + offset) * (N + 1) + x + offset] = heights[y * stride + x].Value<int>();
            }
            else
            {
                for (int y = offset; y <= offset + size; y++)
                    for (int x = offset; x <= offset + size; x++)
                    {
                        int count = 0, sum = 0, max = 0;
                        for (int j = Math.Max(offset, y - 1); j <= y && j < offset + size; j++)
                            for (int i = Math.Max(offset, x - 1); i <= x && i < offset + size; i++)
                            {
                                if (!IsMountain(i, j)) continue;
                                int level = Math.Max(4, Math.Min(8, Noise.JsRound(ElevOf(i, j) / 9.0)));
                                count++;
                                sum += level;
                                max = Math.Max(max, level);
                            }
                        Hv[y * (N + 1) + x] = count == 4 ? Noise.JsRound(sum / 4.0) : Noise.JsRound(max / 2.0);
                    }
            }
            if (size == N && heights?.Count == stride * stride) return;
            var pinned = new HashSet<int>();
            foreach (var building in Blds)
                foreach (var (x, y) in building.Tiles())
                    for (int j = y; j <= y + 1; j++)
                        for (int i = x; i <= x + 1; i++) pinned.Add(j * (N + 1) + i);
            RelaxHeights(Hv, null, null, size < N ? pinned : null);
        }

        void UpgradeBarracks()
        {
            foreach (var b in Blds)
            {
                if (b.T != "barracks" || b.Sz != 1 || !Inb(b.X + 1, b.Y + 1)) continue;
                var expansion = new[] { (b.X + 1, b.Y), (b.X, b.Y + 1), (b.X + 1, b.Y + 1) };
                if (expansion.Any(tile => Grid[tile.Item2, tile.Item1] != null || IsSolid(tile.Item1, tile.Item2))) continue;
                b.Sz = 2;
                foreach (var (x, y) in expansion)
                {
                    Grid[y, x] = b;
                    DecoMap[y, x] = Deco.None;
                    RockAmtMap.Remove(Tk(x, y));
                }
            }
        }
    }
}
