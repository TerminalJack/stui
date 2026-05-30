// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System.Collections;
using UnityEngine;
using System.Linq;

namespace Stui
{
    using Importing;
    using EntityInfo;

    [CreateAssetMenu(fileName = "NewParentNameAnimationTask", menuName = "Inspection Tasks/Animation Parent Names Task", order = 6)]
    public class ParentNameAnimationTask : ScmlInspectorAnimationTask
    {
        public override IEnumerator ProcessAnimation(ScmlObject scmlObject, SpriterEntityInfo entityInfo, Entity entity,
            Animation animation, IBuildTaskContext inspectionCtx)
        {
            yield return $"Entity: '{entity.name}', animation: '{animation.name}', per-spatial info parent names:";

            var parentNameInfos =
            (
                from mlk in animation.mainlineKeys
                from timeline in animation.timelines

                let isBone = timeline.objectType == ObjectType.bone
                let refs = isBone ? mlk.boneRefs : mlk.objectRefs

                // Find the timeline key (if any) that each mainline key references
                let tlk = (
                        from k in timeline.keys
                        where refs.Any(r => r.timelineId == timeline.id && r.timelineKeyId == k.id)
                        select k
                    ).FirstOrDefault()

                // Find the matching bone/object ref (if any) so we can get the parent
                let mlkRef = refs.FirstOrDefault(r => r.timelineId == timeline.id && r.timelineKeyId == tlk?.id)

                let parentRef = mlk.boneRefs.FirstOrDefault(r => r.id == mlkRef?.parentRefId) // May be null.

                // If the following 'where' clause is commented-out then one record will be returned for each mainline
                // key and inactive bones/objects will have a null mlkRef and keyEntry.

                where tlk != null && mlkRef != null

                select new
                {
                    animation,
                    timeline,
                    isBone,
                    mlk.time_s,

                    mlkRef, // May be null.  If null then it _may_ indicate that the bone/object doesn't exist at this
                            // time.  It could also mean that there is a timeline key but no bone/object ref due to the
                            // timeline key being part of a parent/pivot change.

                    keyEntry = tlk, // May be null.

                    parentName = tlk == null ? SpatialInfo.UnassignedParentBoneName : (parentRef != null
                        ? animation.timelines.FirstOrDefault(tl => tl.id == parentRef.timelineId)?.name
                        : "rootTransform")
                }
            )
            .ToList();

            // Group them by animation, timeline, and bone/object.
            var groupedParentInfos = parentNameInfos
                .GroupBy(entry => new { entry.animation, entry.timeline, entry.isBone })
                .Select(g => new
                {
                    g.Key.animation,
                    g.Key.timeline,
                    g.Key.isBone,
                    keys = g.Select(x => new { x.time_s, x.isBone, x.mlkRef, x.keyEntry, x.parentName }).ToList()
                })
                .OrderBy(g => g.animation.name)
                .ThenBy(g => !g.isBone) // Sort bones before objects.
                .ThenBy(g => g.timeline.name)
                .ToList();

            foreach (var groupedInfo in groupedParentInfos)
            {
                yield return $"   animation: {groupedInfo.animation.name}, timeline: {groupedInfo.timeline.name}, isBone: {groupedInfo.isBone}";

                foreach (var key in groupedInfo.keys)
                {
                    if (key.mlkRef != null)
                    {   // This gets most of the timeline key spatial/sprite infos but wont get the ones where the
                        // timeline doesn't have a corresponding boneref or objectref.  (Parent/pivot changes.)
                        key.keyEntry.info.parentBoneName = key.parentName;
                    }

                    yield return $"       time: {key.time_s:F3}, hasRef?: {key.mlkRef != null}, parent: {key.parentName}";
                }
            }

            yield return "=========================";
            yield return $"timeline.key.info values for animation: {animation.name}";
            yield return "=========================";

            foreach (var timeline in animation.timelines)
            {
                yield return $"   timeline: {timeline.name}, isBone: {timeline.objectType == ObjectType.bone}";

                foreach (var tlk in timeline.keys)
                {
                    // Note: Some timeline key infos will still have an unassigned parentName at this point.  (See above.)
                    yield return $"        time: {tlk.time_s:F3}, parentName: {tlk.info.parentBoneName}";
                }
            }
        }
    }
}