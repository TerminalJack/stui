// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Stui.EntityInfo
{
    using Importing;
    using Stui.Extensions;
    using UnityEditor;
    using Debug = UnityEngine.Debug;

    public class VarInstanceInfo
    {
        public VarDef varDef;
        public string bindPoseValue; // Value of this variable on the first frame of the first animation.
        public GameObject gameObject; // Where the SpriterFloat, SpriterInt, or SpriterString component can be found.
    }

    public class TagInstanceInfo
    {
        public TagDef tagDef;
        public bool bindPoseValue; // Value of this tag on the first frame of the first animation.
        public GameObject gameObject; // Where the SpriterTag component can be found.
    }

    // Information common to sprites, bones, and action points.  Events store their metadata here.
    public abstract class SpriterInfoBase
    {
        public string name;
        public ObjectType type;

        public bool hasVirtualParent { get { return parentBoneNames.Count > 1; }}
        public string virtualParentTransformName; // Set even if there isn't one so ones from prior imports can be found.
        public Transform virtualParentTransform; // The transform where the VirtualParent component is.
        public bool hasAlphaController;

        // Because support for animated bone scales is optional, the 'need' here can be overridden by the end-user.  If
        // you want to know if the bone/object has either a SpatialAdapter or ScaleTracker then check the appropriate
        // property below (spatialAdapter or scaleTracker) for a null/non-null value.  These will be set by the
        // PrefabBuilder.
        public bool needsSpatialAdapter; // This is true for all bones with animated bone scales as well as their child bones and sprites.
        public bool needsScaleTracker; // Bones (where hasAnimatedBoneScales == false) that are parents of animated bones need to track their raw scale via a ScaleTracker.

        public SpatialAdapter spatialAdapter; // This will be set by the prefab builder when/if it is created.
        public ScaleTracker scaleTracker; // This will be set by the prefab builder when/if it is created.

        public List<string> parentBoneNames = new List<string>();

        // This is the object-scoped and event-scoped metadata.  The key for these is the id.
        public Dictionary<int, VarInstanceInfo> varInstanceInfos = new Dictionary<int, VarInstanceInfo>(); // Empty if there aren't any variables for this object.
        public Dictionary<int, TagInstanceInfo> tagInstanceInfos = new Dictionary<int, TagInstanceInfo>(); // Empty if there aren't any tags for this object.

        public bool hasMetadata { get { return hasVariables || hasTags;  } }
        public bool hasVariables { get { return varInstanceInfos.Count > 0;  } }
        public bool hasTags { get { return tagInstanceInfos.Count > 0; } }

        public SpriterInfoBase(string _name, ObjectType _type)
        {
            name = _name;
            type = _type;

            virtualParentTransformName = _name + " virtual parent";
        }
    }

    public class SpriterObjectInfo : SpriterInfoBase
    {
        public string spriteRendererTransformName;
        public bool hasPivotController;
        public string pivotControllerTransformName; // Set even if there isn't one so ones from prior imports can be found.

        public SpriterObjectInfo(string _name, ObjectType _type)
            : base(_name, _type)
        {
            spriteRendererTransformName = _name; // This will change if there is a pivot parent.
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

    // This class is used for tracking information that spans across all of an entity's animations.
    // It also validates and preprocesses an entity's Spriter file information before the builders work with the entity.
    // Additionally, it will optionally log a ton of potentially useful information should you need to debug a .scml
    // file's contents.

    public class SpriterEntityInfo
    {
        // Note!: Bones and objects can have the same name in older Spriter projects so don't try to mix these into one collection.
        public Dictionary<string, SpriterObjectInfo> objectInfos = new Dictionary<string, SpriterObjectInfo>();
        public Dictionary<string, SpriterBoneInfo> boneInfos = new Dictionary<string, SpriterBoneInfo>();

        public string EntityName { get { return _entityName;  } }

        public List<SpriterSoundItem> soundItems = new List<SpriterSoundItem>();

        // This is the entity-scoped metadata.  The key for these is either VarDef.id or TagDef.id.
        public Dictionary<int, VarInstanceInfo> varInstanceInfos = new Dictionary<int, VarInstanceInfo>(); // Empty if there aren't any variables for this object.
        public Dictionary<int, TagInstanceInfo> tagInstanceInfos = new Dictionary<int, TagInstanceInfo>(); // Empty if there aren't any tags for this object.

        public bool hasMetadata { get { return hasVariables || hasTags;  } }
        public bool hasVariables { get { return varInstanceInfos.Count > 0;  } }
        public bool hasTags { get { return tagInstanceInfos.Count > 0; } }

        public bool loggingEnabled = false;

        private string _entityName;

        public SpriterEntityInfo()
        {
        }

        public static bool IsBakedBoneOrObject(SpriterInfoBase info, Animation animation)
        {
            // This bone/object uses baked position and scale if:
            //   * Bone Scale Animation is disabled by the user.
            //   * This animation is using baked data.
            //   * This bone/object doesn't require unbaked data.

            bool normalBoneScaleAnimationEnabled = ScmlImportOptions.options?.IsNormalBoneScales ?? true;

            bool isBaked =
                normalBoneScaleAnimationEnabled ||
                animation.usesBakedSpatialData ||
                !info.needsSpatialAdapter;

            return isBaked;
        }

        public static bool UseTransformForPositionAndScale(SpriterInfoBase info)
        {
            // Which component (Transform or SpatialAdapter) is used for position and scale is determined by:
            //   * Transform if Bone Scale Animation is disabled by the user.
            //   * Transform if this bone/object doesn't require unbaked data.
            //   * Otherwise, a SpatialAdapter is used.

            bool normalBoneScaleAnimationEnabled = ScmlImportOptions.options?.IsNormalBoneScales ?? true;

            bool useTransform =
                normalBoneScaleAnimationEnabled ||
                !info.needsSpatialAdapter;

            return useTransform;
        }

        public static bool BoneUsesScaleTracker(SpriterInfoBase info)
        {
            // This bone uses a scale tracker if:
            //   * Bone Scale Animation is enabled by the user.
            //   * and, the bone was marked as needing a scale tracker.

            bool advancedBoneScaleAnimationEnabled = ScmlImportOptions.options?.IsAdvancedBoneScales ?? false;

            bool useScaleTracker =
                advancedBoneScaleAnimationEnabled &&
                info.needsScaleTracker;

            return useScaleTracker;
        }

        public IEnumerator Process(string spriterProjDirectory, ScmlObject scmlObject, Entity entity,
            Dictionary<int, IDictionary<int, File>> fileInfo, IBuildTaskContext buildCtx)
        {
            _entityName = entity.name;

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
            yield return $"{buildCtx.MessagePrefix}, logging (if enabled) project-scoped tag information";
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
            yield return $"{buildCtx.MessagePrefix}, preprocessing collision rectangles";
            PreprocessCollisionRectangles(entity);

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
            yield return $"{buildCtx.MessagePrefix}, preprocessing bone parents";
            PreprocessBoneParents(entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing object parents";
            PreprocessObjectParents(entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, preprocessing pivots and parents";
            PreprocessPivotsAndParents(entity);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, checking for animated bone scales";
            CheckForAnimatedBoneScales(entity); // Run this only after running PreprocessPivotsAndParents().

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, checking for bones that use alpha";
            CheckForBoneAlphaUse(entity);
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
            else
            {
                Log($"Entity '{entity.name}' has no animations that are missing mainline keys at time=0.");
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

                foreach (var info in mainlineBlendingKeys)
                {
                    Log($"    Animation: {info.anim.name}, mlk info: {info.mlk}");
                }
            }
            else
            {
                Log($"Entity '{entity.name}' has no animations with mainline keys that blend with the timeline keys.");
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
                 where objectRef.timelineId == timeline.id && timeline.objectType == ObjectType.bone
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
            else
            {
                Log($"Entity '{entity.name}' has no invalid bone data.");
            }
        }

        public static bool ScaleChangedEnough(float a, float b, float percentThreshold)
        {
            // Avoid division by zero if both are zero
            if (a == 0f && b == 0f)
                return false;

            float avg = (Mathf.Abs(a) + Mathf.Abs(b)) * 0.5f;
            float diff = Mathf.Abs(a - b) / avg;

            return diff > percentThreshold;
        }

        private void CheckForAnimatedBoneScales(Entity entity)
        {
            const float minPercentThreshold = 0.02f; // +/- 2%.

            var boneScaleInfos =
                (from anim in entity.animations
                 from mlk in anim.mainlineKeys
                 from boneRef in mlk.boneRefs
                 let boneTimeline = anim.timelines.FirstOrDefault(t => t.id == boneRef.timelineId)
                 let boneName = boneTimeline?.name ?? "Unknown"
                 from tlk in boneTimeline.keys
                 let scaleX = System.Math.Round(tlk.info.scale_x, 4)
                 let scaleY = System.Math.Round(tlk.info.scale_y, 4)
                 select new
                 {
                     animation = anim,
                     boneName,
                     timeline = boneTimeline,
                     scaleX,
                     scaleY
                 })
                .Distinct()
                .GroupBy(x => new { x.animation, x.boneName, x.timeline })
                .Where(g => g.Select(x =>  new { x.scaleX, x.scaleY }).Distinct().Count() > 1)
                .Select(g => new
                {
                    g.Key.animation,
                    g.Key.boneName,
                    g.Key.timeline,
                    scales = g.Select(x => new { x.scaleX, x.scaleY }).Distinct().ToList()
                })
                .OrderBy(x => x.animation.name).ThenBy(x => x.boneName)
                .ToList();

            // Remove all items that aren't really animated bone scales but one of the following:
            //
            //     * Pivot changes.
            //     * Parent changes.
            //     * Instant changes.
            //     * Changes in scale that are too subtle to (hopefully) notice
            //
            // Note that pivot and parent changes are covered by the curve type being instant.

            boneScaleInfos.RemoveAll(item =>
            {
                var tlks = item.timeline.keys.ToList(); // We need to work with our own copy of the list.

                if (tlks[0].timeZeroAuxKey != null)
                {
                    var auxKey = tlks[0].timeZeroAuxKey.Clone();
                    auxKey.time_s = item.animation.length + 1f;

                    tlks.Add(auxKey);
                }

                for (int i = 1; i < tlks.Count; ++i)
                {
                    var prevKey = tlks[i - 1];
                    var thisKey = tlks[i];

                    if (prevKey.curve_type != CurveType.instant)
                    {
                        bool scaleChangeNoticable =
                            ScaleChangedEnough(prevKey.info.scale_x, thisKey.info.scale_x, minPercentThreshold) ||
                            ScaleChangedEnough(prevKey.info.scale_y, thisKey.info.scale_y, minPercentThreshold);

                        if (scaleChangeNoticable)
                        {
                            return false; // This timeline has animated bone scales.
                        }
                    }
                }

                return true; // This timeline does NOT have animated bone scales.
            });

            if (boneScaleInfos.Count > 0)
            {
                // Note: Use regular console logging for this message so that the user will know which animations
                // are affected.
                var animatonNamesStr = string.Join(", ", boneScaleInfos.Select(bi => $"'{bi.animation.name}'").Distinct());
                string prefix = loggingEnabled ? "    " : "";

                Debug.Log($"{prefix}Entity '{entity.name}' has one or more animations ({animatonNamesStr}) that have bones " +
                    "with animated scales.");

                bool hasNonlinearCurveTypes = false;
                bool hasScaleFlips = false;

                foreach (var boneScaleInfo in boneScaleInfos)
                {
                    boneScaleInfo.animation.hasAnimatedBoneScales = true;
                    boneInfos[boneScaleInfo.boneName].needsSpatialAdapter = true;

                    Log($"    Animation '{boneScaleInfo.animation.name}', bone name '{boneScaleInfo.boneName}' has one or " +
                        "more keys with animated scales:");

                    // For each of the scale changes, list the time, curve type, scales, and make note when the
                    // scale flips.

                    var tlks = boneScaleInfo.timeline.keys.ToList(); // We need to work with our own copy of the list.

                    if (tlks[0].timeZeroAuxKey != null)
                    {
                        var auxKey = tlks[0].timeZeroAuxKey.Clone();
                        auxKey.time_s = boneScaleInfo.animation.length + 1f;

                        tlks.Add(auxKey);
                    }

                    for (int i = 1; i < tlks.Count; ++i)
                    {
                        var prevKey = tlks[i - 1];
                        var thisKey = tlks[i];

                        bool scaleChangeNoticable =
                            ScaleChangedEnough(prevKey.info.scale_x, thisKey.info.scale_x, minPercentThreshold) ||
                            ScaleChangedEnough(prevKey.info.scale_y, thisKey.info.scale_y, minPercentThreshold);

                        var curveType = prevKey.curve_type;

                        if (curveType != CurveType.instant && scaleChangeNoticable)
                        {
                            string notes = "";

                            if (curveType != CurveType.linear)
                            {
                                hasNonlinearCurveTypes = true;
                                notes = "Non-linear or non-instant curve type!";
                            }

                            string scalesStr = $"({prevKey.info.scale_x}, {prevKey.info.scale_y}) -> " +
                                $"({thisKey.info.scale_x}, {thisKey.info.scale_y})";

                            bool isFlip =
                                Mathf.Sign(prevKey.info.scale_x) != Mathf.Sign(thisKey.info.scale_x) ||
                                Mathf.Sign(prevKey.info.scale_y) != Mathf.Sign(thisKey.info.scale_y);

                            if (isFlip)
                            {
                                hasScaleFlips = true;

                                if (!string.IsNullOrEmpty(notes))
                                {
                                    notes += ", ";
                                }

                                notes += "Scale flip!";
                            }

                            if (!string.IsNullOrEmpty(notes))
                            {
                                notes = "<-- " + notes;;
                            }

                            Log($"        time span: {prevKey.time_s:F3} -> {thisKey.time_s:F3}, " +
                                $"curve type: {prevKey.curve_type}, scales: {scalesStr}  {notes}");
                        }
                    }
                }

                if (hasNonlinearCurveTypes)
                {   // Use regular logging.  The user may need to use advanced animated bone scale support.
                    Debug.Log($"{prefix}Entity '{entity.name}' has one or more animated bones with non-linear curve types.");
                }

                if (hasScaleFlips)
                {   // Use regular logging.  The user may need to use advanced animated bone scale support.
                    Debug.Log($"{prefix}Entity '{entity.name}' has one or more animated bone scale flips.");
                }

                var namesOfBonesWithAnimatedScales = boneScaleInfos.Select(i => i.boneName).Distinct().OrderBy(n => n).ToList();

                SetupBonesWithAnimatedBoneScales(entity, namesOfBonesWithAnimatedScales);
                SetupObjectsWithAnimatedBoneScales(entity, namesOfBonesWithAnimatedScales);
            }
            else
            {
                Log($"Entity '{entity.name}' has no animations with animated bone scales.");
            }
        }

        private void SetupBonesWithAnimatedBoneScales(Entity entity, List<string> namesOfBonesWithAnimatedScales)
        {
            foreach (var boneInfo in boneInfos.Values)
            {
                if (!boneInfo.needsSpatialAdapter) // May have been marked already.
                {
                    // If this bone has any ancestor bones that use animated bone scales then this bone will be marked
                    // as having them as well.
                    foreach (var parentBoneName in boneInfo.parentBoneNames)
                    {
                        if (BoneHasAnimatedScales(entity, namesOfBonesWithAnimatedScales, parentBoneName, depth: 0))
                        {
                            boneInfo.needsSpatialAdapter = true;
                            break;
                        }
                    }
                }
            }

            var boneNamesWithAnimatedBoneScales = boneInfos.Values
                .Where(i => i.needsSpatialAdapter)
                .OrderBy(i => i.name)
                .Select(i => i.name)
                .ToList();

            if (boneNamesWithAnimatedBoneScales.Count > 0)
            {
                Log($"Entity '{entity.name}' has the following bones that may require a spatial adapter due " +
                    "to either having animated scales or having one or more ancestor bones that have an animated scale:");

                foreach (var boneName in boneNamesWithAnimatedBoneScales)
                {
                    Log($"    bone name: '{boneName}'");
                }
            }

            // For each of the bones in namesOfBonesWithAnimatedScales, walk up their hierarchy to the root and mark any
            // ancestors as needing a scale tracker if they aren't already marked as needing a spatial adapter...
            foreach (var boneName in namesOfBonesWithAnimatedScales)
            {
                foreach (var parentBoneName in boneInfos[boneName].parentBoneNames)
                {
                    // Note: "rootTransform" will be a parent but won't be in boneInfos.
                    var parentBoneInfo = boneInfos.GetOrDefault(parentBoneName);

                    if (parentBoneInfo != null)
                    {
                        MarkAncestorsForScaleTracker(parentBoneInfo, depth: 0);
                    }
                }
            }

            var boneNamesWithScaleTrackers = boneInfos.Values
                .Where(i => i.needsScaleTracker)
                .OrderBy(i => i.name)
                .Select(i => i.name)
                .ToList();

            if (boneNamesWithScaleTrackers.Count > 0)
            {
                Log($"Entity '{entity.name}', has the following bones that may need a scale tracker due to being an " +
                    "ancestor of one or more bones with animated scales:");

                foreach (var boneName in boneNamesWithScaleTrackers)
                {
                    Log($"    bone name: '{boneName}'");
                }
            }
        }

        private void MarkAncestorsForScaleTracker(SpriterBoneInfo parentBoneInfo, int depth)
        {
            if (++depth > 100)
            {
                return; // Guard against cycles.
            }

            if (!parentBoneInfo.needsSpatialAdapter)
            {
                parentBoneInfo.needsScaleTracker = true;
            }

            foreach (var parentName in parentBoneInfo.parentBoneNames)
            {
                // Note: "rootTransform" will be a parent but won't be in boneInfos.
                var boneInfo = boneInfos.GetOrDefault(parentName);

                if (boneInfo != null)
                {
                    MarkAncestorsForScaleTracker(boneInfo, depth);
                }
            }
        }

        private void SetupObjectsWithAnimatedBoneScales(Entity entity, List<string> namesOfBonesWithAnimatedScales)
        {
            // Some bones have already been setup with scale trackers.  Save their names.
            var boneNamesAlreadyWithScaleTrackers = boneInfos.Values
                .Where(i => i.needsScaleTracker)
                .OrderBy(i => i.name)
                .Select(i => i.name)
                .ToList();

            foreach (var objInfo in objectInfos.Values.Where(i => i.type == ObjectType.sprite || i.type == ObjectType.box))
            {
                // If this sprite or collision rectangle has any ancestor bones that use animated bone scales then the
                // sprite/box will be marked as having them as well.
                foreach (var parentBoneName in objInfo.parentBoneNames)
                {
                    if (BoneHasAnimatedScales(entity, namesOfBonesWithAnimatedScales, parentBoneName, depth: 0))
                    {
                        objInfo.needsSpatialAdapter = true;
                        break;
                    }
                }
            }

            var objectNamesWithAnimatedBoneScales = objectInfos.Values
                .Where(i => i.needsSpatialAdapter)
                .OrderBy(i => i.name)
                .Select(i => i.name)
                .ToList();

            if (objectNamesWithAnimatedBoneScales.Count > 0)
            {
                Log($"Entity '{entity.name}' has the following objects that may require a spatial adapter due " +
                    "to having one or more ancestor bones that have an animated scale:");

                foreach (var objectName in objectNamesWithAnimatedBoneScales)
                {
                    Log($"    sprite name: '{objectName}'");
                }
            }

            // For each of the objects in objectNamesWithAnimatedBoneScales, walk up their hierarchy to the root and
            // mark any ancestors as needing a scale tracker if they aren't already marked as needing a spatial adapter...
            foreach (var objectName in objectNamesWithAnimatedBoneScales)
            {
                foreach (var parentBoneName in objectInfos[objectName].parentBoneNames)
                {
                    // Note: "rootTransform" will be a parent but won't be in boneInfos.
                    var parentBoneInfo = boneInfos.GetOrDefault(parentBoneName);

                    if (parentBoneInfo != null)
                    {
                        MarkAncestorsForScaleTracker(parentBoneInfo, depth: 0);
                    }
                }
            }

            // This will be the bone names we just added, if any.
            var newBoneNamesWithScaleTrackers = boneInfos.Values
                .Where(i => i.needsScaleTracker && !boneNamesAlreadyWithScaleTrackers.Exists(n => n == i.name))
                .OrderBy(i => i.name)
                .Select(i => i.name)
                .ToList();

            if (newBoneNamesWithScaleTrackers.Count > 0)
            {
                string note = boneNamesAlreadyWithScaleTrackers.Count > 0 ? "(additional) " : "";

                Log($"Entity '{entity.name}', has the following {note}bones that may need a scale tracker due " +
                    "to being an ancestor of one or more objects that use a spatial adapter:");

                foreach (var boneName in newBoneNamesWithScaleTrackers)
                {
                    Log($"    bone name: '{boneName}'");
                }
            }
        }

        // ! This and BoneUsesAlphaController() are basically the same method.
        private bool BoneHasAnimatedScales(Entity entity, List<string> namesOfBonesWithAnimatedScales, string boneName, int depth)
        {
            if (++depth > 100)
            {
                return false; // Guard against cycles.
            }

            if (namesOfBonesWithAnimatedScales.Contains(boneName))
            {
                return true;
            }

            var boneInfo = boneInfos.GetOrDefault(boneName);

            if (boneInfo != null)
            {
                foreach (var parentName in boneInfo.parentBoneNames)
                {
                    if (BoneHasAnimatedScales(entity, namesOfBonesWithAnimatedScales, parentName, depth))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void CheckForBoneAlphaUse(Entity entity)
        {
            var namesOfBonesUsingAlpha =
                (from anim in entity.animations
                 from mlk in anim.mainlineKeys
                 from boneRef in mlk.boneRefs
                 let boneTimeline = anim.timelines.FirstOrDefault(t => t.id == boneRef.timelineId)
                 let boneName = boneTimeline?.name ?? "Unknown"
                 from tlk in boneTimeline.keys
                 where tlk.info.a < 1f
                 select boneName)
                .Distinct()
                .OrderBy(bn => bn)
                .ToList();

            if (namesOfBonesUsingAlpha.Count > 0)
            {
                Log($"Entity '{entity.name}' has the following bones that will require an alpha controller:");

                foreach (var boneName in namesOfBonesUsingAlpha)
                {
                    Log($"    bone name: '{boneName}'");

                    boneInfos[boneName].hasAlphaController = true;
                }

                SetupSpritesWithAlphaController(entity, namesOfBonesUsingAlpha);
            }
            else
            {
                Log($"Entity '{entity.name}' has no bones or sprites that require an alpha controller.");
            }
        }

        private void SetupSpritesWithAlphaController(Entity entity, List<string> namesOfBonesUsingAlpha)
        {
            foreach (var spriteInfo in objectInfos.Values.Where(i => i.type == ObjectType.sprite))
            {
                // If this sprite has any ancestors using an alpha controller then the sprite will also need an
                // alpha controller.
                foreach (var parentBoneName in spriteInfo.parentBoneNames)
                {
                    if (BoneUsesAlphaController(entity, namesOfBonesUsingAlpha, parentBoneName, depth: 0))
                    {
                        spriteInfo.hasAlphaController = true;
                        break;
                    }
                }
            }

            var spriteNamesWithAlphaControllers = objectInfos.Values
                .Where(i => i.hasAlphaController)
                .OrderBy(i => i.name)
                .Select(i => i.name)
                .ToList();

            if (spriteNamesWithAlphaControllers.Count > 0)
            {
                Log($"Entity '{entity.name}' has the following sprites that will require an alpha controller due " +
                    "to having an ancestor using bone alpha:");

                foreach (var spriteName in spriteNamesWithAlphaControllers)
                {
                    Log($"    sprite name: '{spriteName}'");
                }
            }
        }

        private bool BoneUsesAlphaController(Entity entity, List<string> namesOfBonesUsingAlpha, string boneName, int depth)
        {
            if (++depth > 100)
            {
                return false; // Guard against cycles.
            }

            if (namesOfBonesUsingAlpha.Contains(boneName))
            {
                return true;
            }

            var boneInfo = boneInfos.GetOrDefault(boneName);

            if (boneInfo != null)
            {
                foreach (var parentName in boneInfo.parentBoneNames)
                {
                    if (BoneUsesAlphaController(entity, namesOfBonesUsingAlpha, parentName, depth))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void LogProjectScopedTagInfo(ScmlObject scmlObject)
        {
            if (scmlObject.tagDefs.Count > 0)
            {
                Log("This Spriter project has the following tag definitions:");

                foreach (var tagDef in scmlObject.tagDefs)
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

                    // Figure out the variable's bind pose value...
                    var firstKeyFromFirstAnim = entity.animations
                        .ElementAtOrDefault(0)?
                        .metadata?
                        .varlines?.FirstOrDefault(vl => vl.varDefId == variableDef.id)?
                        .keys?.ElementAtOrDefault(0);

                    var firstKeyTime = firstKeyFromFirstAnim?.time_s;
                    var firstKeyValue = firstKeyFromFirstAnim?.value;

                    string bindPoseValue = firstKeyTime != null && firstKeyTime == 0f ? firstKeyValue : variableDef.defaultValue;
                    var newVarInstanceInfo = new VarInstanceInfo()
                    {
                        varDef = variableDef,
                        bindPoseValue = bindPoseValue
                    };

                    varInstanceInfos.Add(variableDef.id, newVarInstanceInfo);
                }
            }
            else
            {
                Log($"Entity '{entity.name}' has no entity-scoped variable definitions.");
            }

            var entityScopedTagDefIds = (
                from anim in entity.animations
                where anim.metadata != null
                from taglineKeys in anim.metadata.taglineKeys
                from tags in taglineKeys.tags
                select tags.tagDefId
            )
            .Distinct()
            .OrderBy(id => id)
            .ToList();

            if (entityScopedTagDefIds.Count > 0)
            {
                Log($"Entity '{entity.name}' uses the following entity-scoped tags:");

                foreach (var tagDefId in entityScopedTagDefIds)
                {
                    var tagDef = scmlObject.tagDefs.FirstOrDefault(t => t.id == tagDefId);

                    if (tagDef != null)
                    {
                        Log($"    tag id: {tagDef.id}, tag name: {tagDef.name}");

                        // Figure out the tag's bind pose value...
                        var firstKeyFromFirstAnim =
                            entity.animations.ElementAtOrDefault(0)?
                            .metadata?
                            .taglineKeys?.ElementAtOrDefault(0);

                        bool bindPoseValue = (firstKeyFromFirstAnim?.time_s is 0f)
                            ? firstKeyFromFirstAnim.tags.Exists(t => t.tagDefId == tagDefId)
                            : false;

                        var newTagInstanceInfo = new TagInstanceInfo()
                        {
                            tagDef = tagDef,
                            bindPoseValue = bindPoseValue
                        };

                        tagInstanceInfos.Add(tagDef.id, newTagInstanceInfo);
                    }
                    else
                    {
                        Debug.LogWarning($"An invalid id ({tagDefId}) was found while processing the entity-scoped tag " +
                            $"metadata for entity: '{entity.name}'.  A tag list item with that id was not found.");
                    }
                }
            }
            else
            {
                Log($"Entity '{entity.name}' has no entity-scoped tags.");
            }
        }

        private void AssignEntityScopedMetadataReferences(ScmlObject scmlObject, Entity entity)
        {
            // Put the appropriate references in entity.animation[].metadata.varlines[].varDef and
            // entity.animation[].metadata.taglineKeys[].tags[].tagName.

            var animsWithMetadata = entity.animations.Where(a => a.metadata != null).ToList();

            if (animsWithMetadata.Count == 0)
            {
                Log($"Entity: '{entity.name}' has no entity-scoped metadata to assign.");
                return;
            }

            foreach (var anim in animsWithMetadata)
            {
                for (int i = 0; i < anim.metadata.varlines.Count; ++i)
                {
                    var varline = anim.metadata.varlines[i];

                    var varInstanceInfo = varInstanceInfos.GetOrDefault(varline.varDefId);
                    varline.varDef = varInstanceInfo?.varDef;

                    if (varline.varDef != null)
                    {
                        Log($"Entity-scoped varline varDef assigned for entity: {entity.name}, animation: {anim.name}, " +
                            $"metadata.varlines[{i}], (id: {varline.id}, varDefId: {varline.varDefId})");
                        Log($"    variable name: {varline.varDef.name}, type: {varline.varDef.type}, " +
                            $"default value: '{varline.varDef.defaultValue}', bind pose value: '{varInstanceInfo.bindPoseValue}'");
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

                        var tagInstanceInfo = tagInstanceInfos.GetOrDefault(tag.tagDefId);
                        tag.tagName = tagInstanceInfo?.tagDef?.name;

                        if (tag.tagName != null)
                        {
                            Log($"Entity-scoped tagline TagInfo name assigned for entity: {entity.name}, " +
                                $"animation: {anim.name}, metadata.taglines[{keyIdx}].tags[{tagIdx}], (id: {tag.id}, " +
                                $"tagDefId: {tag.tagDefId})");
                            Log($"    tag name: {tag.tagName}, bind pose value: {tagInstanceInfo.bindPoseValue}");
                        }
                        else
                        {
                            Debug.LogWarning($"While processing the tag metadata (at index [${keyIdx}][{tagIdx}]) " +
                                $"for entity: '{entity.name}', animation: '{anim.name}', a tag definition for " +
                                $"id: {tag.tagDefId} was missing from the tag definitions.");
                        }
                    }
                }
            }
        }

        private void PreprocessObjectScopedMetadata(ScmlObject scmlObject, Entity entity)
        {
            Log($"Entity '{entity.name}', preprocessing object-scoped metadata for all bones...");

            var allBones = boneInfos.Values.Where(o => o.type == ObjectType.bone).ToList();

            if (allBones.Count > 0)
            {
                DoPreprocessObjectScopedMetadata(scmlObject, entity, allBones);
            }
            else
            {
                Log($"    Entity '{entity.name}' has no bones.");
            }

            Log($"Entity: '{entity.name}', preprocessing object-scoped metadata for all sprites...");

            var allSprites = objectInfos.Values.Where(o => o.type == ObjectType.sprite).ToList();

            if (allSprites.Count > 0)
            {
                DoPreprocessObjectScopedMetadata(scmlObject, entity, allSprites);
            }
            else
            {
                Log($"    Entity: '{entity.name}' has no sprites.");
            }

            Log($"Entity: '{entity.name}', preprocessing object-scoped metadata for all action points...");

            var allActionPoints = objectInfos.Values.Where(o => o.type == ObjectType.point).ToList();

            if (allActionPoints.Count > 0)
            {
                DoPreprocessObjectScopedMetadata(scmlObject, entity, allActionPoints);
            }
            else
            {
                Log($"    Entity: '{entity.name}' has no action points.");
            }

            Log($"Entity: '{entity.name}', preprocessing object-scoped metadata for all collision rectangles...");

            var allCollisionRectangles = objectInfos.Values.Where(o => o.type == ObjectType.box).ToList();

            if (allCollisionRectangles.Count > 0)
            {
                DoPreprocessObjectScopedMetadata(scmlObject, entity, allActionPoints);
            }
            else
            {
                Log($"    Entity: '{entity.name}' has no collision rectangles.");
            }
        }

        private void DoPreprocessObjectScopedMetadata<T>(ScmlObject scmlObject, Entity entity, List<T> allInfos) where T : SpriterInfoBase
        {
            int totalVars = 0;
            int totalTags = 0;

            foreach (var info in allInfos)
            {
                int numVars;
                int numTags;

                DoPreprocessObjectScopedMetadata(scmlObject, entity, info, out numVars, out numTags);

                totalVars += numVars;
                totalTags += numTags;
            }

            Log($"    Total # of Variables: {totalVars}");
            Log($"    Total # of Tags: {totalTags}");
        }

        private void DoPreprocessObjectScopedMetadata(ScmlObject scmlObject, Entity entity, SpriterInfoBase info,
            out int numVars, out int numTags)
        {
            // Populate info.variableDefs and info.tagDefs collections

            numVars = 0;
            numTags = 0;

            if (info.type == ObjectType.spriterEvent)
            {
                Debug.LogWarning("An object was passed to DoPreprocessObjectScopedMetadata() that has a type of 'event'.");
                return;
            }

            var allVarDefs = entity.objectInfos.FirstOrDefault(o => o.name == info.name && o.objectType == info.type)?.variableDefs;
            numVars = allVarDefs != null ? allVarDefs.Count : 0;

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

                    // Figure out the variable's bind pose value...
                    var firstKeyFromFirstAnim = entity.animations
                        .ElementAtOrDefault(0)?
                        .timelines.FirstOrDefault(tl => tl.name == info.name && tl.objectType == info.type)?
                        .metadata?
                        .varlines?.FirstOrDefault(vl => vl.varDefId == variableDef.id)?
                        .keys?.ElementAtOrDefault(0);

                    var firstKeyTime = firstKeyFromFirstAnim?.time_s;
                    var firstKeyValue = firstKeyFromFirstAnim?.value;

                    string bindPoseValue = firstKeyTime != null && firstKeyTime == 0f ? firstKeyValue : variableDef.defaultValue;
                    var newVarInstanceInfo = new VarInstanceInfo()
                    {
                        varDef = variableDef,
                        bindPoseValue = bindPoseValue
                    };

                    info.varInstanceInfos.Add(variableDef.id, newVarInstanceInfo);
                }
            }

            var objectScopedTagDefIds = (
                from anim in entity.animations
                from timeline in anim.timelines
                where timeline.name == info.name && timeline.objectType == info.type && timeline.metadata != null
                from taglineKeys in timeline.metadata.taglineKeys
                from tags in taglineKeys.tags
                select tags.tagDefId
            )
            .Distinct()
            .OrderBy(id => id)
            .ToList();

            numTags = objectScopedTagDefIds.Count;

            if (objectScopedTagDefIds.Count > 0)
            {
                Log($"    '{info.name}' uses the following object-scoped tags:");

                foreach (var tagDefId in objectScopedTagDefIds)
                {
                    var tagDef = scmlObject.tagDefs.FirstOrDefault(t => t.id == tagDefId);

                    if (tagDef != null)
                    {
                        Log($"        tag id: {tagDef.id}, tag name: {tagDef.name}");

                        // Figure out the tag's bind pose value...
                        var firstKeyFromFirstAnim = entity.animations
                            .ElementAtOrDefault(0)?
                            .timelines.FirstOrDefault(tl => tl.name == info.name && tl.objectType == info.type)?
                            .metadata?
                            .taglineKeys?.ElementAtOrDefault(0);

                        bool bindPoseValue = (firstKeyFromFirstAnim?.time_s is 0f)
                            ? firstKeyFromFirstAnim.tags.Exists(t => t.tagDefId == tagDefId)
                            : false;

                        var newTagInstanceInfo = new TagInstanceInfo()
                        {
                            tagDef = tagDef,
                            bindPoseValue = bindPoseValue
                        };

                        info.tagInstanceInfos.Add(tagDef.id, newTagInstanceInfo);
                    }
                    else
                    {
                        Debug.LogWarning($"An invalid id ({tagDefId}) was found while processing the object-scoped tag " +
                            $"metadata for entity: '{entity.name}', timeline: '{info.name}'.  A tag list item with " +
                            "that id was not found.");
                    }
                }
            }
        }

        private void PreprocessEventScopedMetadata(ScmlObject scmlObject, Entity entity)
        {
            Log($"Entity: '{entity.name}', preprocessing event-scoped metadata for all events...");

            var allEvents = objectInfos.Values.Where(o => o.type == ObjectType.spriterEvent).ToList();

            if (allEvents.Count > 0)
            {
                int totalVars = 0;
                int totalTags = 0;

                foreach (var eventInfo in allEvents)
                {
                    int numVars;
                    int numTags;

                    DoPreprocessEventScopedMetadata(scmlObject, entity, eventInfo, out numVars, out numTags);

                    totalVars += numVars;
                    totalTags += numTags;
                }

                Log($"    Total # of Variables: {totalVars}");
                Log($"    Total # of Tags: {totalTags}");
                {
                }
            }
            else
            {
                Log($"    '{entity.name}' has no events.");
            }
        }

        private void DoPreprocessEventScopedMetadata(ScmlObject scmlObject, Entity entity, SpriterInfoBase info,
            out int numVars, out int numTags)
        {
            numVars = 0;
            numTags = 0;

            // Populate info.variableDefs and info.tagDefs collections

            if (info.type != ObjectType.spriterEvent)
            {
                Debug.LogWarning("An object was passed to DoPreprocessEventScopedMetadata() that does not have a type of 'event'.");
                return;
            }

            var allVarDefs = entity.objectInfos.FirstOrDefault(o => o.name == info.name && o.objectType == info.type)?.variableDefs;
            numVars = allVarDefs != null ? allVarDefs.Count : 0;

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

                    // Figure out the variable's bind pose value...
                    var firstKeyFromFirstAnim = entity.animations
                        .ElementAtOrDefault(0)?
                        .eventlines.FirstOrDefault(el => el.name == info.name)?
                        .metadata?
                        .varlines?.FirstOrDefault(vl => vl.varDefId == variableDef.id)?
                        .keys?.ElementAtOrDefault(0);

                    var firstKeyTime = firstKeyFromFirstAnim?.time_s;
                    var firstKeyValue = firstKeyFromFirstAnim?.value;

                    string bindPoseValue = firstKeyTime != null && firstKeyTime == 0f ? firstKeyValue : variableDef.defaultValue;
                    var newVarInstanceInfo = new VarInstanceInfo()
                    {
                        varDef = variableDef,
                        bindPoseValue = bindPoseValue
                    };

                    info.varInstanceInfos.Add(variableDef.id, newVarInstanceInfo);
                }
            }

            var eventScopedTagDefIds = (
                from anim in entity.animations
                from eventline in anim.eventlines
                where eventline.name == info.name && eventline.metadata != null
                from taglineKeys in eventline.metadata.taglineKeys
                from tags in taglineKeys.tags
                select tags.tagDefId
            )
            .Distinct()
            .OrderBy(id => id)
            .ToList();

            numTags = eventScopedTagDefIds.Count;

            if (eventScopedTagDefIds.Count > 0)
            {
                Log($"    '{info.name}' uses the following event-scoped tags:");

                foreach (var tagDefId in eventScopedTagDefIds)
                {
                    var tagDef = scmlObject.tagDefs.FirstOrDefault(t => t.id == tagDefId);

                    if (tagDef != null)
                    {
                        Log($"        tag id: {tagDef.id}, tag name: {tagDef.name}");

                        // Figure out the tag's bind pose value...
                        var firstKeyFromFirstAnim = entity.animations
                            .ElementAtOrDefault(0)?
                            .eventlines.FirstOrDefault(el => el.name == info.name)?
                            .metadata?
                            .taglineKeys?.ElementAtOrDefault(0);

                        bool bindPoseValue = (firstKeyFromFirstAnim?.time_s is 0f)
                            ? firstKeyFromFirstAnim.tags.Exists(t => t.tagDefId == tagDefId)
                            : false;

                        var newTagInstanceInfo = new TagInstanceInfo()
                        {
                            tagDef = tagDef,
                            bindPoseValue = bindPoseValue
                        };

                        info.tagInstanceInfos.Add(tagDef.id, newTagInstanceInfo);
                    }
                    else
                    {
                        Debug.LogWarning($"An invalid id ({tagDefId}) was found while processing the event-scoped tag " +
                            $"metadata for entity: '{entity.name}', event: '{info.name}'.  A tag list item with that " +
                            "id was not found.");
                    }
                }
            }
        }

        private void AssignObjectScopedMetadataReferences(ScmlObject scmlObject, Entity entity)
        {
            Log($"Entity '{entity.name}', assigning object-scoped metadata references for all bones...");

            var allBones = boneInfos.Values.Where(o => o.type == ObjectType.bone).ToList();

            if (allBones.Count > 0)
            {
                foreach (var boneInfo in allBones)
                {
                    if (boneInfo.hasMetadata)
                    {
                        DoAssignObjectScopedMetadataReferences(scmlObject, entity, boneInfo);
                    }
                }
            }
            else
            {
                Log($"    Entity '{entity.name}' has no bones.");
            }

            Log($"Entity '{entity.name}', assigning object-scoped metadata references for all sprites...");

            var allSprites = objectInfos.Values.Where(o => o.type == ObjectType.sprite).ToList();

            if (allSprites.Count > 0)
            {
                foreach (var spriteInfo in allSprites)
                {
                    if (spriteInfo.hasMetadata)
                    {
                        DoAssignObjectScopedMetadataReferences(scmlObject, entity, spriteInfo);
                    }
                }
            }
            else
            {
                Log($"    Entity '{entity.name}' has no sprites.");
            }

            Log($"Entity '{entity.name}', assigning object-scoped metadata references for all action points...");

            var allActionPoints = objectInfos.Values.Where(o => o.type == ObjectType.point).ToList();

            if (allActionPoints.Count > 0)
            {
                foreach (var actionPtInfo in allActionPoints)
                {
                    if (actionPtInfo.hasMetadata)
                    {
                        DoAssignObjectScopedMetadataReferences(scmlObject, entity, actionPtInfo);
                    }
                }
            }
            else
            {
                Log($"    Entity '{entity.name}' has no action points.");
            }

            Log($"Entity '{entity.name}', assigning object-scoped metadata references for all collision rectangles...");

            var allCollisionRectangles = objectInfos.Values.Where(o => o.type == ObjectType.box).ToList();

            if (allCollisionRectangles.Count > 0)
            {
                foreach (var collisionRectangleInfo in allCollisionRectangles)
                {
                    if (collisionRectangleInfo.hasMetadata)
                    {
                        DoAssignObjectScopedMetadataReferences(scmlObject, entity, collisionRectangleInfo);
                    }
                }
            }
            else
            {
                Log($"    Entity '{entity.name}' has no collision rectangles.");
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

                        var varInstanceInfo = info.varInstanceInfos.GetOrDefault(varline.varDefId);
                        varline.varDef = varInstanceInfo?.varDef;

                        if (varline.varDef != null)
                        {
                            Log($"    Object-scoped varline varDef assigned for entity: {entity.name}, animation: {anim.name}, " +
                                $"timeline: {timeline.name}, metadata.varlines[{i}], (id: {varline.id}, varDefId: {varline.varDefId})");
                            Log($"        variable name: {varline.varDef.name}, type: {varline.varDef.type}, " +
                                $"default value: '{varline.varDef.defaultValue}', bind pose value: '{varInstanceInfo.bindPoseValue}'");
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

                            var tagInstanceInfo = info.tagInstanceInfos.GetOrDefault(tag.tagDefId);
                            tag.tagName = tagInstanceInfo?.tagDef?.name;

                            if (tag.tagName != null)
                            {
                                Log($"    Object-scoped tagline TagInfo name assigned for entity: {entity.name}, " +
                                    $"animation: {anim.name}, timeline: {timeline.name}, " +
                                    $"metadata.taglines[{keyIdx}].tags[{tagIdx}], (id: {tag.id}, tagId: {tag.tagDefId})");
                                Log($"        tag name: {tag.tagName}, bind pose value: {tagInstanceInfo.bindPoseValue}");
                            }
                            else
                            {
                                Debug.LogWarning($"While processing the tag metadata (at index [${keyIdx}][{tagIdx}]) " +
                                    $"for entity: '{entity.name}', animation: '{anim.name}', timeline: '{timeline.name}', " +
                                    $" a tag definition for id: {tag.tagDefId} was missing from the tag definitions.");
                            }
                        }
                    }
                }
            }
        }

        private void AssignEventScopedMetadataReferences(ScmlObject scmlObject, Entity entity)
        {
            Log($"Entity '{entity.name}', assigning event-scoped metadata references for all events...");

            var allEvents = objectInfos.Values.Where(o => o.type == ObjectType.spriterEvent).ToList();

            if (allEvents.Count > 0)
            {
                foreach (var eventInfo in allEvents)
                {
                    if (eventInfo.hasMetadata)
                    {
                        DoAssignEventScopedMetadataReferences(scmlObject, entity, eventInfo);
                    }
                }
            }
            else
            {
                Log($"    '{entity.name}' has no events.");
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

                        var varInstanceInfo = info.varInstanceInfos.GetOrDefault(varline.varDefId);
                        varline.varDef = varInstanceInfo?.varDef;

                        if (varline.varDef != null)
                        {
                            Log($"    Event-scoped varline varDef assigned for entity: {entity.name}, animation: {anim.name}, " +
                                $"eventline: {eventline.name}, metadata.varlines[{i}], (id: {varline.id}, varDefId: {varline.varDefId})");
                            Log($"        variable name: {varline.varDef.name}, type: {varline.varDef.type}, " +
                                $"default value: '{varline.varDef.defaultValue}', bind pose value: '{varInstanceInfo.bindPoseValue}'");
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

                            var tagInstanceInfo = info.tagInstanceInfos.GetOrDefault(tag.tagDefId);
                            tag.tagName = tagInstanceInfo?.tagDef?.name;

                            if (tag.tagName != null)
                            {
                                Log($"    Event-scoped tagline TagInfo for entity: {entity.name}, animation: {anim.name}, " +
                                    $"eventlinel: {eventline.name}, metadata.taglines[{keyIdx}].tags[{tagIdx}], (id: {tag.id}, tagId: {tag.tagDefId})");
                                Log($"        tag name: {tag.tagName}, bind pose value: {tagInstanceInfo.bindPoseValue}");
                            }
                            else
                            {
                                Debug.LogWarning($"While processing the tag metadata for entity: '{entity.name}', animation: '{anim.name}', " +
                                    $"eventline: '{eventline.name}', a tag definition for id: {tag.tagDefId} was missing from the tag definitions.");
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

            if (allBoneNames.Count > 0)
            {
                Log($"Entity '{entity.name}' has the following bones:");

                foreach (var boneName in allBoneNames)
                {
                    Log($"    '{boneName}'");

                    boneInfos.Add(boneName, new SpriterBoneInfo(boneName, ObjectType.bone));
                }
            }
            else
            {
                Log($"Entity '{entity.name}' has no bones.");
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

            if (allSpriteNames.Count > 0)
            {
                Log($"Entity '{entity.name}' has the following sprites:");

                foreach (var spriteName in allSpriteNames)
                {
                    Log($"    '{spriteName}'");

                    objectInfos.Add(spriteName, new SpriterObjectInfo(spriteName, ObjectType.sprite));
                }
            }
            else
            {
                Log($"Entity '{entity.name}' has no sprites.");
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

                    objectInfos.Add(actionPointName, new SpriterObjectInfo(actionPointName, ObjectType.point));
                }
            }
            else
            {
                Log($"Entity '{entity.name}' has no action points.");
            }
        }

        private void PreprocessCollisionRectangles(Entity entity)
        {
            // Populate the objectInfo collection with any collision rectangles...
            var allCollisionRectangleNames =
                (from anim in entity.animations
                 from tl in anim.timelines
                 where tl.objectType == ObjectType.box
                 select tl.name)
                .Distinct()
                .ToList();

            if (allCollisionRectangleNames.Count > 0)
            {
                Log($"Entity '{entity.name}' has the following collision rectangles:");

                foreach (var collisionRectangleName in allCollisionRectangleNames)
                {
                    Log($"    '{collisionRectangleName}'");

                    objectInfos.Add(collisionRectangleName, new SpriterObjectInfo(collisionRectangleName, ObjectType.box));
                }
            }
            else
            {
                Log($"Entity '{entity.name}' has no collision rectangles.");
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

                    objectInfos.Add(eventName, new SpriterObjectInfo(eventName, ObjectType.spriterEvent));
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
                Log($"Entity '{entity.name}' has no soundlines.");
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
                            .FirstOrDefault(f => f.id == soundObject.folderId)?
                            .files
                            .FirstOrDefault(file => file.id == soundObject.fileId);

                    if (fileItem == null)
                    {
                        Debug.LogWarning("A soundline references a sound file but that file doesn't exist in the " +
                            $"project's folder/file list.  FolderId: {soundObject.folderId}, FileId: {soundObject.fileId}");
                    }
                    else if (fileItem.objectType != ObjectType.sound)
                    {
                        Debug.LogWarning("A soundline references a sound file but the referenced file doesn't have the " +
                            $"type of {ObjectType.sound}.  The referenced file has a type of {fileItem.objectType}.  " +
                            $"FolderId: {soundObject.folderId}, FileId: {soundObject.fileId}");
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
                    && tl.objectType != ObjectType.box
                 select new { tl.name, tl.objectType })
                .Distinct()
                .ToList();

            if (allUnsupportedTypeNames.Count > 0)
            {
                Debug.LogWarning($"Entity '{entity.name}' has the following unsupported types:");

                foreach (var unsupportedType in allUnsupportedTypeNames)
                {
                    Debug.LogWarning($"    '{unsupportedType.name}' (type of {unsupportedType.objectType})");

                    // This still gets an entry in objectInfo.
                    objectInfos.Add(unsupportedType.name, new SpriterObjectInfo(unsupportedType.name, unsupportedType.objectType));
                }
            }
            else
            {
                Log($"Entity: '{entity.name}' has no unsupported types.");
            }

            // Check for any names that are duplicated in both collections and warn the
            // user if any are found.  This might be a problem for very old Spriter files...
            var sharedNames = objectInfos.Keys
                .Intersect(boneInfos.Keys)
                .ToList();

            if (sharedNames.Count > 0)
            {
                Debug.LogWarning($"Entity '{entity.name}' has one or more bones that have the same name as an object.  " +
                    "This may cause problems and should be avoided.  The names follow:");

                foreach (var name in sharedNames)
                {
                    Debug.LogWarning($"    The name '{name}' is shared by both a bone and a {objectInfos[name].type}");
                }
            }
            else
            {
                Log($"Entity: '{entity.name}' has no bones that have the same name as an object.");
            }
        }

        private void PreprocessSpritePivots(Entity entity, Dictionary<int, IDictionary<int, File>> fileInfo)
        {
            // Setup all of the entity's sprite pivots...
            var spriteInfos =
                from anim in entity.animations
                from mlk in anim.mainlineKeys
                from oref in mlk.objectRefs
                let tl = anim.timelines.FirstOrDefault(t => t.id == oref.timelineId)
                where tl != null && tl.objectType == ObjectType.sprite
                from key in tl.keys
                where key.info is SpriteInfo
                select (SpriteInfo)key.info;

            foreach (var si in spriteInfos)
            {
                var spriteFileInfo = fileInfo[si.folderId][si.fileId];
                si.InitPivots(spriteFileInfo);
            }

            // Find all the sprite names for sprites that have non-default pivots...
            var nonDefaultPivotSpriteNames =
                (from anim in entity.animations
                 from mlk in anim.mainlineKeys
                 from oref in mlk.objectRefs
                 let tl = anim.timelines.FirstOrDefault(t => t.id == oref.timelineId)
                 where tl != null && tl.objectType == ObjectType.sprite
                 from key in tl.keys
                 let si = key.info as SpriteInfo
                 where si != null && !si.IsDefaultPivots
                 select tl.name)
                .Distinct().ToList();

            if (nonDefaultPivotSpriteNames.Count > 0)
            {
                Log($"For entity '{entity.name}', the following sprites have non-default pivots and will need a pivot controller.");

                foreach (var spriteName in nonDefaultPivotSpriteNames)
                {
                    Log($"    '{spriteName}'");

                    objectInfos[spriteName].hasPivotController = true;
                    objectInfos[spriteName].pivotControllerTransformName = spriteName;
                    objectInfos[spriteName].spriteRendererTransformName = spriteName + " renderer";
                }
            }
            else
            {
                Log($"Entity '{entity.name}' has no sprites with a non-default pivot.");
            }
        }

        private void PreprocessBoneParents(Entity entity)
        {
            var boneNamesAndTheirParentNames =
                (from anim in entity.animations
                 from mlk in anim.mainlineKeys
                 from boneRef in mlk.boneRefs
                 let boneTimeline = anim.timelines.FirstOrDefault(t => t.id == boneRef.timelineId)
                 let boneName = boneTimeline?.name ?? "Unknown"
                 let parentBoneName = boneRef.parentRefId == -1
                             ? "rootTransform"
                             : anim.timelines.FirstOrDefault(t =>
                                 t.id == mlk.boneRefs.FirstOrDefault(b => b.id == boneRef.parentRefId)?.timelineId)?.name ?? "Unknown"
                 select new
                 {
                     boneName,
                     parentBoneName
                 })
                .Distinct()
                .GroupBy(x => x.boneName)
                .Select(g => new
                {
                    BoneName = g.Key,
                    ParentBoneNames = g.Select(x => x.parentBoneName).Distinct().ToList()
                })
                .OrderBy(x => x.BoneName)
                .ToList();

            bool hasBonesWithMultipleParents = boneNamesAndTheirParentNames.Exists(bi => bi.ParentBoneNames.Count > 1);

            if (hasBonesWithMultipleParents)
            {
                Log($"For entity '{entity.name}', some bones have more than one parent and will need a virtual parent.");
            }
            else
            {
                Log($"Entity '{entity.name}' has no bones that have more than one parent.");
            }

            Log($"Entity '{entity.name}', list of bones and their parent(s):");

            if (boneNamesAndTheirParentNames.Count > 0)
            {
                foreach (var info in boneNamesAndTheirParentNames)
                {
                    var parentNamesString = string.Join(", ", info.ParentBoneNames.Select(s => $"'{s}'"));
                    var callout = info.ParentBoneNames.Count > 1 ? "<-- Multiple parents" : "";

                    Log($"    bone name: '{info.BoneName}', parent name(s) are: {parentNamesString} {callout}");

                    boneInfos[info.BoneName].parentBoneNames.AddRange(info.ParentBoneNames);
                }
            }
            else
            {
                Log($"    Entity '{entity.name}' has no bones.");
            }
        }

        private void PreprocessObjectParents(Entity entity)
        {
            var objectNamesAndTheirParentNames =
                (from anim in entity.animations
                 from mlk in anim.mainlineKeys
                 from objectRef in mlk.objectRefs
                 let objectTimeline = anim.timelines.FirstOrDefault(t => t.id == objectRef?.timelineId)
                 let objectName = objectTimeline?.name ?? "Unknown"
                 let parentBoneName = objectRef.parentRefId == -1
                     ? "rootTransform"
                     : anim.timelines.FirstOrDefault(t =>
                         t.id == mlk.boneRefs.FirstOrDefault(b => b.id == objectRef.parentRefId)?.timelineId)?.name ?? "Unknown"
                 select new
                 {
                     objectName,
                     parentBoneName
                 })
                .Distinct()
                .GroupBy(x => x.objectName)
                .Select(g => new
                {
                    ObjectName = g.Key,
                    ParentBoneNames = g.Select(x => x.parentBoneName).Distinct().ToList()
                })
                .OrderBy(x => x.ObjectName)
                .ToList();

            bool hasObjectsWithMultipleParents = objectNamesAndTheirParentNames.Exists(bi => bi.ParentBoneNames.Count > 1);

            if (hasObjectsWithMultipleParents)
            {
                Log($"For entity '{entity.name}', some objects have more than one parent and will need a virtual parent.");
            }
            else
            {
                Log($"Entity '{entity.name}' has no objects that have more than one parent.");
            }

            Log($"Entity '{entity.name}', list of objects and their parent(s):");

            if (objectNamesAndTheirParentNames.Count > 0)
            {
                foreach (var info in objectNamesAndTheirParentNames)
                {
                    var parentNamesString = string.Join(", ", info.ParentBoneNames.Select(s => $"'{s}'"));
                    var callout = info.ParentBoneNames.Count > 1 ? "<-- Multiple parents" : "";

                    Log($"    object name: '{info.ObjectName}', parent name(s) are: {parentNamesString} {callout}");

                    objectInfos[info.ObjectName].parentBoneNames.AddRange(info.ParentBoneNames);
                }
            }
            else
            {
                Log($"    Entity '{entity.name}' has no objects.");
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
            // timeline key will (sometimes) be created and it will have the same time as the previous
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
                let myRef = refs?.FirstOrDefault(r => r.timelineId == tl.id)

                let parentRef =
                    (myRef == null || myRef.parentRefId == -1 || mlk == null)
                        ? null
                        : mlk.boneRefs.FirstOrDefault(r => r.id == myRef.parentRefId)

                let parentName =
                    parentRef == null
                        ? "rootTransform"
                        : timelinesById[anim][parentRef.timelineId].name

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

            int numParentChanges = 0;
            int numPivotChanges = 0;

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

                        if (isPivotChange)
                        {
                            numPivotChanges++;
                            Log($"Entity: '{entity.name}', animation: '{animation.name}', timeline: '{timeline.name}, " +
                                $"pivot change (requiring timing adjustment) detected at time: {currTime_s}");
                        }
                    }

                    // Apply parent change
                    if (isParentChange)
                    {
                        numParentChanges++;
                        Log($"Entity: '{entity.name}', animation: '{animation.name}', timeline: '{timeline.name}, " +
                            $"parent change (requiring timing adjustment) detected at time: {currTime_s}");

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
                        // key if necessary.  (This is seen fairly often and I'm not sure why.)

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

            Log($"Entity '{entity.name}', has {numParentChanges} parent changes across all animations that require timing adjustments.");
            Log($"Entity '{entity.name}', has {numPivotChanges} pivot changes across all animations that require timing adjustments.");
        }

#if ENABLE_STUI_DEBUG_LOGS
        private const bool forceLogging = true;
#else
        private const bool forceLogging = false;
#endif

        private void Log(object message, Object context = null)
        {
            if (forceLogging || loggingEnabled)
            {
                string prefix = loggingEnabled ? "    " : "";

                Debug.Log($"{prefix}{message}", context);
            }
        }
    }
}