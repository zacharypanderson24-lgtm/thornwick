# Running the Unity tests

Command (run in the background; one Unity process per project):

```
"C:/Program Files/Unity/Hub/Editor/6000.6.9f1/Editor/Unity.exe" -batchmode -nographics -projectPath "<worktree>/unity" -runTests -testPlatform EditMode -testResults <out.xml> -logFile <out.log>
```

- **Everything** (use before a PR): about 4 minutes on a quiet machine (it was about 30 before the world-generation caches).
- **Fast** (use while working): add `-testCategory "!Slow"`. It skips the handful of tests that generate many worlds from different seeds (the six slowest, 5 to 17 seconds each).
- **One class or test:** add `-testFilter "CrownAndHearth.Tests.ClassName"` (several with `;`).

## Why it is quicker now

Making a world cost about 1 second and nearly every test makes one. Mountain heights, surface water (kept in the same compact form a save uses) and the groundwater patches are now cached or computed once per tile, keyed by the world's settings (`World.Ranges.cs`, `World.Hydrology.cs`, `World.Water.cs`). Each world still gets its own copy, so nothing a test changes can leak into the next. Different seeds still pay the full cost, which is what the Slow tests do on purpose.

If you add a test that loops over many seeds or presets, tag it `[Test, Category("Slow")]`.
