using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
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
            var enabledPlugins = state.LoadOrder.PriorityOrder.OnlyEnabled();
            var query = enabledPlugins.SelectMany(_ => _.Mod?.MusicTypes)
                .GroupBy(_ => _.FormKey, (k, v) => v)
                .Where(_ => _.Count() > 2);

            foreach(var value in query)
            {
                var copy = state.PatchMod.MusicTypes.GetOrAddAsOverride(value.First());

                if (copy.Tracks == null)
                    copy.Tracks = new ExtendedList<IFormLink<IMusicTrackGetter>>();

                foreach (var trackList in value.Select(_ => _.Tracks))
                {         
                    copy.Tracks.AddRange(
                        trackList.Where(track =>
                            copy.Tracks.Count(_ => _.FormKey.Equals(track.FormKey)) < trackList.Count(_ => _.FormKey.Equals(track.FormKey))));
                }
            }
        }
    }
}
