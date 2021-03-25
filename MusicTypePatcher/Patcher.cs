using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using System;
using System.Linq;
using System.Threading.Tasks;
using static MoreLinq.Extensions.WindowExtension;

namespace MusicTypePatcher
{
    public class Patcher
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .SetTypicalOpen(GameRelease.SkyrimSE, "MusicTypes Merged.esp")
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(Apply)
                .Run(args);
        }

        private static bool IsSupersetOf(IMusicTypeGetter lhs, IMusicTypeGetter rhs)
        {
            if (lhs == null || rhs == null) 
                throw new ArgumentNullException();

            if (lhs.FormKey != rhs.FormKey)
                return false;

            var lhset = lhs.ContainedFormLinks.ToHashSet();
            var rhset = rhs.ContainedFormLinks.ToHashSet();
            return lhset.IsSupersetOf(rhset);
        }

        private static void Apply(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            using var loadOrder = state.LoadOrder;
            var query = loadOrder.PriorityOrder.OnlyEnabled().WinningOverrides<IMusicTypeGetter>()
                .Select(record => record.AsLink().ResolveAll(state.LinkCache).Reverse().ToArray())
                .ToDictionary(records => records[0], records => records[1..]);

            foreach (var (master, overrides) in query)
            {
                if (overrides.Window(2).All(window => IsSupersetOf(window[^1], window[0])))
                    continue;

                var copy = state.PatchMod.MusicTypes.GetOrAddAsOverride(master);
                copy.FormVersion = 44;
                copy.VersionControl = 0u;
                copy.Tracks ??= new ExtendedList<IFormLinkGetter<IMusicTrackGetter>>();

                foreach (var musicType in overrides)
                {
                    var links = copy.ContainedFormLinks.ToList();
                    var keys = musicType.ContainedFormLinks.Where(link => !links.Remove(link)).Select(link => link.FormKey);
                    copy.Tracks.AddRange(keys);
                }

                Console.WriteLine("Copied {0} tracks to {1}", copy.Tracks.Count - (master.Tracks?.Count ?? 0), copy.EditorID);
            }
        }
    }
}
