// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Stui.Animations
{
    using Importing;
    using Stui.EntityInfo;
    using Stui.Extensions;

    public class BoneBaker
    {
        public static void BakeAnimatedBoneScales(Animation animation, Timeline timeline, SpriterEntityInfo entityInfo)
        {
            var boneInfo = entityInfo.boneInfos.GetOrDefault(timeline.name);
            if (boneInfo == null || !boneInfo.needsSpatialAdapter)
            {
                return; // This bone doesn't have any animations with animated bone scales.
            }

            // See if any of the timeline key spans have an animated scale...
            var animatedBoneScaleSpans = new List<(float spanStart, float spanEnd)>();

            FindAnimatedBoneScaleSpans(animation, timeline, entityInfo, animatedBoneScaleSpans);

            if (animatedBoneScaleSpans.Count > 0)
            {
                BakeAnimatedBoneScaleSpans(animation, timeline, entityInfo, animatedBoneScaleSpans);
            }
        }

        private static void FindAnimatedBoneScaleSpans(Animation animation, Timeline timeline, SpriterEntityInfo entityInfo,
            List<(float spanStart, float spanEnd)> animatedBoneScaleSpans)
        {
            for (int tlkIdx = 0; tlkIdx < timeline.keys.Count; ++tlkIdx)
            {
                var thisTlk = timeline.keys[tlkIdx];
                var thisTlkInfo = thisTlk.info;

                var mainlineKey = animation.mainlineKeys.FirstOrDefault(k => k.time_s == thisTlk.time_s);
                if (mainlineKey == null)
                {
                    // Some of BrashMonkey's own Spriter projects (Goblin_enemy) have animations ('to_Ladder' and
                    // 'stand_up) that have timeline keys at the very end of the animation that have no
                    // corresponding mainline key.  We're trying to filter those cases out here.
                    if (thisTlk.time_s < animation.length)
                    {
                        Debug.LogWarning($"entity: '{entityInfo.EntityName}', animation: '{animation.name}', " +
                            $"timeline: '{timeline.name}', time: {thisTlk.time_s}, a timeline key was found but the " +
                            "corresponding mainline key is missing.");
                    }

                    continue;
                }

                var thisBoneRef = mainlineKey.boneRefs.FirstOrDefault(r => r.timelineId == timeline.id);
                if (thisBoneRef == null)
                {
                    continue; // The bone doesn't exist at this time.
                }

                if (thisTlk.time_s >= animation.length)
                {   // The span with this key has already been checked.
                    break;
                }

                float spanStartTime = thisTlk.time_s;
                float spanEndTime = (tlkIdx + 1 < timeline.keys.Count)
                    ? timeline.keys[tlkIdx + 1].time_s
                    : animation.length;

                // Guard against the bone dropping out of existance at some point during the span...
                int spanStartMlkIdx = animation.mainlineKeys.FindIndex(k => k.time_s == spanStartTime);
                int spanEndMlkIdx = animation.mainlineKeys.FindIndex(k => k.time_s == spanEndTime);

                for (int mli = spanStartMlkIdx; mli < spanEndMlkIdx; ++mli)
                {
                    if (animation.mainlineKeys[mli].boneRefs.FirstOrDefault(r => r.timelineId == timeline.id) == null)
                    {
                        spanEndTime = animation.mainlineKeys[mli].time_s;
                        break;
                    }
                }

                float nextRawScaleX = (tlkIdx + 1 < timeline.keys.Count)
                    ? timeline.keys[tlkIdx + 1].info.rawScaleX
                    : GetFinalFrameInferredSpatialInfo(animation, timeline).rawScaleX;

                float nextRawScaleY = (tlkIdx + 1 < timeline.keys.Count)
                    ? timeline.keys[tlkIdx + 1].info.rawScaleY
                    : GetFinalFrameInferredSpatialInfo(animation, timeline).rawScaleY;

                bool isScaleAnimated =
                    Mathf.Approximately(thisTlkInfo.rawScaleX, nextRawScaleX) == false ||
                    Mathf.Approximately(thisTlkInfo.rawScaleY, nextRawScaleY) == false;

                if (isScaleAnimated)
                {
                    if (animatedBoneScaleSpans.Count > 0 &&
                        animatedBoneScaleSpans[animatedBoneScaleSpans.Count - 1].spanEnd == spanStartTime)
                    {   // Extend the already existing span.
                        var spanEntry = animatedBoneScaleSpans[animatedBoneScaleSpans.Count - 1];
                        spanEntry.spanEnd = spanEndTime;

                        animatedBoneScaleSpans[animatedBoneScaleSpans.Count - 1] = spanEntry;
                    }
                    else
                    {
                        animatedBoneScaleSpans.Add((spanStartTime, spanEndTime));
                    }
                }
            }
        }

        private static SpatialInfo GetFinalFrameInferredSpatialInfo(Animation animation, Timeline timeline)
        {
            // This will return the appropriate value for the the last frame of an animation based on whether 1) there
            // is a time 0 auxiliary key, and if not 2) on whether the animation loops or not.

            var firstKey = timeline.keys[0];
            var lastKey = timeline.keys[timeline.keys.Count - 1];
            var timeZeroAuxKey = firstKey.timeZeroAuxKey;

            if (timeZeroAuxKey != null)
            {
                return timeZeroAuxKey.info;
            }
            else if (animation.looping && firstKey.time_s == 0f)
            {
                return firstKey.info;
            }
            else
            {
                return lastKey.info;
            }
        }

        private class AnimatedBoneScaleDescendantInfo
        {
            public float time_s; // The time where all these refs need a timeline key entry.
            public Ref rootBoneRef; // The root of descendantBoneRefs and descendantObjectRefs.
            public List<Ref> descendantBoneRefs;
            public List<Ref> descendantObjectRefs;
        }

        private static void BakeAnimatedBoneScaleSpans(Animation animation, Timeline timeline, SpriterEntityInfo entityInfo,
            List<(float spanStart, float spanEnd)> animatedBoneScaleSpans)
        {
            List<AnimatedBoneScaleDescendantInfo> animatedBoneScaleDescendantInfos = new List<AnimatedBoneScaleDescendantInfo>();

            foreach (var spanEntry in animatedBoneScaleSpans)
            {
                int spanStartMlkIdx = animation.mainlineKeys.FindIndex(k => k.time_s == spanEntry.spanStart);
                int spanEndMlkIdx = animation.mainlineKeys.FindIndex(k => k.time_s == spanEntry.spanEnd);

                if (spanEndMlkIdx < 0)
                {   // The span goes to the end of the animation but there isn't a mainline key at that time so use
                    // the last mainline key.
                    spanEndMlkIdx = animation.mainlineKeys.Count - 1;
                }

                for (int mlkIdx = spanStartMlkIdx; mlkIdx <= spanEndMlkIdx; ++mlkIdx)
                {
                    var mainlineKey = animation.mainlineKeys[mlkIdx];
                    var rootBoneRef = mainlineKey.boneRefs.FirstOrDefault(r => r.timelineId == timeline.id);

                    if (rootBoneRef == null)
                    {   // The bone doesn't exist at this point-in-time.  Note that thisBoneRef can be null, but the
                        // only valid case when it can is for the last mainline key of the span.
                        if (mlkIdx != spanEndMlkIdx - 1)
                        {
                            // This should have been taken care of when the span was built so this is a programming error.
                            Debug.LogWarning("An invalid animated bone scale time span was encountered.");
                        }

                        break;
                    }

                    var boneDescendantRefs = new List<Ref>();
                    var objectDescendantRefs = new List<Ref>();

                    // Get all of this bone's children and their children, etc. for this point in time.
                    GetAllDescendantRefs(mainlineKey, rootBoneRef, boneDescendantRefs, objectDescendantRefs, depth: 0);

                    // If the root bone or any of the descendants have a timeline key at mainlineKey.time_s then ALL
                    // bones and objects will get one as well (including the root bone.)

                    bool rootHasTimelineKey = TimelineKeyExistsAtTime(mainlineKey.time_s, animation,
                        rootBoneRef.timelineId, rootBoneRef.timelineKeyId);

                    bool boneHasTimelineKey = false;

                    if (!rootHasTimelineKey)
                    {
                        foreach (var boneDescendantRef in boneDescendantRefs)
                        {
                            boneHasTimelineKey = TimelineKeyExistsAtTime(mainlineKey.time_s, animation,
                                boneDescendantRef.timelineId, boneDescendantRef.timelineKeyId);

                            if (boneHasTimelineKey)
                            {
                                break;
                            }
                        }
                    }

                    bool objectHasTimelineKey = false;

                    if (!rootHasTimelineKey && !boneHasTimelineKey)
                    {
                        foreach (var objectDescendantRef in objectDescendantRefs)
                        {
                            objectHasTimelineKey = TimelineKeyExistsAtTime(mainlineKey.time_s, animation,
                                objectDescendantRef.timelineId, objectDescendantRef.timelineKeyId);

                            if (objectHasTimelineKey)
                            {
                                break;
                            }
                        }
                    }

                    if (rootHasTimelineKey || boneHasTimelineKey || objectHasTimelineKey)
                    {   // All bones (including the top-most) and objects will need to get a key at
                        // mainlineKey.time_s if they don't already have one.  New timeline keys and
                        // SpatialInfos/SpriteInfos will need to be created and the existing Refs in
                        // mainlineKey.boneRefs and mainlineKey.objectRefs will need to be updated.
                        animatedBoneScaleDescendantInfos.Add(new AnimatedBoneScaleDescendantInfo()
                        {
                            time_s = mainlineKey.time_s,
                            rootBoneRef = rootBoneRef,
                            descendantBoneRefs = boneDescendantRefs,
                            descendantObjectRefs =  objectDescendantRefs
                        });
                    }
                }
            }

            if (animatedBoneScaleDescendantInfos.Count > 0)
            {
                CreateAnimatedBoneTimelineEntries(animation, entityInfo, animatedBoneScaleDescendantInfos);
            }
        }

        private static bool TimelineKeyExistsAtTime(float time_s, Animation animation, int timelineId, int timelineKeyId)
        {
            var tl = animation.timelines.FirstOrDefault(t => t.id == timelineId);
            if (tl != null)
            {
                var tlk = tl.keys.FirstOrDefault(k => k.id == timelineKeyId);
                if (tlk != null && tlk.time_s == time_s)
                {
                    return true;
                }
            }

            return false;
        }

        private static void GetAllDescendantRefs(MainlineKey mlk, Ref thisBoneRef, List<Ref> boneDescendantRefs,
            List<Ref> objectDescendantRefs, int depth)
        {
            if (depth++ > 100)
            {
                return;
            }

            var childBoneInfos = (
                from bref in mlk.boneRefs
                where  bref.parentRefId == thisBoneRef.id
                select bref
            )
            .ToList();

            boneDescendantRefs.AddRange(childBoneInfos);

            var childObjectInfos = (
                from oref in mlk.objectRefs
                where  oref.parentRefId == thisBoneRef.id
                select oref
            )
            .ToList();

            objectDescendantRefs.AddRange(childObjectInfos);

            foreach (var childBoneRef in childBoneInfos)
            {
                GetAllDescendantRefs(mlk, childBoneRef, boneDescendantRefs, objectDescendantRefs, depth);
            }
        }

        private static void CreateAnimatedBoneTimelineEntries(Animation animation, SpriterEntityInfo entityInfo,
            List<AnimatedBoneScaleDescendantInfo> animatedBoneScaleDescendantInfos)
        {
            foreach (var info in animatedBoneScaleDescendantInfos)
            {
                CreateTimelineEntryIfNeeded(animation, entityInfo, info.time_s, info.rootBoneRef);

                foreach (var boneRef in info.descendantBoneRefs)
                {
                    CreateTimelineEntryIfNeeded(animation, entityInfo, info.time_s, boneRef);
                }

                foreach (var objectRef in info.descendantObjectRefs)
                {
                    CreateTimelineEntryIfNeeded(animation, entityInfo, info.time_s, objectRef);
                }
            }
        }

        private static void CreateTimelineEntryIfNeeded(Animation animation, SpriterEntityInfo entityInfo, float time_s, Ref theRef)
        {
            var timeline = animation.timelines.FirstOrDefault(t => t.id == theRef.timelineId);
            if (timeline == null)
            {
                Debug.LogWarning($"Entity: '{entityInfo.EntityName}', animation: {animation.name}, could not find " +
                    $"timeline with an id of {theRef.timelineId}.");
                return;
            }

            var fromTlk = timeline.keys.FirstOrDefault(k => k.id == theRef.timelineKeyId);
            if (fromTlk == null)
            {
                Debug.LogWarning($"Entity: '{entityInfo.EntityName}', animation: {animation.name}, " +
                    $"timeline: {timeline.name}, could not find timeline key with an id of {theRef.timelineKeyId}.");
                return;
            }

            if (fromTlk.time_s == time_s)
            {
                return; // Key already exists at time_s.
            }

            var toTlk = timeline.keys.FirstOrDefault(k => k.time_s > time_s);

            var toInfo = toTlk != null ? toTlk.info : GetFinalFrameInferredSpatialInfo(animation, timeline);
            float toTime_s = toTlk != null ? toTlk.time_s : animation.length;

            var newTimelineKey = fromTlk.Clone(); // This clones the info member as well, which we will keep if the curve_type is instant.
            newTimelineKey.id = 1 + timeline.keys.Max(k => k.id);
            newTimelineKey.time_s = time_s;

            if (newTimelineKey.curve_type != CurveType.instant)
            {   // All non-instant curve types are forced to be linear curve types.
                newTimelineKey.curve_type = CurveType.linear;

                float t = Mathf.InverseLerp(fromTlk.time_s, toTime_s, time_s);
                newTimelineKey.info = SpatialInfo.Lerp(fromTlk.info, toInfo, t);
            }
            else if (newTimelineKey.info.haveBaked)
            {   // The BoneBaker should run before anything has been baked so this is unexpected (but recoverable
                // assuming the undo doesn't fail.)
                if (!newTimelineKey.info.UndoBake())
                {
                    Debug.LogWarning("BoneBaker.CreateTimelineEntryIfNeeded(): UndoBake() failed.");
                }
                else
                {
                    Debug.LogWarning("BoneBaker.CreateTimelineEntryIfNeeded(): Had to undo a bake.  This is not " +
                        "expected and may indicate a programming error.");
                }
            }

            int insertIdx = timeline.keys.FindIndex(k => k.time_s > time_s);
            insertIdx = insertIdx >= 0 ? insertIdx : timeline.keys.Count;

            timeline.keys.Insert(insertIdx, newTimelineKey);
            theRef.timelineKeyId = newTimelineKey.id;
        }
    }
}