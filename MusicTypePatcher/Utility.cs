using CSharpFunctionalExtensions;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MusicTypePatcher
{
    public static class Utility
    {
        public static MusicType.TranslationMask Mask { get; } = new MusicType.TranslationMask(defaultOn: true)
        {
            FormVersion = false, Version2 = false, VersionControl = false
        };

        public static IEnumerable<T> IntersectWith<T>(this IEnumerable<T> source, IEnumerable<T> other, IEqualityComparer<T>? comparer = null)
        {
            var _comparer = comparer ?? EqualityComparer<T>.Default;

            var set = new HashSet<T>(source, _comparer);
            var lookup = other.ToLookup(u => u, _comparer);

            if (set.Count is 0 || lookup.Count is 0)
            {
                yield break;
            }

            foreach (var it in set)
            {
                var lookupCount = lookup[it].Count();
                var sourceCount = source.Count(u => _comparer.Equals(u, it));

                foreach (var item in Enumerable.Repeat(it, Math.Min(lookupCount, sourceCount)))
                {
                    yield return item;
                }
            }
        }

        public static bool UnsortedEqual<T>(this IEnumerable<T> first, IEnumerable<T> second) where T : class
        {
            _ = first ?? throw new ArgumentNullException(nameof(first));
            _ = second ?? throw new ArgumentNullException(nameof(second));

            var dictionary = first.GroupBy(value => value).ToDictionary(value => value.Key, value => value.Count());

            foreach (var item in second)
            {
                if (dictionary.TryGetValue(item, out var count) is false || count is 0)
                    return false;
                dictionary[item]--;
            }

            return dictionary.All(kvp => kvp.Value is 0);
        }

        public static IEnumerable<TSource> Without<TSource>(this IEnumerable<TSource> first, IEnumerable<TSource> second, IEqualityComparer<TSource>? comparer = null) where TSource : notnull
        {
            _ = first ?? throw new ArgumentNullException(nameof(first));
            _ = second ?? throw new ArgumentNullException(nameof(second));

            Dictionary<TSource, int> _dictionary = new(comparer);

            foreach (var element in second)
            {
                if (_dictionary.ContainsKey(element))
                {
                    _dictionary[element]++;
                    continue;
                }
                _dictionary.Add(element, 1);
            }

            foreach (var element in first)
            {
                if (_dictionary.TryGetValue(element, out var _count))
                {
                    switch (_count)
                    {
                        case <= 0:
                            yield return element;
                            break;
                        default:
                            _dictionary[element]--;
                            break;
                    }
                    continue;
                }
                yield return element;
            }
        }

        public static bool HasMultipleOverrides<TMajor, TMajorGetter>(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, in FormKey formKey)
            where TMajor : class, IMajorRecordCommon, TMajorGetter where TMajorGetter : class, IMajorRecordCommonGetter
        {
            return GetExtentOverrides(state.LinkCache.ResolveAllContexts<TMajor, TMajorGetter>(formKey), state).Count() > 1;
        }

        public static IEnumerable<IModContext<TMod, TModGetter, TMajor, TMajorGetter>> GetExtentOverrides<TMod, TModGetter, TMajor, TMajorGetter>(this IEnumerable<IModContext<TMod, TModGetter, TMajor, TMajorGetter>> source, IPatcherState<TMod, TModGetter> state) 
            where TMod : class, IMod, TModGetter where TModGetter : class, IModGetter
            where TMajor : class, IMajorRecordCommon, TMajorGetter where TMajorGetter : class, IMajorRecordCommonGetter
        {
            var contexts = source.ToArray();
            var contextCount = contexts.Length;

            if (contextCount is <= 2)
            {
                return source.TakeLast(1);
            }

            int[][] m = new int[contextCount][];
            for (int i = 0; i < contextCount; i++)
                m[i] = new int[contextCount];

            for (int i = 0; i < contextCount; i++)
            {
                var modKey = contexts[i].ModKey;
                var masters = state.LoadOrder.TryGetValue(modKey)?.Mod?.MasterReferences.Select(static u => u.Master).ToHashSet() ?? new();
                
                for (int k = 0; k < contextCount; k++)
                {
                    m[k][i] = masters.Contains(contexts[k].ModKey) ? 1 : 0;
                }
            }

            return contexts.Where((i, k) => m[k].Sum() is 0);
        }

        public static IEnumerable<TMajorGetter> ResolveEnumerable<TMajorGetter>(this IEnumerable<IFormLinkGetter<TMajorGetter>>? source, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
            where TMajorGetter : class, IMajorRecordCommonGetter
        {
            if (source is null)
            {
                yield break;
            }

            foreach (var link in source)
            {
                if (link.TryResolve(linkCache, out var majorGetter))
                {
                    yield return majorGetter;
                }
            }
        }

        public static List<(Maybe<T> Left, Maybe<T> Right)> AlignGlobal<T>(IReadOnlyList<T> left, IReadOnlyList<T> right, int matchScore = 1, int gapScore = -3) where T : notnull
        {
            int length_x = left.Count;
            int length_y = right.Count;

            int[,] similarity = new int[length_x, length_y];
            for (int row = 0; row < length_x; row++)
            {
                for (int column = 0; column < length_y; column++)
                {
                    similarity[row, column] = left[row].Equals(right[column]) ? matchScore : -matchScore;
                }
            }

            int[,] forward = new int[length_x + 1, length_y + 1];
            for (int u = 0; u < length_y + 1; u++)
                forward[0, u] = u * gapScore;

            for (int row = 1; row < length_x + 1; row++)
            {
                forward[row, 0] = forward[row - 1, 0] + gapScore;
                for (int column = 1; column < length_y + 1; column++)
                {
                    int match = forward[row - 1, column - 1] + similarity[row - 1, column - 1];
                    int delete = forward[row - 1, column] + gapScore;
                    int insert = forward[row, column - 1] + gapScore;

                    forward[row, column] = Math.Max(match > delete ? match : delete, insert);
                }
            }

            List<(Maybe<T>, Maybe<T>)> alignments = new();

            int i = length_x;
            int j = length_y;

            while (i > 0 || j > 0)
            {
                if (i > 0 && j > 0 && forward[i, j] == forward[i - 1, j - 1] + similarity[i - 1, j - 1])
                {
                    alignments.Add((left[--i], right[--j]));
                    continue;
                }

                if (i > 0 && forward[i, j] == forward[i - 1, j] + gapScore)
                {
                    alignments.Add((left[--i], Maybe<T>.None));
                    continue;
                }
                
                alignments.Add((Maybe<T>.None, right[--j]));
            }

            alignments.Reverse();
            return alignments;
        }

        public static List<(Maybe<T> Left, Maybe<T> Right)> Align<T>(IReadOnlyList<T> left, IReadOnlyList<T> right, int matchScore = 1, int gapScore = -2) where T : notnull
        {
            int rows = left.Count;
            int columns = right.Count;

            int[,] substitutions = new int[rows, columns];
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                    substitutions[row, column] = left[row].Equals(right[column]) ? matchScore : -matchScore;

            int maxX = 0;
            int maxY = 0;

            int[,] scores = new int[rows + 1, columns + 1];
            for (int row = 1; row < rows + 1; row++)
            {
                for (int column = 1; column < columns + 1; column++)
                {
                    int match = scores[row - 1, column - 1] + substitutions[row - 1, column - 1];
                    int delete = scores[row - 1, column] + gapScore;
                    int insert = scores[row, column - 1] + gapScore;

                    int score = Math.Max(Math.Max(match, delete), Math.Max(0, insert));

                    if (score > scores[maxX, maxY])
                    {
                        maxX = row;
                        maxY = column;
                    }
                    scores[row, column] = score;
                }
            }

            List<(Maybe<T>, Maybe<T>)> alignments = new();

            int i = maxX;
            int k = maxY;

            while (scores[i, k] != 0)
            {
                if (i > 0 && k > 0 && scores[i, k] == scores[i - 1, k - 1] + substitutions[i - 1, k - 1])
                {
                    alignments.Add((left[--i], right[--k]));
                }
                else if (i > 0 && scores[i, k] == scores[i - 1, k] + gapScore)
                {
                    alignments.Add((left[--i], Maybe<T>.None));
                }
                else
                {
                    alignments.Add((Maybe<T>.None, right[--k]));
                }
            }

            while (i > 0 || k > 0)
            {
                if (i > 0 && k > 0)
                {
                    alignments.Add((left[--i], right[--k]));
                    continue;
                }

                if (i > 0)
                {
                    alignments.Add((left[--i], Maybe<T>.None));
                    continue;
                }

                alignments.Add((Maybe<T>.None, right[--k]));
            }
            
            alignments.Reverse();

            i = maxX;
            k = maxY;

            while (i < rows || k < columns)
            {
                if (i < rows && k < columns)
                {
                    alignments.Add((left[i++], right[k++]));
                    continue;
                }

                if (i < rows)
                {
                    alignments.Add((left[i++], Maybe<T>.None));
                    continue;
                    
                }

                if (k < columns)
                {
                    alignments.Add((Maybe<T>.None, right[k++]));
                }
            }

            return alignments;
        }

        public static void Deconstruct<T>(this Maybe<T> maybe, out bool hasValue, out T? value)
        {
            hasValue = maybe.HasValue;
            value = maybe.GetValueOrDefault();
        }

        public static void Deconstruct<TMod, TModGetter, TMajor, TMajorGetter>(this IModContext<TMod, TModGetter, TMajor, TMajorGetter> modContext, out ModKey modKey, out TMajorGetter record)
            where TMod : class, IMod, TModGetter where TModGetter : class, IModGetter
            where TMajor : class, IMajorRecordCommon, TMajorGetter where TMajorGetter : class, IMajorRecordCommonGetter
        {
            modKey = modContext.ModKey;
            record = modContext.Record;
        }

        public record MusicTypeResolver(IPatcherState<ISkyrimMod, ISkyrimModGetter> State, FormKey FormKey) 
        { 
            public MusicType Resolve()
            {
                var contexts = State.LinkCache.ResolveAllContexts<IMusicType, IMusicTypeGetter>(FormKey)
                    .Reverse().ToArray();
                var extents = contexts.GetExtentOverrides(State).ToArray();
                
                var source = contexts[0].Record;
                var target = new MusicType(FormKey, SkyrimRelease.SkyrimSE)
                {
                    EditorID = source.EditorID,
                    FadeDuration = source.FadeDuration ?? 0F,
                    Flags = source.Flags,
                    Data = new()
                    {
                        DuckingDecibel = source.Data?.DuckingDecibel ?? 0F,
                        Priority = source.Data?.Priority ?? 0
                    }
                };

                foreach (var (_, extent) in extents)
                {
                    if (extent.Flags != source.Flags)
                        target.Flags = extent.Flags;

                    if (extent.FadeDuration != source.FadeDuration)
                        target.FadeDuration = extent.FadeDuration;

                    if (extent.Data?.Priority != source.Data?.Priority)
                        target.Data.Priority = extent.Data?.Priority ?? 50;

                    if (extent.Data?.DuckingDecibel != source.Data?.DuckingDecibel)
                        target.Data.DuckingDecibel = extent.Data?.DuckingDecibel ?? 0;
                }
                
                Console.WriteLine($"[{target.FormKey}] {target.EditorID} -> {{ {string.Join(", ", extents.Select(u => u.ModKey))} }}");            

                var contextTracks = Array.ConvertAll(contexts, static u => u.Record.Tracks ?? Array.Empty<IFormLinkGetter<IMusicTrackGetter>>());
                var trackList = contextTracks[1..].Aggregate(contextTracks[0], (current, next) => 
                {
                    var index = 0;
                    var alignas = Align(current, next);

                    var tracks = new List<IFormLinkGetter<IMusicTrackGetter>>();
                    var temp = new List<IFormLinkGetter<IMusicTrackGetter>>();

                    foreach (var (l, r) in alignas)
                    {
                        switch (l, r)
                        {
                            case ((_, IFormLinkGetter<IMusicTrackGetter> left), (false, _)):
                                //Console.Write($"{"Delete",-12}: ");
                                tracks.Add(left);
                                break;
                            case ((false, _), (_, IFormLinkGetter<IMusicTrackGetter> right)):
                                //Console.Write($"{"Add",-12}: ");
                                if (temp.Count > 0)
                                    tracks.AddRange(temp);
                                temp.Clear();
                                tracks.Add(right);
                                break;
                            case ((_, IFormLinkGetter<IMusicTrackGetter> left), (_, IFormLinkGetter<IMusicTrackGetter> right)):
                                tracks.Add(left);
                                if (left.Equals(right))
                                {
                                    //Console.Write($"{"Match",-12}: ");
                                    break;
                                }
                                //Console.Write($"{"Mismatch",-12}: ");
                                temp.Add(right);
                                break;
                        }
                        //Console.WriteLine($"[<{index,3}> {l.Map(u => u.Resolve(State.LinkCache).EditorID),-20} => {r.Map(u => u.Resolve(State.LinkCache).EditorID),-20}]");
                        index++;
                    }

                    //Console.WriteLine();
                    tracks.AddRange(temp);
                    return tracks;
                });

                //Console.WriteLine();
                
                var extentTracks = Array.ConvertAll(extents, static u => u.Record.Tracks?.ToArray() ?? Array.Empty<IFormLinkGetter<IMusicTrackGetter>>());
                var commonTracks = extentTracks.Aggregate(static (current, next) => current.IntersectWith(next).ToArray())
                    .ToImmutableArray();
                var actualTracks = commonTracks.AddRange(extentTracks.SelectMany(u => u.Without(commonTracks)));

                if (actualTracks.Length < trackList.Count)
                {
                    target.Tracks = new(AlignGlobal(trackList, actualTracks).Select(static u => u.Right).Choose());
                }
                else
                {
                    target.Tracks = new(trackList);
                }

                return target;
            }
        }
    }
}
