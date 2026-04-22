// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Spriter2UnityDX.EntityInfo
{
    using Importing;
    using UnityEditor;
    using Debug = UnityEngine.Debug;

    // This class is used for tracking information that spans across all of an entity's animations.
    // It also validates and preprocesses an entity's Spriter file information before the builders work with the entity.

    public class SpriterEntityInfo
    {
        // Information common to sprites, bones, and action points.  Events store their metadata here.
        public abstract class SpriterInfoBase
        {
            public string name;
            public ObjectType type;

            public bool hasVirtualParent;
            public string virtualParentTransformName; // Set even if there isn't one so ones from prior imports can be found.
            public Transform virtualParentTransform; // The transform where the VirtualParent component is.

            public List<string> parentBoneNames = new List<string>(); // Empty if there aren't any.

            // This is the object-scoped and event-scoped metadata.  The key for these is the id.
            public Dictionary<int, VarDef> variableDefs = new Dictionary<int, VarDef>(); // Empty if there aren't any variables for this object.
            public Dictionary<int, TagListItem> tagDefs = new Dictionary<int, TagListItem>(); // Empty if there aren't any tags for this object.

            public bool HasMetadata { get { return HasVariables || HasTags;  } }
            public bool HasVariables { get { return variableDefs.Count > 0;  } }
            public bool HasTags { get { return tagDefs.Count > 0; } }

            public SpriterInfoBase(string _name, ObjectType _type)
            {
                name = _name;
                type = _type;

                virtualParentTransformName = _name + " virtual parent";
            }
        }

        public class SpriterObjectInfo : SpriterInfoBase
        {
            public string spriteRenderTransformName;
            public bool hasPivotController;
            public string pivotControllerTransformName; // Set even if there isn't one so ones from prior imports can be found.

            public SpriterObjectInfo(string _name, ObjectType _type)
                : base(_name, _type)
            {
                spriteRenderTransformName = _name; // This will change if there is a pivot parent.
                pivotControllerTransformName = _name;
            }
        }

        public class SpriterBoneInfo : SpriterInfoBase
        {
            public SpriterBoneInfo(string _name, ObjectType _type)
                : base(_name, _type)
            {
            }
        }

        // Note!: Bones and objects can have the same name in older Spriter projects so don't try to mix these into one collection.
        public Dictionary<string, SpriterObjectInfo> objectInfo = new Dictionary<string, SpriterObjectInfo>();
        public Dictionary<string, SpriterBoneInfo> boneInfo = new Dictionary<string, SpriterBoneInfo>();

        public string EntityName { get { return _entityName;  } }

        public List<SpriterSoundItem> soundItems = new List<SpriterSoundItem>();

        // This is the entity-scoped metadata.  The key for these is the id.
        public Dictionary<int, VarDef> variableDefs = new Dictionary<int, VarDef>(); // Empty if there aren't any entity-scoped variables.
        public Dictionary<int, TagListItem> tagDefs = new Dictionary<int, TagListItem>(); // Empty if there aren't any entity-scoped tags.

        public bool HasMetadata { get { return HasVariables || HasTags;  } }
        public bool HasVariables { get { return variableDefs.Count > 0;  } }
        public bool HasTags { get { return tagDefs.Count > 0; } }

        private string _entityName;

        public SpriterEntityInfo()
        {
        }

        public IEnumerator Process(string spriterProjDirectory, ScmlObject scmlObject, Entity entity,
            Dictionary<int, IDictionary<int, File>> fileInfo, IBuildTaskContext buildCtx)
        {
            _entityName = entity.name;

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, checking for missing mainline keys";
            CheckForMissingMainlineKeys(entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, checking for missing mainline time=0 keys";
            CheckForMissingMainlineTime0Keys(entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, checking for mainline blending keys";
            CheckForMainlineBlendingKeys(entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, handling invalid bone data";
            HandleInvalidBoneData(entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, checking for animated bone scales";
            CheckForAnimatedBoneScales(entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, checking for animated bone alphas";
            CheckForAnimatedBoneAlphas(entity);

            LogProjectScopedTagInfo(scmlObject); // The log messages will only be seen when debug logging is enabled.

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing entity metadata";
            PreprocessEntityScopedMetadata(scmlObject, entity); // Populates entity's variableDefs and tagDefs collections.

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, assigning entity metadata references";
            AssignEntityScopedMetadataReferences(scmlObject, entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing bones";
            PreprocessBones(entity); // Populates boneInfo collection.

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing sprites";
            PreprocessSprites(entity); // Populates objectInfo collection.

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing action points";
            PreprocessActionPoints(entity); // Adds to objectInfo collection.

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing Spriter events";
            PreprocessEvents(entity); // Adds to objectInfo collection (weird case.)

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing object-scoped metadata";
            PreprocessObjectScopedMetadata(scmlObject, entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing event-scoped metadata";
            PreprocessEventScopedMetadata(scmlObject, entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, assigning object-scoped metadata references";
            AssignObjectScopedMetadataReferences(scmlObject, entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, assigning event-scoped metadata references";
            AssignEventScopedMetadataReferences(scmlObject, entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing Spriter sounds";
            PreprocessSounds(spriterProjDirectory, scmlObject, entity); // Validates and adds to soundItems collection.

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing unsupported types";
            PreprocessUnsupportTypes(entity); // Warns of unsupported types.  Puts them in objectInfo collection.

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing pivot points";
            PreprocessSpritePivots(entity, fileInfo);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing bones with multiple parents";
            PreprocessBonesWithMultipleParents(entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing objects with multiple parents";
            PreprocessObjectsWithMultipleParents(entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing z-indices";
            PreprocessZIndices(entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing pivots and parents";
            PreprocessPivotsAndParents(entity);
        }

        private void CheckForMissingMainlineKeys(Entity entity)
        {
            // Check for timelines that have keys without a corresponding mainline key...
            var mainlineTimeSets = entity.animations
                .ToDictionary(
                    anim => anim,
                    anim => new HashSet<float>(anim.mainlineKeys.Select(mk => mk.time))
                );

            var missingMainlineKeys = (
                from anim in entity.animations
                from tl in anim.timelines
                from tlKey in tl.keys
                let timeset = mainlineTimeSets[anim]
                let currentTime = tlKey.time_s
                let mlk = anim.mainlineKeys
                    .OrderBy(mk => mk.time_s)
                    .LastOrDefault(mk => mk.time_s == currentTime)
                let isBoneTl = tl.objectType == ObjectType.bone
                let refs = isBoneTl ? mlk?.boneRefs : mlk?.objectRefs
                let myRef = refs?.FirstOrDefault(r => r.timeline == tl.id)
                where myRef == null
                select new
                {
                    animation = anim,
                    timeline = tl,
                    missingTime = tlKey.time_s,
                }
            ).ToList();

            if (missingMainlineKeys.Count > 0)
            {
                Debug.LogWarning($"For entity '{entity.name}', one or more timeline keys are missing a corresponding " +
                    "mainline key.  These will be ignored.");
            }

            foreach (var mtk in missingMainlineKeys)
            {
                Debug.LogWarning($"    Animation: {mtk.animation.name}, timeline: {mtk.timeline.name}, time: {mtk.missingTime}");

                mtk.timeline.keys.RemoveAll(k => k.time_s == mtk.missingTime);
            }
        }

        private void CheckForMissingMainlineTime0Keys(Entity entity)
        {   // Give the user a warning if there are any animations that don't have a mainline key at time 0.

            var animationsMissingZeroKey = entity.animations
                .Where(anim =>
                    anim.mainlineKeys.Count > 0 &&
                    anim.mainlineKeys[0].time_s != 0f
                )
                .ToList();

            if (animationsMissingZeroKey.Count > 0)
            {
                Debug.LogWarning($"For entity '{entity.name}', one or more animations are missing mainline keys at " +
                    "time=0.  All keyframe times for the animation(s) will be adjusted to remove the blank frames.  " +
                    "Because of this the keyframe times will not match Spriter's and the animation(s) may not match " +
                    "Spriter's playback.  More details follow:");
            }

            foreach (var anim in animationsMissingZeroKey)
            {
                Debug.LogWarning($"    Animation: {anim.name}, first key's time: {anim.mainlineKeys[0].time_s}");

                float delta = 0f - anim.mainlineKeys[0].time_s;

                foreach (var mlk in anim.mainlineKeys)
                {
                    mlk.time_s += delta;
                }

                IEnumerable<TimelineKey> allTimelineKeys = anim.timelines.SelectMany(tl => tl.keys);

                foreach (var tlk in allTimelineKeys)
                {
                    tlk.time_s += delta;
                }
            }
        }

        private void CheckForMainlineBlendingKeys(Entity entity)
        {
            var mainlineBlendingKeys = (
                from anim in entity.animations
                from mlk in anim.mainlineKeys
                where mlk.curve_type != CurveType.linear
                select new
                {
                    anim,
                    mlk
                }
            ).ToList();

            if (mainlineBlendingKeys.Count > 0)
            {
                Log($"For entity '{entity.name}', one or more mainline keys have a non-linear curve type and therefore " +
                    "blend with the animation's timeline keys.");
            }

            foreach (var info in mainlineBlendingKeys)
            {
                Log($"    Animation: {info.anim.name}, mlk info: {info.mlk}");
            }
        }

        private void HandleInvalidBoneData(Entity entity)
        {
            // Spriter has a bug that creates a mainline.key.object_ref for a bone.  There will
            // already be a bone_ref in the key so we just need to remove the object_ref entry...
            var mislinkedObjectRefs =
                (from anim in entity.animations
                 from mlk in anim.mainlineKeys
                 from objectRef in mlk.objectRefs
                 from timeline in anim.timelines
                 where objectRef.timeline == timeline.id && timeline.objectType == ObjectType.bone
                 select new
                 {
                     AnimationName = anim.name,
                     ObjectRefId = objectRef.id,
                     TimelineId = timeline.id,
                     TimelineName = timeline.name,
                     ObjectRef = objectRef,
                     mlk
                 }).ToList();

            if (mislinkedObjectRefs.Count > 0)
            {
                Debug.LogWarning($"Entity '{entity.name}' has one or more bones that have 'object_ref' entries.  These entries " +
                    "will be ignored.  Information regarding this follows:");
            }

            foreach (var objectRefInfo in mislinkedObjectRefs)
            {
                Debug.LogWarning($"    Animation name: '{objectRefInfo.AnimationName}', object ref id: {objectRefInfo.ObjectRefId}, " +
                    $"timeline name: '{objectRefInfo.TimelineName}', timeline id: {objectRefInfo.TimelineId}");

                if (objectRefInfo.mlk.objectRefs.Remove(objectRefInfo.ObjectRef))
                {
                    Log("Entry removed successfully");
                }
                else
                {
                    Debug.LogWarning("Entry removal failed!");
                }
            }
        }

        private void CheckForAnimatedBoneScales(Entity entity)
        {
            var boneScaleInfo =
                (from anim in entity.animations
                 from mlk in anim.mainlineKeys
                 from boneRef in mlk.boneRefs
                 let boneTimeline = anim.timelines.FirstOrDefault(t => t.id == boneRef.timeline)
                 let boneName = boneTimeline?.name ?? "Unknown"
                 from tlk in boneTimeline.keys
                 let scaleX = System.Math.Round(tlk.info.scale_x, 4)
                 let scaleY = System.Math.Round(tlk.info.scale_y, 4)
                 select new
                 {
                     animName = anim.name,
                     animLength = anim.length,
                     boneName,
                     timeline = boneTimeline,
                     scaleX,
                     scaleY
                 })
                .Distinct()
                .GroupBy(x => new { x.animName, x.animLength, x.boneName, x.timeline })
                .Where(g => g.Select(x =>  new { x.scaleX, x.scaleY }).Distinct().Count() > 1)
                .Select(g => new
                {
                    g.Key.animName,
                    g.Key.animLength,
                    g.Key.boneName,
                    g.Key.timeline,
                    scales = g.Select(x => new { x.scaleX, x.scaleY }).Distinct().ToList()
                })
                .OrderBy(x => x.animName).ThenBy(x => x.boneName)
                .ToList();

            // Remove all items that aren't really animated bone scales but pivot/parent changes or instant changes.
            boneScaleInfo.RemoveAll(item =>
            {
                var tlks = item.timeline.keys.ToList(); // We need to work with our own copy of the list.

                if (tlks.Count >= 3 && tlks[0].time_s == tlks[1].time_s)
                {   // The first two keys have the same time so this is a pivot and/or parent change.  Put a copy of the
                    // key that is at index 0 at the end of the keys and remove it.
                    var first = tlks[0].Clone();
                    first.time_s = item.animLength + 1f;

                    tlks.RemoveAt(0);
                    tlks.Add(first);
                }

                // Check each of the spans between pivot/parent changes and instant changes and make sure the scales are
                // the same or nearly the same for all of the keys in the span.  A new span starts when either two keys
                // are found with the same time (a pivot and/or parent change) or the difference of their times
                // is < ~1ms (an instant change.)
                bool newSpan = true;

                for (int i = 1; i < tlks.Count; ++i)
                {
                    var prevKey = tlks[i - 1];
                    var thisKey = tlks[i];

                    double prevKeyTime = System.Math.Round(prevKey.time_s, 4);
                    double thisKeyTime = System.Math.Round(thisKey.time_s, 4);

                    newSpan = thisKeyTime - prevKeyTime < 0.0011;

                    if (!newSpan)
                    {
                        var xDelta = Mathf.Abs(prevKey.info.scale_x - thisKey.info.scale_x);
                        var yDelta = Mathf.Abs(prevKey.info.scale_y - thisKey.info.scale_y);

                        if (xDelta > 0.03f || yDelta > 0.03f)
                        {
                            return false; // This timeline has animated bone scales.
                        }
                    }
                }

                return true; // This timeline does NOT have animated bone scales.
            });

            if (boneScaleInfo.Count > 0)
            {
                Debug.LogWarning($"Entity '{entity.name}' has one or more bones with animated scales.  " +
                    "Animated bone scales are not supported by the importer.  The animation(s) may not match " +
                    "Spriter's playback.  Information regarding this follows:");
            }

            foreach (var boneInfo in boneScaleInfo)
            {
                Debug.LogWarning($"    Animation '{boneInfo.animName}', bone name '{boneInfo.boneName}' has one or " +
                    "more keys with the following (different) scales:");

                foreach (var scaleInfo in boneInfo.scales)
                {
                    Debug.LogWarning($"        scale x: {scaleInfo.scaleX}, scale y: {scaleInfo.scaleY}");
                }
            }
        }

        private void CheckForAnimatedBoneAlphas(Entity entity)
        {
            var boneAlphaInfo =
                (from anim in entity.animations
                 from mlk in anim.mainlineKeys
                 from boneRef in mlk.boneRefs
                 let boneTimeline = anim.timelines.FirstOrDefault(t => t.id == boneRef.timeline)
                 let boneName = boneTimeline?.name ?? "Unknown"
                 from tlk in boneTimeline.keys
                 let alpha = System.Math.Round(tlk.info.a, 4)
                 select new
                 {
                     animName = anim.name,
                     animLength = anim.length,
                     boneName,
                     timeline = boneTimeline,
                     alpha,
                 })
                .Distinct()
                .GroupBy(x => new { x.animName, x.animLength, x.boneName, x.timeline })
                .Where(g => g.Select(x => x.alpha).Distinct().Count() > 1)
                .Select(g => new
                {
                    g.Key.animName,
                    g.Key.animLength,
                    g.Key.boneName,
                    g.Key.timeline,
                    alphas = g.Select(x => x.alpha).Distinct().ToList()
                })
                .OrderBy(x => x.animName).ThenBy(x => x.boneName)
                .ToList();

            // Remove all items that aren't really animated bone alphas but pivot/parent changes.
            boneAlphaInfo.RemoveAll(item =>
            {
                var tlks = item.timeline.keys.ToList();

                if (tlks.Count >= 3 && tlks[0].time_s == tlks[1].time_s)
                {   // The first two keys have the same time so this is a pivot and/or parent change.  Put a copy of the
                    // key that is at index 0 at the end of the keys and remove it.
                    var first = tlks[0].Clone();
                    first.time_s = item.animLength + 1f;

                    tlks.RemoveAt(0);
                    tlks.Add(first);
                }

                // Check each of the spans between pivot/parent changes and make sure the alphas are the same for all
                // of the keys in the span.  A new span starts when two keys are found with the same time.  This will
                // be a pivot and/or parent change.
                bool newSpan = true;

                for (int i = 1; i < tlks.Count; ++i)
                {
                    var prevKey = tlks[i - 1];
                    var thisKey = tlks[i];

                    double prevKeyTime = System.Math.Round(prevKey.time_s, 4);
                    double thisKeyTime = System.Math.Round(thisKey.time_s, 4);

                    newSpan = prevKeyTime == thisKeyTime;

                    if (!newSpan && prevKey.info.a != thisKey.info.a)
                    {
                        return false; // This timeline has animated bone alphas.
                    }
                }

                return true; // This timeline does NOT have animated bone alphas.
            });

            if (boneAlphaInfo.Count > 0)
            {
                Debug.LogWarning($"Entity '{entity.name}' has one or more bones with animated alphas.  " +
                    "Animated bone alphas are not supported by the importer.  The animation(s) may not match " +
                    "Spriter's playback.  Information regarding this follows:");
            }

            foreach (var boneInfo in boneAlphaInfo)
            {
                Debug.LogWarning($"    Animation '{boneInfo.animName}', bone name '{boneInfo.boneName}' has one or " +
                    "more keys with the following (different) alphas:");

                foreach (var boneAlpha in boneInfo.alphas)
                {
                    Debug.LogWarning($"        alpha: {boneAlpha}");
                }
            }
        }

        private void LogProjectScopedTagInfo(ScmlObject scmlObject)
        {
            if (scmlObject.tags.Count > 0)
            {
                Log("This Spriter project has the following tag definitions:");

                foreach (var tagDef in scmlObject.tags)
                {
                    Log($"    tag id: {tagDef.id}, tag name: {tagDef.name}");
                }

                Log("Note that tags are _defined_ at the project scope but are still scoped similar to variables.");
                Log("Because of this, you can have tags with the same name but with different scopes (entity-, object-,");
                Log("or event-scoped.)  In this case they are completely different tags and just happen to share the same name.");
            }
            else
            {
                Log($"This Spriter project has no tag definitions.");
            }
        }

        private void PreprocessEntityScopedMetadata(ScmlObject scmlObject, Entity entity)
        {
            // Populate this.variableDefs and this.tagDefs collections.

            if (entity.variableDefs.Count > 0)
            {
                Log($"Entity '{entity.name}' has the following entity-scoped variable definitions:");

                foreach (var variableDef in entity.variableDefs)
                {
                    Log($"    variable name: {variableDef.name}, type: {variableDef.type}, default value: '{variableDef.defaultValue}'");

                    if (variableDef.type == VarType.String)
                    {   // Figure out what all of the possible string values this string variable can have.
                        var possibleStringValues =
                            entity.animations?
                                .SelectMany(a => a.metadata?.varlines ?? Enumerable.Empty<Varline>())
                                .Where(v => v.varDefId == variableDef.id)
                                .SelectMany(v => v.keys ?? Enumerable.Empty<VarlineKey>())
                                .Select(k => k.value)
                                .Where(v => v != null)
                                .Distinct()
                                .OrderBy(v => v)
                                .ToList()
                            ?? new List<string>();

                        // Make sure the default value is the first element of the list...

                        possibleStringValues.RemoveAll(s => s == variableDef.defaultValue);
                        possibleStringValues.Insert(0, variableDef.defaultValue);

                        variableDef.possibleStringValues = possibleStringValues;

                        Log($"        '{variableDef.name}' has the possible string values:");

                        foreach (var s in variableDef.possibleStringValues)
                        {
                            Log($"            '{s}'");
                        }
                    }

                    variableDefs.Add(variableDef.id, variableDef);
                }
            }
            else
            {
                Log($"Entity '{entity.name}' has no variable definitions.");
            }

            var entityScopedTagIds = (
                from anim in entity.animations
                where anim.metadata != null
                from taglineKeys in anim.metadata.taglineKeys
                from tags in taglineKeys.tags
                select tags.tagId
            )
            .Distinct()
            .OrderBy(id => id)
            .ToList();

            if (entityScopedTagIds.Count > 0)
            {
                Log($"Entity '{entity.name}' uses the following entity-scoped tags:");

                foreach (var tagId in entityScopedTagIds)
                {
                    var tagDef = scmlObject.tags.FirstOrDefault(t => t.id == tagId);

                    if (tagDef != null)
                    {
                        Log($"    tag id: {tagDef.id}, tag name: {tagDef.name}");
                        tagDefs.Add(tagDef.id, tagDef);
                    }
                    else
                    {
                        Debug.LogWarning($"An invalid id ({tagId}) was found while processing the entity-scoped tag " +
                            $"metadata for entity: '{entity.name}'.  A tag list item with that id was not found.");
                    }
                }
            }
            else
            {
                Log($"Entity '{entity.name}' uses no entity-scoped tags.");
            }
        }

        private void AssignEntityScopedMetadataReferences(ScmlObject scmlObject, Entity entity)
        {
            // Put the appropriate references in entity.animation[].metadata.varlines[].varDef and
            // entity.animation[].metadata.taglineKeys[].tags[].tagName.

            foreach (var anim in entity.animations)
            {
                if (anim.metadata == null)
                {
                    continue;
                }

                for (int i = 0; i < anim.metadata.varlines.Count; ++i)
                {
                    var varline = anim.metadata.varlines[i];

                    varline.varDef = variableDefs.GetValueOrDefault(varline.varDefId);

                    if (varline.varDef != null)
                    {
                        Log($"Entity-scoped varline varDef assigned for entity: {entity.name}, animation: {anim.name}, " +
                            $"metadata.varlines[{i}], (id: {varline.id}, varDefId: {varline.varDefId})");
                        Log($"    variable name: {varline.varDef.name}, type: {varline.varDef.type}, " +
                            $"default value: '{varline.varDef.defaultValue}'");
                    }
                    else
                    {
                        Debug.LogWarning($"While processing the variable metadata (at index {i}) for entity: '{entity.name}', " +
                            $"animation: '{anim.name}', a variable definition for id: {varline.varDefId} was missing " +
                            $"from the entity-scoped variable definitions.");
                    }
                }

                for (int keyIdx = 0; keyIdx < anim.metadata.taglineKeys.Count; ++keyIdx)
                {
                    var taglineKey = anim.metadata.taglineKeys[keyIdx];

                    for (int tagIdx = 0; tagIdx < taglineKey.tags.Count; ++tagIdx)
                    {
                        var tag = taglineKey.tags[tagIdx];

                        var tagListItem = tagDefs.GetValueOrDefault(tag.tagId);
                        tag.tagName = tagListItem?.name;

                        if (tag.tagName != null)
                        {
                            Log($"Entity-scoped tagline TagInfo name assigned for entity: {entity.name}, " +
                                $"animation: {anim.name}, metadata.taglines[{keyIdx}].tags[{tagIdx}], (id: {tag.id}, " +
                                $"tagId: {tag.tagId})");
                            Log($"    tag name: {tag.tagName}");
                        }
                        else
                        {
                            Debug.LogWarning($"While processing the tag metadata (at index [${keyIdx}][{tagIdx}]) " +
                                $"for entity: '{entity.name}', animation: '{anim.name}', a tag definition for " +
                                $"id: {tag.tagId} was missing from the tag definitions.");
                        }
                    }
                }
            }
        }

        private void PreprocessObjectScopedMetadata(ScmlObject scmlObject, Entity entity)
        {
            Log($"Entity '{entity.name}', preprocessing object-scoped metadata for all bones...");

            foreach (var boneInfo in boneInfo.Values.Where(o => o.type == ObjectType.bone))
            {
                DoPreprocessObjectScopedMetadata(scmlObject, entity, boneInfo);
            }

            Log($"Entity: '{entity.name}', preprocessing object-scoped metadata for all sprites...");

            foreach (var spriteInfo in objectInfo.Values.Where(o => o.type == ObjectType.sprite))
            {
                DoPreprocessObjectScopedMetadata(scmlObject, entity, spriteInfo);
            }

            Log($"Entity: '{entity.name}', preprocessing object-scoped metadata for all action points...");

            foreach (var actionPtInfo in objectInfo.Values.Where(o => o.type == ObjectType.point))
            {
                DoPreprocessObjectScopedMetadata(scmlObject, entity, actionPtInfo);
            }
        }

        private void DoPreprocessObjectScopedMetadata(ScmlObject scmlObject, Entity entity, SpriterInfoBase info)
        {
            // Populate info.variableDefs and info.tagDefs collections

            if (info.type == ObjectType.spriterEvent)
            {
                Debug.LogWarning("An object was passed to DoPreprocessObjectScopedMetadata() that has a type of 'event'.");
                return;
            }

            var allVarDefs = entity.objectInfos.FirstOrDefault(o => o.name == info.name && o.objectType == info.type)?.variableDefs;

            if (allVarDefs?.Count > 0)
            {
                Log($"    '{info.name}' has the following object-scoped variable definitions:");

                foreach (var variableDef in allVarDefs)
                {
                    Log($"        variable name: {variableDef.name}, type: {variableDef.type}, default value: '{variableDef.defaultValue}'");

                    if (variableDef.type == VarType.String)
                    {   // Figure out what all of the possible string values this string variable can have.

                        List<string> possibleStringValues = entity.animations?
                            .SelectMany(a => a.timelines ?? Enumerable.Empty<Timeline>())
                            .Where(t => t.name == info.name && t.objectType == info.type)
                            .SelectMany(t => t.metadata?.varlines ?? Enumerable.Empty<Varline>())
                            .Where(v => v.varDefId == variableDef.id)
                            .SelectMany(v => v.keys ?? Enumerable.Empty<VarlineKey>())
                            .Select(k => k.value)
                            .Where(v => v != null)
                            .Distinct()
                            .OrderBy(v => v)
                            .ToList()
                        ?? new List<string>();

                        // Make sure the default value is the first element of the list...

                        possibleStringValues.RemoveAll(s => s == variableDef.defaultValue);
                        possibleStringValues.Insert(0, variableDef.defaultValue);

                        variableDef.possibleStringValues = possibleStringValues;

                        Log($"        '{variableDef.name}' has the possible string values:");

                        foreach (var s in variableDef.possibleStringValues)
                        {
                            Log($"            '{s}'");
                        }
                    }

                    info.variableDefs.Add(variableDef.id, variableDef);
                }
            }

            var objectScopedTagIds = (
                from anim in entity.animations
                from timeline in anim.timelines
                where timeline.name == info.name && timeline.objectType == info.type && timeline.metadata != null
                from taglineKeys in timeline.metadata.taglineKeys
                from tags in taglineKeys.tags
                select tags.tagId
            )
            .Distinct()
            .OrderBy(id => id)
            .ToList();

            if (objectScopedTagIds.Count > 0)
            {
                Log($"    '{info.name}' uses the following object-scoped tags:");

                foreach (var tagId in objectScopedTagIds)
                {
                    var tagDef = scmlObject.tags.FirstOrDefault(t => t.id == tagId);

                    if (tagDef != null)
                    {
                        Log($"        tag id: {tagDef.id}, tag name: {tagDef.name}");
                        info.tagDefs.Add(tagDef.id, tagDef);
                    }
                    else
                    {
                        Debug.LogWarning($"An invalid id ({tagId}) was found while processing the object-scoped tag " +
                            $"metadata for entity: '{entity.name}', timeline: '{info.name}'.  A tag list item with " +
                            "that id was not found.");
                    }
                }
            }
        }

        private void PreprocessEventScopedMetadata(ScmlObject scmlObject, Entity entity)
        {
            Log($"Entity: '{entity.name}', preprocessing event-scoped metadata for all events...");

            foreach (var eventInfo in objectInfo.Values.Where(o => o.type == ObjectType.spriterEvent))
            {
                DoPreprocessEventScopedMetadata(scmlObject, entity, eventInfo);
            }
        }

        private void DoPreprocessEventScopedMetadata(ScmlObject scmlObject, Entity entity, SpriterInfoBase info)
        {
            // Populate info.variableDefs and info.tagDefs collections

            if (info.type != ObjectType.spriterEvent)
            {
                Debug.LogWarning("An object was passed to DoPreprocessEventScopedMetadata() that does not have a type of 'event'.");
                return;
            }

            var allVarDefs = entity.objectInfos.FirstOrDefault(o => o.name == info.name && o.objectType == info.type)?.variableDefs;

            if (allVarDefs?.Count > 0)
            {
                Log($"    '{info.name}' has the following event-scoped variable definitions:");

                foreach (var variableDef in allVarDefs)
                {
                    Log($"        variable name: {variableDef.name}, type: {variableDef.type}, default value: '{variableDef.defaultValue}'");

                    if (variableDef.type == VarType.String)
                    {   // Figure out what all of the possible string values this string variable can have.

                        List<string> possibleStringValues = entity.animations?
                            .SelectMany(a => a.eventlines)
                            .Where(e => e.name == info.name)
                            .SelectMany(e => e.metadata?.varlines ?? Enumerable.Empty<Varline>())
                            .Where(v => v.varDefId == variableDef.id)
                            .SelectMany(v => v.keys ?? Enumerable.Empty<VarlineKey>())
                            .Select(k => k.value)
                            .Where(v => v != null)
                            .Distinct()
                            .OrderBy(v => v)
                            .ToList()
                        ?? new List<string>();

                        // Make sure the default value is the first element of the list...

                        possibleStringValues.RemoveAll(s => s == variableDef.defaultValue);
                        possibleStringValues.Insert(0, variableDef.defaultValue);

                        variableDef.possibleStringValues = possibleStringValues;

                        Log($"        '{variableDef.name}' has the possible string values:");

                        foreach (var s in variableDef.possibleStringValues)
                        {
                            Log($"            '{s}'");
                        }
                    }

                    info.variableDefs.Add(variableDef.id, variableDef);
                }
            }

            var eventScopedTagIds = (
                from anim in entity.animations
                from eventline in anim.eventlines
                where eventline.name == info.name && eventline.metadata != null
                from taglineKeys in eventline.metadata.taglineKeys
                from tags in taglineKeys.tags
                select tags.tagId
            )
            .Distinct()
            .OrderBy(id => id)
            .ToList();

            if (eventScopedTagIds.Count > 0)
            {
                Log($"    '{info.name}' uses the following event-scoped tags:");

                foreach (var tagId in eventScopedTagIds)
                {
                    var tagDef = scmlObject.tags.FirstOrDefault(t => t.id == tagId);

                    if (tagDef != null)
                    {
                        Log($"        tag id: {tagDef.id}, tag name: {tagDef.name}");
                        info.tagDefs.Add(tagDef.id, tagDef);
                    }
                    else
                    {
                        Debug.LogWarning($"An invalid id ({tagId}) was found while processing the event-scoped tag " +
                            $"metadata for entity: '{entity.name}', event: '{info.name}'.  A tag list item with that " +
                            "id was not found.");
                    }
                }
            }
        }

        private void AssignObjectScopedMetadataReferences(ScmlObject scmlObject, Entity entity)
        {
            Log($"Entity '{entity.name}', assigning object-scoped metadata references for all bones...");

            foreach (var boneInfo in boneInfo.Values.Where(o => o.type == ObjectType.bone))
            {
                if (boneInfo.HasMetadata)
                {
                    DoAssignObjectScopedMetadataReferences(scmlObject, entity, boneInfo);
                }
            }

            Log($"Entity '{entity.name}', assigning object-scoped metadata references for all sprites...");

            foreach (var spriteInfo in objectInfo.Values.Where(o => o.type == ObjectType.sprite))
            {
                if (spriteInfo.HasMetadata)
                {
                    DoAssignObjectScopedMetadataReferences(scmlObject, entity, spriteInfo);
                }
            }

            Log($"Entity '{entity.name}', assigning object-scoped metadata references for all action points...");

            foreach (var actionPtInfo in objectInfo.Values.Where(o => o.type == ObjectType.point))
            {
                if (actionPtInfo.HasMetadata)
                {
                    DoAssignObjectScopedMetadataReferences(scmlObject, entity, actionPtInfo);
                }
            }
        }

        private void DoAssignObjectScopedMetadataReferences(ScmlObject scmlObject, Entity entity, SpriterInfoBase info)
        {
            // Call this only for non-event objInfos.  It will put the appropriate references in
            // timeline.metadata.varlines[].varDef and timeline.metadata.taglineKeys[].tags[].tagName.

            foreach (var anim in entity.animations)
            {
                foreach (var timeline in anim.timelines)
                {
                    if (timeline.metadata == null || timeline.name != info.name || timeline.objectType != info.type)
                    {
                        continue;
                    }

                    for (int i = 0; i < timeline.metadata.varlines.Count; ++i)
                    {
                        var varline = timeline.metadata.varlines[i];

                        varline.varDef = info.variableDefs.GetValueOrDefault(varline.varDefId);

                        if (varline.varDef != null)
                        {
                            Log($"    Object-scoped varline varDef assigned for entity: {entity.name}, animation: {anim.name}, " +
                                $"timeline: {timeline.name}, metadata.varlines[{i}], (id: {varline.id}, varDefId: {varline.varDefId})");
                            Log($"        variable name: {varline.varDef.name}, type: {varline.varDef.type}, " +
                                $"default value: '{varline.varDef.defaultValue}'");
                        }
                        else
                        {
                            Debug.LogWarning($"While processing the variable metadata (at index {i}) for entity: '{entity.name}', " +
                                $"animation: '{anim.name}', timeline: '{timeline.name}', a variable definition for id: {varline.varDefId} " +
                                "was missing from the object-scoped variable definitions.");
                        }
                    }

                    for (int keyIdx = 0; keyIdx < timeline.metadata.taglineKeys.Count; ++keyIdx)
                    {
                        var taglineKey = timeline.metadata.taglineKeys[keyIdx];

                        for (int tagIdx = 0; tagIdx < taglineKey.tags.Count; ++tagIdx)
                        {
                            var tag = taglineKey.tags[tagIdx];

                            var tagListItem = info.tagDefs.GetValueOrDefault(tag.tagId);
                            tag.tagName = tagListItem?.name;

                            if (tag.tagName != null)
                            {
                                Log($"    Object-scoped tagline TagInfo name assigned for entity: {entity.name}, " +
                                    $"animation: {anim.name}, timeline: {timeline.name}, " +
                                    $"metadata.taglines[{keyIdx}].tags[{tagIdx}], (id: {tag.id}, tagId: {tag.tagId})");
                                Log($"        tag name: {tag.tagName}");
                            }
                            else
                            {
                                Debug.LogWarning($"While processing the tag metadata (at index [${keyIdx}][{tagIdx}]) " +
                                    $"for entity: '{entity.name}', animation: '{anim.name}', timeline: '{timeline.name}', " +
                                    $" a tag definition for id: {tag.tagId} was missing from the tag definitions.");
                            }
                        }
                    }
                }
            }
        }

        private void AssignEventScopedMetadataReferences(ScmlObject scmlObject, Entity entity)
        {
            Log($"Entity '{entity.name}', assigning event-scoped metadata references for all events...");

            foreach (var eventInfo in objectInfo.Values.Where(o => o.type == ObjectType.spriterEvent))
            {
                if (eventInfo.HasMetadata)
                {
                    DoAssignEventScopedMetadataReferences(scmlObject, entity, eventInfo);
                }
            }
        }

        private void DoAssignEventScopedMetadataReferences(ScmlObject scmlObject, Entity entity, SpriterInfoBase info)
        {
            // Call this only for event objInfos.  It will put the appropriate references in
            // timeline.metadata.varlines[].varDef and timeline.metadata.taglineKeys[].tags[].tagName.

            foreach (var anim in entity.animations)
            {
                foreach (var eventline in anim.eventlines)
                {
                    if (eventline == null || eventline.name != info.name || eventline.metadata == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < eventline.metadata.varlines.Count; ++i)
                    {
                        var varline = eventline.metadata.varlines[i];

                        varline.varDef = info.variableDefs.GetValueOrDefault(varline.varDefId);

                        if (varline.varDef != null)
                        {
                            Log($"    Event-scoped varline varDef assigned for entity: {entity.name}, animation: {anim.name}, " +
                                $"eventline: {eventline.name}, metadata.varlines[{i}], (id: {varline.id}, varDefId: {varline.varDefId})");
                            Log($"        variable name: {varline.varDef.name}, type: {varline.varDef.type}, " +
                                $"default value: '{varline.varDef.defaultValue}'");
                        }
                        else
                        {
                            Debug.LogWarning($"While processing the variable metadata for entity: '{entity.name}', animation: '{anim.name}', " +
                                $"eventline: '{eventline.name}', a variable definition for id: {varline.varDefId} was missing from the event-scoped variable definitions.");
                        }
                    }

                    for (int keyIdx = 0; keyIdx < eventline.metadata.taglineKeys.Count; ++keyIdx)
                    {
                        var taglineKey = eventline.metadata.taglineKeys[keyIdx];

                        for (int tagIdx = 0; tagIdx < taglineKey.tags.Count; ++tagIdx)
                        {
                            var tag = taglineKey.tags[tagIdx];

                            var tagListItem = info.tagDefs.GetValueOrDefault(tag.tagId);
                            tag.tagName = tagListItem?.name;

                            if (tag.tagName != null)
                            {
                                Log($"    Event-scoped tagline TagInfo for entity: {entity.name}, animation: {anim.name}, " +
                                    $"eventlinel: {eventline.name}, metadata.taglines[{keyIdx}].tags[{tagIdx}], (id: {tag.id}, tagId: {tag.tagId})");
                                Log($"        tag name: {tag.tagName}");
                            }
                            else
                            {
                                Debug.LogWarning($"While processing the tag metadata for entity: '{entity.name}', animation: '{anim.name}', " +
                                    $"eventline: '{eventline.name}', a tag definition for id: {tag.tagId} was missing from the tag definitions.");
                            }
                        }
                    }
                }
            }
        }

        private void PreprocessBones(Entity entity)
        {
            // Populate the boneInfo collection with any bones...
            var allBoneNames =
                (from anim in entity.animations
                 from tl in anim.timelines
                 where tl.objectType == ObjectType.bone
                 select tl.name)
                .Distinct()
                .ToList();

            Log($"Entity '{entity.name}' has the following bones:");

            foreach (var boneName in allBoneNames)
            {
                Log($"    '{boneName}'");

                boneInfo.Add(boneName, new SpriterBoneInfo(boneName, ObjectType.bone));
            }
        }

        private void PreprocessSprites(Entity entity)
        {
            // Populate the objectInfo collection with any sprites...
            var allSpriteNames =
                (from anim in entity.animations
                 from tl in anim.timelines
                 where tl.objectType == ObjectType.sprite
                 select tl.name)
                .Distinct()
                .ToList();

            Log($"Entity '{entity.name}' has the following sprites:");

            foreach (var spriteName in allSpriteNames)
            {
                Log($"    '{spriteName}'");

                objectInfo.Add(spriteName, new SpriterObjectInfo(spriteName, ObjectType.sprite));
            }
        }

        private void PreprocessActionPoints(Entity entity)
        {
            // Populate the objectInfo collection with any action points...
            var allActionPointNames =
                (from anim in entity.animations
                 from tl in anim.timelines
                 where tl.objectType == ObjectType.point
                 select tl.name)
                .Distinct()
                .ToList();

            if (allActionPointNames.Count > 0)
            {
                Log($"Entity '{entity.name}' has the following action points:");

                foreach (var actionPointName in allActionPointNames)
                {
                    Log($"    '{actionPointName}'");

                    objectInfo.Add(actionPointName, new SpriterObjectInfo(actionPointName, ObjectType.point));
                }
            }
            else
            {
                Log($"Entity '{entity.name}' has no action points.");
            }
        }

        private void PreprocessEvents(Entity entity)
        {
            // All events will be found here...
            var allEventNames =
                (from anim in entity.animations
                 from evt in anim.eventlines
                 select evt.name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            if (allEventNames.Count > 0)
            {
                Log($"Entity '{entity.name}' has the following events:");

                foreach (var eventName in allEventNames)
                {
                    Log($"    '{eventName}'");

                    objectInfo.Add(eventName, new SpriterObjectInfo(eventName, ObjectType.spriterEvent));
                }
            }
            else
            {
                Log($"Entity '{entity.name}' has no events.");
            }
        }

        private void PreprocessSounds(string spriterProjDirectory, ScmlObject scmlObject, Entity entity)
        {
            // Validate and add to soundItems collection.
            var allSoundlineInfo =
                entity.animations.SelectMany(a => a.soundlines.Select(s => (animationName: a.name, soundline: s))).ToList();

            if (allSoundlineInfo.Count == 0)
            {
                Log($"Entity '{entity.name}' has no soundlines:");
                return;
            }

            Log($"Entity '{entity.name}' has the following soundlines:");

            foreach (var soundlineInfo in allSoundlineInfo)
            {
                Log($"    '{soundlineInfo.soundline.name}'");

                foreach (var key in soundlineInfo.soundline.keys)
                {
                    var soundObject = key.soundObject;

                    // Validate that there is a corresponding sound file in the Spriter project's folders list and that
                    // the file exists.

                    var fileItem =
                        scmlObject.folders
                            .FirstOrDefault(f => f.id == soundObject.folder)?
                            .files
                            .FirstOrDefault(file => file.id == soundObject.file);

                    if (fileItem == null)
                    {
                        Debug.LogWarning("A soundline references a sound file but that file doesn't exist in the " +
                            $"project's folder/file list.  FolderId: {soundObject.folder}, FileId: {soundObject.file}");
                    }
                    else if (fileItem.objectType != ObjectType.sound)
                    {
                        Debug.LogWarning("A soundline references a sound file but the referenced file doesn't have the " +
                            $"type of {ObjectType.sound}.  The referenced file has a type of {fileItem.objectType}.  " +
                            $"FolderId: {soundObject.folder}, FileId: {soundObject.file}");
                    }
                    else if (!System.IO.File.Exists($"{spriterProjDirectory}/{fileItem.name}"))
                    {
                        Debug.LogWarning($"A soundline references a sound file at '{spriterProjDirectory}/{fileItem.name}' " +
                            "but that file doesn't exist.");
                    }
                    else
                    {
                        SpriterSoundItem soundItem = new SpriterSoundItem
                        {
                            soundItemName = $"{soundlineInfo.soundline.name}, {soundlineInfo.animationName} animation @ {key.time_s} seconds",
                            animationName = soundlineInfo.animationName,
                            soundlineName = soundlineInfo.soundline.name,
                            time = key.time_s,
                            volume = soundObject.volume,
                            panning = soundObject.panning
                        };

                        string clipPath = $"{spriterProjDirectory}/{fileItem.name}";
                        soundItem.audioClip = (AudioClip)AssetDatabase.LoadAssetAtPath(clipPath, typeof(AudioClip));

                        if (soundItem.audioClip == null)
                        {
                            Debug.LogWarning($"A soundline references a sound file at '{clipPath}' but Unity cannot " +
                                "load the asset at that path as an AudioClip.");
                        }
                        else
                        {
                            Log($"       Animation name: {soundItem.animationName}, time: {soundItem.time}, " +
                                $"volume: {soundItem.volume}, panning: {soundItem.panning}");

                            soundItems.Add(soundItem);
                        }
                    }
                }
            }
        }

        private void PreprocessUnsupportTypes(Entity entity)
        {
            // Check for unsupported types and warn the user.  They will still go into objectInfo.
            var allUnsupportedTypeNames =
                (from anim in entity.animations
                 from tl in anim.timelines
                 where tl.objectType != ObjectType.sprite && tl.objectType != ObjectType.bone
                    && tl.objectType != ObjectType.point && tl.objectType != ObjectType.spriterEvent
                 select new { tl.name, tl.objectType })
                .Distinct()
                .ToList();

            if (allUnsupportedTypeNames.Count > 0)
            {
                Debug.LogWarning($"Entity '{entity.name}' has the following unsupported types:");
            }

            foreach (var unsupportedType in allUnsupportedTypeNames)
            {
                Debug.LogWarning($"    '{unsupportedType.name}' (type of {unsupportedType.objectType})");

                // This still gets an entry in objectInfo.
                objectInfo.Add(unsupportedType.name, new SpriterObjectInfo(unsupportedType.name, unsupportedType.objectType));
            }

            // Check for any names that are duplicated in both collections and warn the
            // user if any are found.  This might be a problem for very old Spriter files...
            var sharedNames = objectInfo.Keys
                .Intersect(boneInfo.Keys)
                .ToList();

            if (sharedNames.Count > 0)
            {
                Debug.LogWarning($"Entity '{entity.name}' has one or more bones that have the same name as an object.  " +
                    "This may cause problems and should be avoided.  The names follow:");
            }

            foreach (var name in sharedNames)
            {
                Debug.LogWarning($"    The name '{name}' is shared by both a bone and a {objectInfo[name].type}");
            }
        }

        private void PreprocessSpritePivots(Entity entity, Dictionary<int, IDictionary<int, File>> fileInfo)
        {
            // Setup all of the entity's sprite pivots...
            var spriteInfos =
                from anim in entity.animations
                from mlk in anim.mainlineKeys
                from oref in mlk.objectRefs
                let tl = anim.timelines.FirstOrDefault(t => t.id == oref.timeline)
                where tl != null && tl.objectType == ObjectType.sprite
                from key in tl.keys
                where key.info is SpriteInfo
                select (SpriteInfo)key.info;

            foreach (var si in spriteInfos)
            {
                var spriteFileInfo = fileInfo[si.folder][si.file];
                si.InitPivots(spriteFileInfo);
            }

            // Find all the sprite names for sprites that have non-default pivots...
            var nonDefaultPivotSpriteNames =
                (from anim in entity.animations
                 from mlk in anim.mainlineKeys
                 from oref in mlk.objectRefs
                 let tl = anim.timelines.FirstOrDefault(t => t.id == oref.timeline)
                 where tl != null && tl.objectType == ObjectType.sprite
                 from key in tl.keys
                 let si = key.info as SpriteInfo
                 where si != null && !si.IsDefaultPivots
                 select tl.name)
                .Distinct().ToList();

            if (nonDefaultPivotSpriteNames.Count > 0)
            {
                Log($"For entity '{entity.name}', the following sprites have non-default pivots and will need a pivot controller.");
            }

            foreach (var spriteName in nonDefaultPivotSpriteNames)
            {
                Log($"    '{spriteName}'");

                objectInfo[spriteName].hasPivotController = true;
                objectInfo[spriteName].pivotControllerTransformName = spriteName;
                objectInfo[spriteName].spriteRenderTransformName = spriteName + " renderer";
            }
        }

        private void PreprocessBonesWithMultipleParents(Entity entity)
        {
            // Find all of the bones that have more than one parent...
            var bonesWithMultipleParents =
                (from anim in entity.animations
                 from mlk in anim.mainlineKeys
                 from boneRef in mlk.boneRefs
                 let boneTimeline = anim.timelines.FirstOrDefault(t => t.id == boneRef.timeline)
                 let boneName = boneTimeline?.name ?? "Unknown"
                 let parentBoneName = boneRef.parent == -1
                             ? "rootTransform"
                             : anim.timelines.FirstOrDefault(t =>
                                 t.id == mlk.boneRefs.FirstOrDefault(b => b.id == boneRef.parent)?.timeline)?.name ?? "Unknown"
                 select new
                 {
                     boneName,
                     parentBoneName
                 })
                .Distinct()
                .GroupBy(x => x.boneName)
                .Where(g => g.Select(x => x.parentBoneName).Distinct().Count() > 1)
                .Select(g => new
                {
                    BoneName = g.Key,
                    ParentBoneNames = g.Select(x => x.parentBoneName).Distinct().ToList()
                })
                .OrderBy(x => x.BoneName)
                .ToList();

            if (bonesWithMultipleParents.Count > 0)
            {
                Log($"For entity '{entity.name}', the following bones have more than one parent and will need a virtual parent.");
            }

            foreach (var info in bonesWithMultipleParents)
            {
                Log($"    bone name: '{info.BoneName}', parent names are:");

                foreach (var parentName in info.ParentBoneNames)
                {
                    Log($"        '{parentName}'");
                }

                boneInfo[info.BoneName].hasVirtualParent = true;
                boneInfo[info.BoneName].parentBoneNames.AddRange(info.ParentBoneNames);
            }
        }

        private void PreprocessObjectsWithMultipleParents(Entity entity)
        {
            // Find all of the objects that have more than one parent...
            var objectsWithMultipleParents =
                (from anim in entity.animations
                 from mlk in anim.mainlineKeys
                 from objectRef in mlk.objectRefs
                 let objectTimeline = anim.timelines.FirstOrDefault(t => t.id == objectRef?.timeline)
                 let objectName = objectTimeline?.name ?? "Unknown"
                 let parentBoneName = objectRef.parent == -1
                     ? "rootTransform"
                     : anim.timelines.FirstOrDefault(t =>
                         t.id == mlk.boneRefs.FirstOrDefault(b => b.id == objectRef.parent)?.timeline)?.name ?? "Unknown"
                 select new
                 {
                     objectName,
                     parentBoneName
                 })
                .Distinct()
                .GroupBy(x => x.objectName)
                .Where(g => g.Select(x => x.parentBoneName).Distinct().Count() > 1)
                .Select(g => new
                {
                    ObjectName = g.Key,
                    ParentBoneNames = g.Select(x => x.parentBoneName).Distinct().ToList()
                })
                .OrderBy(x => x.ObjectName)
                .ToList();

            if (objectsWithMultipleParents.Count > 0)
            {
                Log($"For entity '{entity.name}', the following objects have more than one parent and will need a virtual parent.");
            }

            foreach (var info in objectsWithMultipleParents)
            {
                Log($"    object name: '{info.ObjectName}', parent names are:");

                foreach (var parentName in info.ParentBoneNames)
                {
                    Log($"        '{parentName}'");
                }

                objectInfo[info.ObjectName].hasVirtualParent = true;
                objectInfo[info.ObjectName].parentBoneNames.AddRange(info.ParentBoneNames);
            }
        }

        private void PreprocessZIndices(Entity entity)
        {
            // Populate all of the timeline key.infos with the z_index.  We'll do this
            // both for bones and sprites but it is only needed for sprites.

            // Flatten every key with its z_index
            var flatKeyParents = (
                from anim in entity.animations
                from tl in anim.timelines
                from tlKey in tl.keys

                let currentTime = tlKey.time_s
                let mlk = anim.mainlineKeys
                    .OrderBy(mk => mk.time_s)
                    .LastOrDefault(mk => mk.time_s <= currentTime)

                let isBoneTl = tl.objectType == ObjectType.bone
                let refs = isBoneTl ? mlk?.boneRefs : mlk?.objectRefs
                let myRef = refs?.FirstOrDefault(r => r.timeline == tl.id)

                let zIndex = myRef != null ? myRef.z_index : 0

                select new
                {
                    animation = anim,
                    timeline = tl,
                    keyEntry = tlKey,
                    zIndex
                }
            ).ToList();

            // Group them by animation and timeline.
            var groupedKeyZIndex = flatKeyParents
                .GroupBy(entry => new { entry.animation, entry.timeline })
                .Select(g => new
                {
                    g.Key.animation,
                    g.Key.timeline,
                    keys = g.Select(x => new { x.keyEntry, x.zIndex }).ToList()
                })
                .ToList();

            foreach (var timelineGroup in groupedKeyZIndex)
            {
                var anim = timelineGroup.animation;
                var timeline = timelineGroup.timeline;
                var keyInfos = timelineGroup.keys;  // List of { keyEntry, parentName }

                foreach (var keyInfo in keyInfos)
                {
                    Log($"Z-indices for entity: {entity.name}, animation: {anim.name}, timeline: {timeline.name}, " +
                        $"time: {keyInfo.keyEntry.time_s}, z_index: {keyInfo.zIndex}");

                    keyInfo.keyEntry.info.z_index = keyInfo.zIndex;
                }
            }
        }

        // PreprocessPivotsAndParents() helper methods...

        bool IsParentChange(string curr, string prev, string last, bool isFirst)
            => isFirst ? !string.Equals(last, curr)
                    : !string.Equals(prev, curr);

        // ! IsPivotChange() likely isn't quite right since it doesn't factor-in sprite swapping and sprites can have
        // ! different pivots.  The sprite swap would have to happen on the same frame as a pivot change, though, and
        // ! this would likely just cause an unnecessary pivot change to be keyed.
        bool IsPivotChange(SpriteInfo a, SpriteInfo b)
            => a.pivot_x != b.pivot_x || a.pivot_y != b.pivot_y;

        bool HandleKeyTimeAdjustment(Animation animation, TimelineKey tlk)
        {
            var currTime_s = tlk.time_s;

            if (currTime_s == 0f)
            {   // The parent/pivot information will be in the next key but _it_ needs to be keyed at time=0 and this
                // key will need to be made into a time zero aux key and removed from the timeline.
                return true;
            }

            // Adjust the time of tlk and, if not already done, create a new mainline key with the new time.

            const float keyTimeAdjust_s = 0.0005f;

            var prevTime_s = tlk.time_s;
            tlk.time_s += tlk.time_s > 0f ? -keyTimeAdjust_s : keyTimeAdjust_s;
            var newTime_s = tlk.time_s;

            tlk.curve_type = CurveType.instant; // Override curve type to instant for this short span.

            var mainlineKey = animation.mainlineKeys.FirstOrDefault(k => k.time_s == newTime_s);

            if (mainlineKey == null)
            {
                mainlineKey = animation.mainlineKeys.FirstOrDefault(k => k.time_s == prevTime_s);

                if (mainlineKey != null)
                {
                    var newMainlineKey = mainlineKey.Clone();
                    newMainlineKey.time_s = newTime_s;
                    newMainlineKey.id = 1 + animation.mainlineKeys.Max(k => k.id);

                    var insertIdx = 1 + animation.mainlineKeys.FindLastIndex(k => k.time_s < newTime_s);
                    animation.mainlineKeys.Insert(insertIdx, newMainlineKey);
                }
            }

            return false; // Don't create a time zero aux key.
        }

        private void PreprocessPivotsAndParents(Entity entity)
        {
            // Here we will deal with reparenting and pivot changes.
            //
            // The parent information is in the mainline keys but it will be more convenient
            // for it to be in each of the timeline spatial info objects since that is where
            // all of the keyed information is.  We have to be careful about how we determine
            // the parents, though.  When there is a parent (or pivot) change an extra
            // timeline key will be created and it will have the same time as the previous
            // timeline key but will hold the information regarding the new parent and/or pivot.

            // First get the parent names of all of the timeline keys...

            // For faster lookups, cache each animation’s timelines by id
            var timelinesById = entity.animations
                .ToDictionary(
                    anim => anim,
                    anim => anim.timelines.ToDictionary(t => t.id)
                );

            var flatKeyParents = (
                from anim in entity.animations
                from tl in anim.timelines
                from tlKey in tl.keys

                let currentTime = tlKey.time_s
                let mlk = anim.mainlineKeys
                            .OrderBy(mk => mk.time_s)
                            .LastOrDefault(mk => mk.time_s <= currentTime)

                let isBoneTl = tl.objectType == ObjectType.bone
                let refs = isBoneTl ? mlk?.boneRefs : mlk?.objectRefs
                let myRef = refs?.FirstOrDefault(r => r.timeline == tl.id)

                let parentRef =
                    (myRef == null || myRef.parent == -1 || mlk == null)
                        ? null
                        : mlk.boneRefs.FirstOrDefault(r => r.id == myRef.parent)

                let parentName =
                    parentRef == null
                        ? "rootTransform"
                        : timelinesById[anim][parentRef.timeline].name

                select new
                {
                    animation = anim,
                    timeline = tl,
                    keyEntry = tlKey,
                    parentName
                }
            ).ToList();

            // Group them by animation and timeline.
            var groupedKeyParents = flatKeyParents
                .GroupBy(entry => new { entry.animation, entry.timeline })
                .Select(g => new
                {
                    g.Key.animation,
                    g.Key.timeline,
                    keys = g.Select(x => new { x.keyEntry, x.parentName }).ToList()
                })
                .ToList();

            foreach (var tlGroup in groupedKeyParents)
            {
                var animation = tlGroup.animation;
                var timeline = tlGroup.timeline;
                var tlKeyInfos = tlGroup.keys; // List of { keyEntry, parentName }
                int keyCount = tlKeyInfos.Count;
                var lastKey = tlKeyInfos[keyCount - 1].keyEntry;

                // Assign parent names and adjust frame times for the parent/pivot change keys.

                // First, put all of the parent bone names in the timeline keys.infos.  These may change below.
                foreach (var keyInfo in tlKeyInfos)
                {
                    keyInfo.keyEntry.info.parentBoneName = keyInfo.parentName;
                }

                bool createAuxKey = false;

                for (int keyIdx = 0; keyIdx < keyCount - 1; ++keyIdx)
                {
                    var currKey = tlKeyInfos[keyIdx].keyEntry;
                    var nextKey = tlKeyInfos[keyIdx + 1].keyEntry;
                    float currTime_s = currKey.time_s;

                    if (currTime_s != nextKey.time_s)
                    {
                        continue;
                    }

                    // This is the first key entry for a parent/pivot change.  We will need to
                    // adjust some times and parent names...

                    var prevKey = keyIdx > 0 ? tlKeyInfos[keyIdx - 1].keyEntry : null;
                    var currInfo = currKey.info;
                    var prevParentName = prevKey != null ? prevKey.info.parentBoneName : lastKey.info.parentBoneName;
                    var lastParentName = lastKey.info.parentBoneName;

                    // Detect changes
                    bool isParentChange = IsParentChange(currInfo.parentBoneName, prevParentName, lastParentName, isFirst: keyIdx == 0);
                    bool isPivotChange = false;

                    if (currInfo is SpriteInfo currSI && nextKey.info is SpriteInfo nextSI)
                    {
                        isPivotChange = IsPivotChange(currSI, nextSI);
                    }

                    // Apply parent change
                    if (isParentChange)
                    {
                        currInfo.parentBoneName = prevParentName;

                        createAuxKey |= HandleKeyTimeAdjustment(animation, currKey);
                    }

                    // Apply pivot change
                    if (isPivotChange && !isParentChange)
                    {
                        createAuxKey |= HandleKeyTimeAdjustment(animation, currKey);
                    }

                    if (!isParentChange && !isPivotChange)
                    {   // This shouldn't happen but two keys with the same time will cause problems so treat it like
                        // a pivot/parent change and adjust the timeline key time and create a corresponding mainline
                        // key if necessary.

                        Debug.LogWarning($"For entity: {entity.name}, animation: {animation.name}, timeline: {timeline.name}, " +
                            $" time: {currTime_s}: Two timeline keys have the same time but neither a parent change or " +
                            "pivot change was made.  Both keys were preserved and the timing was adjusted.");

                        createAuxKey |= HandleKeyTimeAdjustment(animation, currKey);
                    }
                }

                if (createAuxKey)
                {   // We assign the first timeline key info into timeZeroAuxKey so that when creating the animaton
                    // curve's final key, it will have this information.
                    timeline.keys[1].timeZeroAuxKey = timeline.keys[0];
                    timeline.keys.RemoveAt(0);
                }
            }
        }

        private const string LOG_SYMBOL = "ENABLE_STUI_DEBUG_LOGS";

        [Conditional(LOG_SYMBOL)]
        private static void Log(object message, Object context = null)
        {
            Debug.Log(message, context);
        }

        [Conditional(LOG_SYMBOL)]
        private static void LogFormat(string format, params object[] args)
        {
            Debug.LogFormat(format, args);
        }

        [Conditional(LOG_SYMBOL)]
        private static void LogFormat(Object context, string format, params object[] args)
        {
            Debug.LogFormat(context, format, args);
        }
    }
}