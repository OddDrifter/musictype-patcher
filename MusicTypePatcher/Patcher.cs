using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MusicTypePatcher
{
    public class Patcher
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance.AddPatch<ISkyrimMod, ISkyrimModGetter>(Apply).Run(args, new RunPreferences()
            {
                ActionsForEmptyArgs = new RunDefaultPatcher()
                {
                    IdentifyingModKey = "MusicTypeSynthesisPatch.esp",
                    TargetRelease = GameRelease.SkyrimSE
                }
            });
        }

        private static void Apply(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            using var loadOrder = state.LoadOrder; 
            var query = loadOrder.PriorityOrder.Reverse().OnlyEnabled()
                .SelectMany(plugins => plugins.AsEnumerable().MusicType().WinningOverrides())
                .GroupBy(record => record.FormKey);

            foreach(var value in query)
            {
                bool shouldKeep = false;
                var trackList = new ExtendedList<IFormLink<IMusicTrackGetter>>();

                foreach (var musicType in value)
                {
                    shouldKeep = trackList.Except(musicType.Tracks).Any();
                    foreach (var tracks in musicType.Tracks.GroupBy(_ => _.FormKey))
                    {
                        trackList.AddRange(tracks.Skip(trackList.Count(_ => _.FormKey == tracks.Key)));
                    }
                }

                if (shouldKeep)
                {
                    var copy = new MusicType(value.Key, SkyrimRelease.SkyrimSE) { Tracks = trackList };
                    copy.DeepCopyIn(value.First(), new MusicType.TranslationMask(defaultOn: true) { Tracks = false });
                    state.PatchMod.MusicTypes.GetOrAddAsOverride(copy);
                    Console.WriteLine("Copied {0} tracks to {1}", copy.Tracks.Count() - value.First().Tracks.Count(), copy.EditorID);
                }
            }
        }
    }
}
