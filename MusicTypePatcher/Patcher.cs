using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using System;
using System.Collections.Generic;
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
                .Select(record => record.AsLink().ResolveAll(state.LinkCache).Reverse())
                .Where(records => !records.Window(2).All(window => IsSupersetOf(window.Last(), window.First())))
                .ToDictionary(records => state.PatchMod.MusicTypes.GetOrAddAsOverride(records.First()), records => records.Skip(1));
                
            int originalCount = 0;
            var temp = new List<FormLinkInformation>();

            foreach (var (copy, overrides) in query)
            {
                copy.FormVersion = 44;
                copy.VersionControl = 0u;
                copy.Tracks ??= new ExtendedList<IFormLinkGetter<IMusicTrackGetter>>();

                originalCount = copy.Tracks.Count;

                foreach (var musicType in overrides)
                {
                    temp.AddRange(copy.ContainedFormLinks);
                    var keys = musicType.ContainedFormLinks.Where(link => !link.FormKey.IsNull && !temp.Remove(link)).Select(link => link.FormKey);
                    copy.Tracks.AddRange(keys);
                    temp.Clear();
                }

                if (copy.Tracks.Count - originalCount <= 0)
                {
                    state.PatchMod.Remove(copy);
                    continue;
                }
                
                Console.WriteLine("Copied {0} tracks to {1}", copy.Tracks.Count - originalCount, copy.EditorID);
            }
        }
    }
}
