using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;

namespace MusicTypePatcher
{
    public class Patcher
    {
        public static uint Timestamp { get; } = (uint)(Math.Max(1, DateTime.Today.Year - 2000) << 9 | DateTime.Today.Month << 5 | DateTime.Today.Day);

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .SetTypicalOpen(GameRelease.SkyrimSE, "MusicTypes Merged.esp")
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(Apply)
                .Run(args);
        }

        private static IEnumerable<IModContext<TGet>> ExtentContexts<TGet>(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, FormKey formKey)
            where TGet : class, IMajorRecordGetter
        {
            var contexts = state.LinkCache.ResolveAllSimpleContexts<TGet>(formKey).ToList();
            var masterRefs = contexts.SelectMany(i => state.LoadOrder.TryGetValue(i.ModKey)?.Mod?.MasterReferences ?? new List<IMasterReferenceGetter>(), (i, k) => (i.ModKey, k.Master))
                .ToLookup(i => i.ModKey, i => i.Master);

            foreach (var ctx in contexts)
            {
                var modKey = ctx.ModKey;
                if (contexts.Any(i => masterRefs[i.ModKey].Contains(modKey)))
                    continue;

                yield return ctx;
            }
        }

        private static void Apply(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            using var loadOrder = state.LoadOrder;

            foreach (var musicType in loadOrder.PriorityOrder.OnlyEnabled().MusicType().WinningOverrides())
            {
                var origin = state.LinkCache.Resolve<IMusicTypeGetter>(musicType.FormKey);
                var extentContexts = ExtentContexts<IMusicTypeGetter>(state, musicType.FormKey).ToList();
                if (extentContexts.Count < 2)
                    continue;

                var copy = state.PatchMod.MusicTypes.GetOrAddAsOverride(origin);
                copy.FormVersion = 44;
                copy.VersionControl = Timestamp;
                copy.Tracks = new();

                var originalTracks = origin.Tracks.EmptyIfNull();
                int originalTrackCount = originalTracks.Count();

                var extentTracks = extentContexts.Select(static i => i.Record.Tracks.EmptyIfNull());

                extentTracks.Aggregate(originalTracks, (i, k) => i.Intersection(k)).ForEach(copy.Tracks.Add);
                copy.Tracks.AddRange(extentTracks.SelectMany(i => i.DisjunctLeft(copy.Tracks)));

                Console.WriteLine("Copied {0} tracks to {1}", copy.Tracks.Count - originalTrackCount, copy.EditorID);
            }
        }
    }
}
