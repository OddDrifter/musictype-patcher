using MusicTypePatcher;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using System;
using System.Linq;

await SynthesisPipeline.Instance.SetTypicalOpen(GameRelease.SkyrimSE, new("CombinedMusicTypes.esp", ModType.LightMaster))
    .AddPatch<ISkyrimMod, ISkyrimModGetter>(Apply)
    .Run(args);

static void Apply(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
{
    Console.WriteLine($"Report any issues to https://github.com/OddDrifter/musictype-patcher");

    var query = state.LoadOrder.PriorityOrder.OnlyEnabledAndExisting()
        .WinningOverrides<IMusicTypeGetter>()
        .Where(u => Utility.HasMultipleOverrides<IMusicType, IMusicTypeGetter>(state, u.FormKey));

    foreach (var record in query)
    {
        var copy = new Utility.MusicTypeResolver(state, record.FormKey).Resolve();
        if (record.Equals(copy, Utility.Mask) is false)
        {
            state.PatchMod.MusicTypes.Set(copy);
            continue;
        }
        Console.WriteLine($"Skipping {copy.EditorID}{Environment.NewLine}");
    }
}
