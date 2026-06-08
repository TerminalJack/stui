// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

//This project is open source. Anyone can use any part of this code however they wish
//Feel free to use this code in your own projects, or expand on this code
//If you have any improvements to the code itself, please visit
//https://github.com/Dharengo/Spriter2UnityDX and share your suggestions by creating a fork
//-Dengar/Dharengo

using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace Stui.Animations
{
    using Importing;
    using EntityInfo;
    using Stui.Extensions;

    public class AnimationBuilder : UnityEngine.Object
    {
        //Only one of these is made for each Entity, and these globals are the same for every
        //Animation that belongs to these entities
        private ScmlProcessingInfo ProcessingInfo;
        private const float inf = float.PositiveInfinity;
        private IDictionary<int, IDictionary<int, Sprite>> Folders;
        private IDictionary<string, Transform> Transforms;
        private string PrefabPath;
        private string AnimationsPath;
        private Transform Root;
        private IDictionary<string, AnimationClip> OriginalClips = new Dictionary<string, AnimationClip>();
        private IDictionary<string, SpatialInfo> DefaultBones;
        private IDictionary<string, SpriteInfo> DefaultSprites;
        private IDictionary<string, SpatialInfo> DefaultActionPoints;
        private IDictionary<string, SpriteInfo> DefaultCollisionRectangles;
        private AnimatorController Controller;
        private bool ModdedController = false;
        private SpriterEntityInfo entityInfo;

        public AnimationBuilder(ScmlProcessingInfo info, IDictionary<int, IDictionary<int, Sprite>> folders,
                                 IDictionary<string, Transform> transforms, IDictionary<string, SpatialInfo> defaultBones,
                                 IDictionary<string, SpriteInfo> defaultSprites, IDictionary<string, SpatialInfo> defaultActionPoints,
                                 IDictionary<string, SpriteInfo> defaultCollisionRectangles, string prefabPath,
                                 AnimatorController controller, SpriterEntityInfo _entityInfo)
        {
            ProcessingInfo = info;
            Folders = folders;
            Transforms = transforms;
            PrefabPath = prefabPath;
            DefaultBones = defaultBones;
            DefaultSprites = defaultSprites;
            DefaultActionPoints = defaultActionPoints;
            DefaultCollisionRectangles = defaultCollisionRectangles;
            entityInfo = _entityInfo;

            Root = Transforms["rootTransform"];
            Controller = controller;
            AnimationsPath = PrefabPath.Substring(0, PrefabPath.LastIndexOf('.')) + "_Anims";

            foreach (var item in GetOrigClips())
            {
                var clip = item as AnimationClip;
                if (clip != null)
                {
                    OriginalClips[clip.name] = clip;
                }
            }
        }

        public Object[] GetOrigClips()
        {
            ScmlImportOptions.AnimationImportOption importOption = ScmlImportOptions.options != null
                ? ScmlImportOptions.options.importOption
                : ScmlImportOptions.AnimationImportOption.NestedInPrefab;

            switch (importOption)
            {
                case ScmlImportOptions.AnimationImportOption.NestedInPrefab:
                    return AssetDatabase.LoadAllAssetRepresentationsAtPath(PrefabPath);

                case ScmlImportOptions.AnimationImportOption.SeparateFolder:
                    return AssetDatabase.LoadAllAssetsAtPath(AnimationsPath);
            }

            return null;
        }

        public IEnumerator Build(Animation animation, IDictionary<int, Timeline> timelines, IBuildTaskContext buildCtx)
        {
            var clip = new AnimationClip
            {
                name = animation.name,
                frameRate = 1000f
            };

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, creating animation clip '{clip.name}'";

            // This Dictionary will shrink in size for every transform (aka timeline) that is used in the animation.
            var pendingTransforms = new Dictionary<string, Transform>(Transforms);

            foreach (var key in animation.mainlineKeys)
            {
                var processedBoneRefIds = new HashSet<int>();
                var brefs = new Queue<Ref>(key.boneRefs);

                while (brefs.Count > 0)
                {
                    var bref = brefs.Dequeue();

                    if (bref.parentRefId < 0 || processedBoneRefIds.Contains(bref.parentRefId))
                    {
                        var timeline = timelines[bref.timelineId];
                        processedBoneRefIds.Add(bref.id);

                        if (pendingTransforms.TryGetValue(timeline.name, out Transform bone)) // Skip it if it has already been seen.
                        {
                            if (buildCtx.IsCanceled) { yield break; }
                            yield return $"{buildCtx.MessagePrefix}, bone: '{timeline.name}', creating animation curves";

                            SetCurves(bone, DefaultBones[timeline.name], timeline, clip, animation);
                            pendingTransforms.Remove(timeline.name);
                        }
                    }
                    else
                    {
                        brefs.Enqueue(bref);
                    }
                }

                foreach (var objRef in key.objectRefs)
                {
                    var timeline = timelines[objRef.timelineId];

                    if (timeline.objectType == ObjectType.sprite)
                    {
                        Transform spriteTransform;

                        if (pendingTransforms.TryGetValue(timeline.name, out spriteTransform))
                        {
                            if (buildCtx.IsCanceled) { yield break; }
                            yield return $"{buildCtx.MessagePrefix}, sprite: '{timeline.name}', creating animation curves";

                            SetCurves(spriteTransform, DefaultSprites[timeline.name], timeline, clip, animation);
                            CreateSpriteVisibilityCurve(spriteTransform, animation.mainlineKeys, timeline, clip);
                            CreateSortingOrderCurve(spriteTransform, animation.mainlineKeys, timeline, clip);

                            pendingTransforms.Remove(timeline.name);
                        }
                    }
                    else if (timeline.objectType == ObjectType.point)
                    {
                        Transform actionPointTransform;

                        if (pendingTransforms.TryGetValue(timeline.name, out actionPointTransform))
                        {
                            if (buildCtx.IsCanceled) { yield break; }
                            yield return $"{buildCtx.MessagePrefix}, action point: '{timeline.name}', creating animation curves";

                            SetCurves(actionPointTransform, DefaultActionPoints[timeline.name], timeline, clip, animation);
                            pendingTransforms.Remove(timeline.name);
                        }
                    }
                    else if (timeline.objectType == ObjectType.box)
                    {
                        Transform boxTransform;

                        if (pendingTransforms.TryGetValue(timeline.name, out boxTransform))
                        {
                            if (buildCtx.IsCanceled) { yield break; }
                            yield return $"{buildCtx.MessagePrefix}, collision rectangle: '{timeline.name}', creating animation curves";

                            SetCurves(boxTransform, DefaultCollisionRectangles[timeline.name], timeline, clip, animation);
                            CreateColliderEnabledCurve(boxTransform, animation.mainlineKeys, timeline, clip);

                            pendingTransforms.Remove(timeline.name);
                        }
                    }
                }
            }

            if (pendingTransforms.Count > 0)
            {
                if (buildCtx.IsCanceled) { yield break; }
                yield return $"{buildCtx.MessagePrefix}, hiding all sprite renderers and collision rectangles that are not used in this animation";

                yield return null; // Wait for next frame so the display of status messages can catch up.

                foreach (var kvPair in pendingTransforms)
                {   // Hide all of the remaining sprite renderers, if necessary.
                    if (DefaultSprites.ContainsKey(kvPair.Key))
                    {
                        // The SpriteRenderer component is on this game object or a child.
                        var rendererComponent = kvPair.Value.GetComponentInChildren<SpriteRenderer>(includeInactive: true);

                        if (rendererComponent != null && rendererComponent.enabled)
                        {
                            var rendererComponentTransformPath = GetPathToChild(rendererComponent.transform);
                            var binding = EditorCurveBinding.FloatCurve(rendererComponentTransformPath,
                                typeof(SpriteRenderer), "m_Enabled");
                            var curve = new AnimationCurve(new Keyframe(0f, 0f, inf, inf));

                            AnimationUtility.SetEditorCurve(clip, binding, curve);
                        }
                    }

                    // Disable any remaining box colliders, if necessary.
                    if (DefaultCollisionRectangles.ContainsKey(kvPair.Key))
                    {
                        var colliderComponent = kvPair.Value.GetComponent<BoxCollider2D>();

                        if (colliderComponent != null && colliderComponent.enabled)
                        {
                            var colliderComponentTransformPath = GetPathToChild(colliderComponent.transform);
                            var binding = EditorCurveBinding.FloatCurve(colliderComponentTransformPath,
                                typeof(BoxCollider2D), "m_Enabled");
                            var curve = new AnimationCurve(new Keyframe(0f, 0f, inf, inf));

                            AnimationUtility.SetEditorCurve(clip, binding, curve);
                        }
                    }
                }
            }

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, creating animation curves for entity-scoped tags";

            AddEntityScopedTagsToClip(animation, clip);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, creating animation curves for entity-scoped variables";

            AddEntityScopedVariablesToClip(animation, clip);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, creating animation curves for event-scoped tags";

            AddEventScopedTagsToClip(animation, clip);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, creating animation curves for event-scoped variables";

            AddEventScopedVariablesToClip(animation, clip);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, creating animation curves for object-scoped tags";

            AddObjectScopedTagsToClip(animation, clip);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, creating animation curves for object-scoped variables";

            AddObjectScopedVariablesToClip(animation, clip);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, creating animation curve for spatial controller";

            AddSpatialControllerCurveToClip(animation, clip);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, configuring animation clip";

            yield return null; // Wait for next frame so the display of status messages can catch up.

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.stopTime = animation.length; //Set the animation's length and other settings

            if (animation.looping)
            {
                clip.wrapMode = WrapMode.Loop;
                settings.loopTime = true;
            }
            else
            {
                clip.wrapMode = WrapMode.ClampForever;
            }

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, setting animation clip settings";

            yield return null; // Wait for next frame so the display of status messages can catch up.

            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (OriginalClips.ContainsKey(animation.name))
            {   // If the clip already exists, copy this clip into the old one
                if (buildCtx.IsCanceled) { yield break; }
                yield return $"{buildCtx.MessagePrefix}, overwriting animation clip";

                yield return null; // Wait for next frame so the display of status messages can catch up.

                var oldClip = OriginalClips[animation.name];
                var cachedEvents = AnimationUtility.GetAnimationEvents(oldClip).ToList();

                // Remove the events that the importer generates and manages.
                cachedEvents.RemoveAll(e => e.functionName == nameof(SoundController.SoundController_PlaySound));
                cachedEvents.RemoveAll(e => e.functionName == nameof(EventController.EventController_HandleEvent));

                EditorUtility.CopySerialized(clip, oldClip);
                clip = oldClip;

                AnimationUtility.SetAnimationEvents(clip, cachedEvents.ToArray());
                ProcessingInfo.ModifiedAnims.Add(clip);
            }
            else
            {
                ScmlImportOptions.AnimationImportOption importOption = ScmlImportOptions.options != null
                    ? ScmlImportOptions.options.importOption
                    : ScmlImportOptions.AnimationImportOption.NestedInPrefab;

                switch (importOption)
                {
                    case ScmlImportOptions.AnimationImportOption.NestedInPrefab:
                        if (buildCtx.IsCanceled) { yield break; }
                        yield return $"{buildCtx.MessagePrefix}, adding animation clip to prefab";

                        yield return null; // Wait for next frame so the display of status messages can catch up.

                        AssetDatabase.AddObjectToAsset(clip, PrefabPath);
                        break;

                    case ScmlImportOptions.AnimationImportOption.SeparateFolder:
                        if (!AssetDatabase.IsValidFolder(AnimationsPath))
                        {
                            var splitIndex = AnimationsPath.LastIndexOf('/');
                            var path = AnimationsPath.Substring(0, splitIndex);
                            var newFolder = AnimationsPath.Substring(splitIndex + 1);
                            AssetDatabase.CreateFolder(path, newFolder);
                        }

                        var animPath = string.Format("{0}/{1}.anim", AnimationsPath, clip.name);

                        if (buildCtx.IsCanceled) { yield break; }
                        yield return $"{buildCtx.MessagePrefix}, writing animation clip to file '{animPath}'";

                        yield return null; // Wait for next frame so the display of status messages can catch up.

                        AssetDatabase.CreateAsset(clip, animPath);
                        break;
                }

                ProcessingInfo.NewAnims.Add(clip);
            }

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, creating animation events for sounds";

            AddSoundEventsToClip(animation, clip);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, adding Spriter events to clip";

            AddSpriterEventsToClip(animation, clip);

            var totalKeyCount = GetTotalKeyCount(clip);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}, total key count for animation: {totalKeyCount}";

            if (!ArrayUtility.Contains(Controller.animationClips, clip))
            {   // Don't add the clip if it's already there
                if (buildCtx.IsCanceled) { yield break; }
                yield return $"{buildCtx.MessagePrefix}, adding/replacing animation state to animator controller";

                var state = GetStateFromController(clip.name); // Find a state of the same name

                if (state != null)
                {
                    state.motion = clip; // If it exists, replace it
                }
                else
                {   // Otherwise add it as a new state.
                    Controller.AddMotion(clip);
                }

                if (!ModdedController)
                {
                    if (!ProcessingInfo.NewControllers.Contains(Controller) && !ProcessingInfo.ModifiedControllers.Contains(Controller))
                    {
                        ProcessingInfo.ModifiedControllers.Add(Controller);
                    }

                    ModdedController = true;
                }

                EditorUtility.SetDirty(Controller);
            }

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(clip));
        }

        private static int GetTotalKeyCount(AnimationClip clip)
        {
            if (clip == null)
            {
                return 0;
            }

            int total = 0;

            // Float curves (position, rotation, scale, colors, ints-as-floats, etc.)
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve != null)
                {
                    total += curve.keys.Length;
                }
            }

            // Object reference curves (sprites, materials, etc.)
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys != null)
                {
                    total += keys.Length;
                }
            }

            return total;
        }

        private void AddSoundEventsToClip(Animation animation, AnimationClip clip)
        {
            // Add any animation events for Spriter sounds to the clip.
            if (Root.TryGetComponent<SoundController>(out var soundController))
            {
                var animEvents = AnimationUtility.GetAnimationEvents(clip).ToList();

                foreach (var soundItem in entityInfo.soundItems.Where(si => si.animationName == animation.name))
                {
                    int soundIdx = soundController.SoundItems.IndexOf(soundItem);
                    if (soundIdx < 0)
                    {
                        Debug.LogWarning("A sound item wasn't found in the SoundController.soundItems list.  The " +
                            $"sound item details are: soundlineName: {soundItem.soundlineName}, animationName: " +
                            $"{soundItem.animationName}, time: {soundItem.time}");
                    }
                    else
                    {
                        var soundEvent = new AnimationEvent
                        {
                            functionName = nameof(SoundController.SoundController_PlaySound),
                            time = soundItem.time,
                            intParameter = soundIdx
                        };

                        animEvents.Add(soundEvent);
                    }
                }

                AnimationUtility.SetAnimationEvents(clip, animEvents.ToArray());
            }
        }

        private void AddSpriterEventsToClip(Animation animation, AnimationClip clip)
        {
            if (animation.eventlines.Count > 0)
            {
                var animEvents = AnimationUtility.GetAnimationEvents(clip).ToList();

                foreach (var eventline in animation.eventlines)
                {
                    foreach (var key in eventline.keys)
                    {
                        var spriterEvent = new AnimationEvent
                        {
                            functionName = nameof(EventController.EventController_HandleEvent),
                            time = key.time,
                            stringParameter = eventline.name
                        };

                        animEvents.Add(spriterEvent);
                    }
                }

                AnimationUtility.SetAnimationEvents(clip, animEvents.ToArray());
            }
        }

        private void CreateTagCurve(AnimationCurve tagCurve, Animation animation, List<TaglineKey> taglineKeys, TagDef tagDef)
        {
            List<AnimationCurve> allCurves = new List<AnimationCurve>();

            var keys = taglineKeys.ToList();

            // If a key for time 0 doesn't exist then create one.  (Tag will be inactive at time 0.)
            if (keys[0].time_s != 0f)
            {
                keys.Insert(0, new TaglineKey() { time_s = 0f, tags = new List<TagInfo>() });
            }

            for (int i = 0; i < keys.Count; ++i)
            {
                var key = keys[i];

                if (key.time_s >= animation.length)
                {   // This key is on the last frame of the animation.  A key will have already been
                    // created for it below.
                    break;
                }

                float startTime = key.time_s;
                float startValue = key.tags.Exists(t => t.tagDefId == tagDef.id) ? 1f : 0f;

                // Skip any keys that have the same value as startValue.
                int nextKeyIdx = i + 1;

                for ( ; nextKeyIdx < keys.Count; ++nextKeyIdx)
                {
                    float nextKeyValue = keys[nextKeyIdx].tags.Exists(t => t.tagDefId == tagDef.id) ? 1f : 0f;
                    if (nextKeyValue != startValue)
                    {
                        break;
                    }
                }

                var nextKey = (nextKeyIdx < keys.Count) ? keys[nextKeyIdx] : null;

                float endTime = nextKey != null ? nextKey.time_s : animation.length;

                float endValue = nextKey != null
                    ? nextKey.tags.Exists(t => t.tagDefId == tagDef.id) ? 1f : 0f
                    : startValue;

                allCurves.Add(CreateCurve(CurveType.instant, startTime, endTime, startValue, endValue));

                i = nextKeyIdx - 1; // Account for any skipped keys.
            }

            CurveBuilder.ConcatenateCurvesInto(tagCurve, allCurves.ToArray());
            CurveBuilder.RemoveRedundantKeys(tagCurve);
        }

        private void CreateTagCurves(Animation animation, AnimationClip clip, List<TagInstanceInfo> tagInstanceInfos, Metadata metadata)
        {
            foreach (var tagInstanceInfo in tagInstanceInfos)
            {
                var tagDef = tagInstanceInfo.tagDef;

                // Do we need to create an animation curve for this tag?  A curve will need to be created in either of
                // the following cases:
                //
                //   1) The tag is used in this animation.  That is, it appears at least once in the tagline.
                //   2) The tag's bind pose value is true.  (That is, the tag is active on the first frame of the first
                //      animation.)  If this is the case then we need to override that and create a curve where it is
                //      false (inactive) for the entire animation.

                bool tagIsUsed = metadata?.taglineKeys?.SelectMany(k => k.tags).Any(t => t.tagDefId == tagDef.id) ?? false;
                bool needCurve = tagInstanceInfo.bindPoseValue || tagIsUsed;

                if (needCurve)
                {
                    AnimationCurve tagCurve = new AnimationCurve();

                    if (tagIsUsed)
                    {
                        CreateTagCurve(tagCurve, animation, metadata.taglineKeys, tagDef);
                    }
                    else
                    {
                        tagCurve.AddKey(new Keyframe(0f, 0f)); // Value of 0 at time 0.
                    }

                    if (tagInstanceInfo.gameObject.TryGetComponent<SpriterTag>(out var spriterTagComponent))
                    {
                        var spriterTagTransform = spriterTagComponent.transform;
                        var spriterTagTransformPath = GetPathToChild(spriterTagTransform);
                        var spriterTagBinding = EditorCurveBinding.FloatCurve(spriterTagTransformPath,
                            typeof(SpriterTag), nameof(SpriterTag.IsActive));

                        AnimationUtility.SetEditorCurve(clip, spriterTagBinding, tagCurve);
                    }
                    else
                    {
                        Debug.LogWarning("A Spriter Tag component wasn't found on the " +
                            $"'{tagInstanceInfo.gameObject.name}' game object.");
                    }
                }
            }
        }

        private float GetSpriterVariableValueAsFloat(VarDef varDef, string valueAsString)
        {
            float result = 0f;

            switch (varDef.type)
            {
                case VarType.Float:
                    result = float.TryParse(valueAsString, out var f) ? f : 0f;
                    break;

                case VarType.Int:
                    result = int.TryParse(valueAsString, out var i) ? i : 0;
                    break;

                case VarType.String:
                    result = varDef.possibleStringValues.FindIndex(s => s == valueAsString) is var idx && idx >= 0 ? idx : 0;
                    break;

                default:
                    break;
            }

            return result;
        }

        private void CreateVariableCurve(AnimationCurve varCurve, Animation animation, List<VarlineKey> varlineKeys, VarDef varDef)
        {
            List<AnimationCurve> allCurves = new List<AnimationCurve>();

            var keys = varlineKeys.ToList();

            // If a key for time 0 doesn't exist then create one.  (Variable will have its default value.)
            if (keys[0].time_s != 0f)
            {
                keys.Insert(0, new VarlineKey() { time_s = 0f, curve_type = CurveType.instant, value = varDef.defaultValue });
            }

            // ! If the variable type is int then adjust the key timing so that the curve matches Spriter's.  Spriter
            // ! does truncation whereas Unity does rounding when converting from float to int.

            for (int i = 0; i < keys.Count; ++i)
            {
                var key = keys[i];

                if (key.time_s >= animation.length)
                {   // This key is on the last frame of the animation.  A key will have already been
                    // created for it below.
                    break;
                }

                float startTime = key.time_s;
                float startValue = GetSpriterVariableValueAsFloat(varDef, key.value);

                var nextKey = (i + 1 < keys.Count) ? keys[i + 1] : null;

                float endTime = nextKey != null ? nextKey.time_s : animation.length;

                float endValue = nextKey != null
                    ? GetSpriterVariableValueAsFloat(varDef, nextKey.value)
                    : startValue;

                CurveType curve_type = varDef.type == VarType.String
                    ? CurveType.instant
                    : key.curve_type;

                allCurves.Add(CreateCurve(curve_type, startTime, endTime, startValue, endValue, key.c1, key.c2, key.c3, key.c4));
            }

            CurveBuilder.ConcatenateCurvesInto(varCurve, allCurves.ToArray());
            CurveBuilder.RemoveRedundantKeys(varCurve);
        }

        private EditorCurveBinding GetSpriterVarComponentBinding<T>(GameObject gameObject, string propertyName) where T : MonoBehaviour
        {
            if (gameObject.TryGetComponent<T>(out var spriterVarComponent ))
            {
                var spriterVarTransform = spriterVarComponent.transform;
                var spriterVarTransformPath = GetPathToChild(spriterVarTransform);

                var spriterVarBinding = EditorCurveBinding.FloatCurve(spriterVarTransformPath, typeof(T), propertyName);

                return spriterVarBinding;
            }
            else
            {
                throw new Exception($"A Spriter Variable component of type {nameof(T)} wasn't found on the " +
                            $"'{gameObject.name}' game object.");
            }
        }

        private void CreateVariableCurves(Animation animation, AnimationClip clip, List<VarInstanceInfo> varInstanceInfos, Metadata metadata)
        {
            foreach (var varInstanceInfo in varInstanceInfos)
            {
                var varDef = varInstanceInfo.varDef;

                // Do we need to create an animation curve for this variable?  A curve will need to be created in either
                // of the following cases:
                //
                //   1) The variable is used in this animation.  That is, it has a varline.
                //   2) The variable's bind pose value is different than the variable's default value.  If this is the
                //      case then we need to override Unity's bind pose behavior and create a curve where the variabl is
                //      has its default value for the entire animation.

                var varline = metadata?.varlines.FirstOrDefault(vl => vl.varDefId == varDef.id);
                bool needCurve = varline != null || varInstanceInfo.bindPoseValue != varDef.defaultValue;

                if (needCurve)
                {
                    AnimationCurve varCurve = new AnimationCurve();

                    if (varline != null)
                    {
                        CreateVariableCurve(varCurve, animation, varline.keys, varDef);
                    }
                    else
                    {
                        var varValueAsFloat = GetSpriterVariableValueAsFloat(varDef, varDef.defaultValue);
                        varCurve.AddKey(new Keyframe(0f, varValueAsFloat)); // Variable's default value at time 0.
                    }

                    try
                    {
                        EditorCurveBinding varComponentBinding = new EditorCurveBinding();

                        switch (varDef.type)
                        {
                            case VarType.Float:
                                varComponentBinding = GetSpriterVarComponentBinding<SpriterFloat>(
                                    varInstanceInfo.gameObject,
                                    nameof(SpriterFloat.Value));
                                break;

                            case VarType.Int:
                                varComponentBinding = GetSpriterVarComponentBinding<SpriterInt>(
                                    varInstanceInfo.gameObject,
                                    nameof(SpriterInt.Value));
                                break;

                            case VarType.String:
                                varComponentBinding = GetSpriterVarComponentBinding<SpriterString>(
                                    varInstanceInfo.gameObject,
                                    nameof(SpriterString.ValueIndex));
                                break;

                            default:
                                break;
                        }

                        AnimationUtility.SetEditorCurve(clip, varComponentBinding, varCurve);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(ex.Message);
                    }
                }
            }
        }

        private void AddEntityScopedTagsToClip(Animation animation, AnimationClip clip)
        {
            CreateTagCurves(animation, clip, entityInfo.tagInstanceInfos.Values.ToList(), animation.metadata);
        }

        private void AddEntityScopedVariablesToClip(Animation animation, AnimationClip clip)
        {
            CreateVariableCurves(animation, clip, entityInfo.varInstanceInfos.Values.ToList(), animation.metadata);
        }

        private void AddEventScopedTagsToClip(Animation animation, AnimationClip clip)
        {
            var allEventInfosWithTags =
                entityInfo.objectInfos.Values
                .Where(i => i.type == ObjectType.spriterEvent && i.hasTags);

            foreach (var info in allEventInfosWithTags)
            {
                var metadata = animation.eventlines?.FirstOrDefault(el => el.name == info.name)?.metadata;

                CreateTagCurves(animation, clip, info.tagInstanceInfos.Values.ToList(), metadata);
            }
        }

        private void AddEventScopedVariablesToClip(Animation animation, AnimationClip clip)
        {
            var allEventInfosWithVars =
                entityInfo.objectInfos.Values
                .Where(i => i.type == ObjectType.spriterEvent && i.hasVariables);

            foreach (var info in allEventInfosWithVars)
            {
                var metadata = animation.eventlines?.FirstOrDefault(el => el.name == info.name)?.metadata;

                CreateVariableCurves(animation, clip, info.varInstanceInfos.Values.ToList(), metadata);
            }
        }

        private void AddObjectScopedTagsToClip(Animation animation, AnimationClip clip)
        {
            var allNonEventInfosWithTags =
                entityInfo.boneInfos.Values.Cast<SpriterInfoBase>()
                .Concat(entityInfo.objectInfos.Values)
                .Where(i => i.type != ObjectType.spriterEvent && i.hasTags);

            foreach (var info in allNonEventInfosWithTags)
            {
                var metadata = animation.timelines.FirstOrDefault(tl => tl.name == info.name && tl.objectType == info.type)?.metadata;

                CreateTagCurves(animation, clip, info.tagInstanceInfos.Values.ToList(), metadata);
            }
        }

        private void AddObjectScopedVariablesToClip(Animation animation, AnimationClip clip)
        {
            var allNonEventInfosWithVars =
                entityInfo.boneInfos.Values.Cast<SpriterInfoBase>()
                .Concat(entityInfo.objectInfos.Values)
                .Where(i => i.type != ObjectType.spriterEvent && i.hasVariables);

            foreach (var info in allNonEventInfosWithVars)
            {
                var metadata = animation.timelines.FirstOrDefault(tl => tl.name == info.name && tl.objectType == info.type)?.metadata;

                CreateVariableCurves(animation, clip, info.varInstanceInfos.Values.ToList(), metadata);
            }
        }

        private void AddSpatialControllerCurveToClip(Animation animation, AnimationClip clip)
        {
            if (Root.TryGetComponent<SpatialController>(out var spatialController))
            {
                bool animUsesSpriterScaling = !animation.usesBakedSpatialData;
                bool useSpriterScalingBindPoseValue = spatialController.UseSpriterScaling;

                if (animUsesSpriterScaling != useSpriterScalingBindPoseValue)
                {
                    var spatialControllerTransformPath = GetPathToChild(Root);

                    var spatialControllerBinding = EditorCurveBinding.FloatCurve(spatialControllerTransformPath,
                        typeof(SpatialController), nameof(SpatialController.UseSpriterScaling));

                    float keyedValue = animUsesSpriterScaling ? 1f : 0f;

                    AnimationUtility.SetEditorCurve(clip, spatialControllerBinding,
                        CurveBuilder.CreateLinearCurve(0f, animation.length, keyedValue, keyedValue));
                }
            }
        }

        private void SetCurves(Transform child, SpatialInfo defaultInfo, Timeline timeline, AnimationClip clip, Animation animation)
        {
            var childPath = GetPathToChild(child);

            foreach (var kvPair in GetCurves(animation, timeline, defaultInfo, child))
            {   // Makes sure that curves are only added for properties that actually mutate in the animation
                switch (kvPair.Key)
                {
                    case ChangedValues.PositionX:
                        CreatePositionXCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.PositionY:
                        CreatePositionYCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.PositionZ:
                        CreatePositionZCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.SpatialPositionX:
                        CreateSpatialPositionXCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.SpatialPositionY:
                        CreateSpatialPositionYCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.ScaleX:
                        CreateScaleXCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.ScaleY:
                        CreateScaleYCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.ScaleZ:
                        CreateScaleZCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.SpatialScaleX:
                        CreateSpatialScaleXCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.SpatialScaleY:
                        CreateSpatialScaleYCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.RawScaleX:
                        CreateRawScaleXCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.RawScaleY:
                        CreateRawScaleYCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.RotationZ:
                        CreateRotationZCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.PivotX:
                        CreatePivotXCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.PivotY:
                        CreatePivotYCurve(childPath, timeline, clip, animation, curve: kvPair.Value);
                        break;

                    case ChangedValues.BoxPivotX:
                        CreateBoxPivotXCurve(childPath, timeline, clip, animation, curve: kvPair.Value, child);
                        break;

                    case ChangedValues.BoxPivotY:
                        CreateBoxPivotYCurve(childPath, timeline, clip, animation, curve: kvPair.Value, child);
                        break;

                    case ChangedValues.VirtualParent:
                        CreateVirtualParentCurve(childPath, timeline, clip, animation, curve: kvPair.Value, child);
                        break;

                    case ChangedValues.Alpha:
                        CreateAlphaCurve(childPath, timeline, clip, animation, curve: kvPair.Value, child);
                        break;

                    case ChangedValues.Sprite:
                        CreateSpriteCurve(childPath, timeline, clip, animation, curve: kvPair.Value, child, defaultInfo);
                        break;
                }
            }

            clip.EnsureQuaternionContinuity();
        }

        private void CreatePositionXCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            SetKeys<SpatialInfo>(curve, timeline, x => x.x, animation);
            var positionXBinding = EditorCurveBinding.FloatCurve(childPath, typeof(Transform), "m_LocalPosition.x");
            AnimationUtility.SetEditorCurve(clip, positionXBinding, curve);
        }

        private void CreatePositionYCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            SetKeys<SpatialInfo>(curve, timeline, x => x.y, animation);
            var positionYBinding = EditorCurveBinding.FloatCurve(childPath, typeof(Transform), "m_LocalPosition.y");
            AnimationUtility.SetEditorCurve(clip, positionYBinding, curve);
        }

        private void CreatePositionZCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            var positionZBinding = EditorCurveBinding.FloatCurve(childPath, typeof(Transform), "m_LocalPosition.z");
            AnimationUtility.SetEditorCurve(clip, positionZBinding, CurveBuilder.CreateLinearCurve(0f, animation.length, 0f, 0f));
        }

        private void CreateSpatialPositionXCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            SetKeys<SpatialInfo>(curve, timeline, x => x.x, animation);
            var positionXBinding = EditorCurveBinding.FloatCurve(childPath, typeof(SpatialAdapter),
                $"{nameof(SpatialAdapter.Position)}.{nameof(SpatialAdapter.Position.x)}");
            AnimationUtility.SetEditorCurve(clip, positionXBinding, curve);
        }

        private void CreateSpatialPositionYCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            SetKeys<SpatialInfo>(curve, timeline, x => x.y, animation);
            var positionYBinding = EditorCurveBinding.FloatCurve(childPath, typeof(SpatialAdapter),
                $"{nameof(SpatialAdapter.Position)}.{nameof(SpatialAdapter.Position.y)}");
            AnimationUtility.SetEditorCurve(clip, positionYBinding, curve);
        }

        private void CreateScaleXCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            SetKeys<SpatialInfo>(curve, timeline, x => x.scale_x, animation);
            var scaleXBinding = EditorCurveBinding.FloatCurve(childPath, typeof(Transform), "m_LocalScale.x");
            AnimationUtility.SetEditorCurve(clip, scaleXBinding, curve);
        }

        private void CreateScaleYCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            SetKeys<SpatialInfo>(curve, timeline, x => x.scale_y, animation);
            var scaleYBinding = EditorCurveBinding.FloatCurve(childPath, typeof(Transform), "m_LocalScale.y");
            AnimationUtility.SetEditorCurve(clip, scaleYBinding, curve);
        }

        private void CreateScaleZCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            curve.AddKey(0f, 1f);
            var scaleZBinding = EditorCurveBinding.FloatCurve(childPath, typeof(Transform), "m_LocalScale.z");
            AnimationUtility.SetEditorCurve(clip, scaleZBinding, curve);
        }

        private void CreateSpatialScaleXCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            SetKeys<SpatialInfo>(curve, timeline, x => x.scale_x, animation);
            var scaleXBinding = EditorCurveBinding.FloatCurve(childPath, typeof(SpatialAdapter),
                $"{nameof(SpatialAdapter.Scale)}.{nameof(SpatialAdapter.Scale.x)}");
            AnimationUtility.SetEditorCurve(clip, scaleXBinding, curve);
        }

        private void CreateSpatialScaleYCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            SetKeys<SpatialInfo>(curve, timeline, x => x.scale_y, animation);
            var scaleYBinding = EditorCurveBinding.FloatCurve(childPath, typeof(SpatialAdapter),
                $"{nameof(SpatialAdapter.Scale)}.{nameof(SpatialAdapter.Scale.y)}");
            AnimationUtility.SetEditorCurve(clip, scaleYBinding, curve);
        }

        private void CreateRawScaleXCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            SetKeys<SpatialInfo>(curve, timeline, x => x.rawScaleX, animation);
            var rawScaleXBinding = EditorCurveBinding.FloatCurve(childPath, typeof(ScaleTracker),
                $"{nameof(ScaleTracker.RawScale)}.{nameof(ScaleTracker.RawScale.x)}");
            AnimationUtility.SetEditorCurve(clip, rawScaleXBinding, curve);
        }

        private void CreateRawScaleYCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            SetKeys<SpatialInfo>(curve, timeline, x => x.rawScaleY, animation);
            var rawScaleYBinding = EditorCurveBinding.FloatCurve(childPath, typeof(ScaleTracker),
                $"{nameof(ScaleTracker.RawScale)}.{nameof(ScaleTracker.RawScale.y)}");
            AnimationUtility.SetEditorCurve(clip, rawScaleYBinding, curve);
        }

        private void CreateRotationZCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            var rotationXBinding = EditorCurveBinding.FloatCurve(childPath, typeof(Transform), "localEulerAnglesRaw.x");
            AnimationUtility.SetEditorCurve(clip, rotationXBinding, CurveBuilder.CreateLinearCurve(0f, animation.length, 0f, 0f));

            var rotationYBinding = EditorCurveBinding.FloatCurve(childPath, typeof(Transform), "localEulerAnglesRaw.y");
            AnimationUtility.SetEditorCurve(clip, rotationYBinding, CurveBuilder.CreateLinearCurve(0f, animation.length, 0f, 0f));

            bool tempFinalKeyCreated = ConvertTimelineAngles(timeline, animation);

            SetKeys<SpatialInfo>(curve, timeline, x => x.angle, animation);

            if (tempFinalKeyCreated)
            {
                timeline.keys.RemoveAt(timeline.keys.Count - 1);
            }

            var rotationZBinding = EditorCurveBinding.FloatCurve(childPath, typeof(Transform), "localEulerAnglesRaw.z");
            AnimationUtility.SetEditorCurve(clip, rotationZBinding, curve);
        }

        private void CreatePivotXCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            SetKeys<SpriteInfo>(curve, timeline, x => x.pivot_x, animation, mainlineBlending: false, CurveType.instant);
            var pivotXPropName = $"{nameof(DynamicPivot2D.Pivot)}.{nameof(DynamicPivot2D.Pivot.x)}";
            var pivotXBinding = EditorCurveBinding.FloatCurve(childPath, typeof(DynamicPivot2D), pivotXPropName);
            AnimationUtility.SetEditorCurve(clip, pivotXBinding, curve);
        }

        private void CreatePivotYCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve)
        {
            SetKeys<SpriteInfo>(curve, timeline, x => x.pivot_y, animation, mainlineBlending: false, CurveType.instant);
            var pivotYPropName = $"{nameof(DynamicPivot2D.Pivot)}.{nameof(DynamicPivot2D.Pivot.y)}";
            var pivotYBinding = EditorCurveBinding.FloatCurve(childPath, typeof(DynamicPivot2D), pivotYPropName);
            AnimationUtility.SetEditorCurve(clip, pivotYBinding, curve);
        }

        private void CreateBoxPivotXCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve, Transform child)
        {
            if (child.TryGetComponent<BoxCollider2D>(out var boxCollider2D))
            {
                var colliderWidth = boxCollider2D.size.x;
                Func<SpriteInfo, float> calcPivotX = boxInfo => (0.5f - boxInfo.pivot_x) * colliderWidth;

                SetKeys<SpriteInfo>(curve, timeline, calcPivotX, animation, mainlineBlending: false, CurveType.instant);
                var pivotXBinding = EditorCurveBinding.FloatCurve(childPath, typeof(BoxCollider2D), "m_Offset.x");
                AnimationUtility.SetEditorCurve(clip, pivotXBinding, curve);
            }
        }

        private void CreateBoxPivotYCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve, Transform child)
        {
            if (child.TryGetComponent<BoxCollider2D>(out var boxCollider2D))
            {
                var colliderHeight = boxCollider2D.size.y;
                Func<SpriteInfo, float> calcPivotY = boxInfo => (0.5f - boxInfo.pivot_y) * colliderHeight;

                SetKeys<SpriteInfo>(curve, timeline, calcPivotY, animation, mainlineBlending: false, CurveType.instant);
                var pivotYBinding = EditorCurveBinding.FloatCurve(childPath, typeof(BoxCollider2D), "m_Offset.y");
                AnimationUtility.SetEditorCurve(clip, pivotYBinding, curve);
            }
        }

        private void CreateVirtualParentCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve, Transform child)
        {
            SetVirtualParentKeys(curve, timeline, x => x.parentBoneName, animation, child.name);

            // The VirtualParent is on this game object's parent.
            if (child.parent.TryGetComponent<VirtualParent>(out var virtualParentComponent))
            {
                var virtualParentTransform = virtualParentComponent.transform;
                var virtualParentTransformPath = GetPathToChild(virtualParentTransform);
                var virtualParentBinding = EditorCurveBinding.FloatCurve(virtualParentTransformPath,
                    typeof(VirtualParent), nameof(VirtualParent.ParentIndex));

                AnimationUtility.SetEditorCurve(clip, virtualParentBinding, curve);
            }
            else
            {
                Debug.LogWarning($"A Virtual Parent component wasn't found for '{timeline.name}' " +
                    $"on the '{child.parent.gameObject.name}' game object");
            }
        }

        private void CreateAlphaCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve, Transform child)
        {
            SetKeys<SpatialInfo>(curve, timeline, x => x.a, animation);

            if (child.TryGetComponent<AlphaController>(out var alphaController))
            {
                var alphaControllerTransform = alphaController.transform;
                var alphaControllerTransformPath = GetPathToChild(alphaControllerTransform);
                var alphaBinding = EditorCurveBinding.FloatCurve(alphaControllerTransformPath,
                    typeof(AlphaController), nameof(AlphaController.Alpha));

                AnimationUtility.SetEditorCurve(clip, alphaBinding, curve);
            }
            else if (entityInfo.boneInfos.GetOrDefault(timeline.name) == null) // Make sure it's not a bone.
            {
                // The SpriteRenderer is on this game object or a child.
                var rendererTransform = child.GetComponentInChildren<SpriteRenderer>(includeInactive: true).transform;
                var rendererTransformPath = GetPathToChild(rendererTransform);
                var rendererBinding = EditorCurveBinding.FloatCurve(rendererTransformPath, typeof(SpriteRenderer), "m_Color.a");

                AnimationUtility.SetEditorCurve(clip, rendererBinding, curve);
            }
            else
            {
                Debug.LogWarning($"An Alpha Controller component didn't get created for bone '{timeline.name}'");
            }
        }

        private void CreateSpriteCurve(string childPath, Timeline timeline, AnimationClip clip,
            Animation animation, AnimationCurve curve, Transform child, SpatialInfo defaultInfo)
        {
            // The SpriteRenderer is on this game object or a child.
            var rendererTransform = child.GetComponentInChildren<SpriteRenderer>(includeInactive: true).transform;

            if (ScmlImportOptions.options != null && ScmlImportOptions.options.directSpriteSwapping)
            {
                SetSpriteSwapKeys(rendererTransform, timeline, clip, animation);
            }
            else
            {
                TextureController swapper;
                rendererTransform.TryGetComponent(out swapper);

                if (swapper == null)
                {   // Add a Texture Controller if one doesn't already exist
                    swapper = rendererTransform.gameObject.AddComponent<TextureController>();
                    var info = (SpriteInfo)defaultInfo;
                    swapper.Sprites = new[] { Folders[info.folderId][info.fileId] };
                }

                SetKeys(curve, timeline, ref swapper.Sprites, animation);

                var rendererTransformPath = GetPathToChild(rendererTransform);
                var textureControllerBinding = EditorCurveBinding.FloatCurve(rendererTransformPath,
                    typeof(TextureController), nameof(TextureController.DisplayedSprite));

                AnimationUtility.SetEditorCurve(clip, textureControllerBinding, curve);
            }
        }

        private void SetSpriteSwapKeys(Transform child, Timeline timeline, AnimationClip clip, Animation animation)
        {
            // Create ObjectReferenceCurve for swapping sprites. This curve will save data in object form instead of floats like regular AnimationCurve.
            var keyframes = new List<ObjectReferenceKeyframe>();

            foreach (var key in timeline.keys)
            {
                var info = (SpriteInfo)key.info;
                var sprite = Folders[info.folderId][info.fileId];

                keyframes.Add(new ObjectReferenceKeyframe { time = TweakTime(key.time_s), value = sprite });
            }

            var rendererBinding = new EditorCurveBinding { path = GetPathToChild(child), propertyName = "m_Sprite", type = typeof(SpriteRenderer) };
            AnimationUtility.SetObjectReferenceCurve(clip, rendererBinding, keyframes.ToArray());
        }

        private bool ConvertTimelineAngles(Timeline timeline, Animation animation)
        {
            for (int i = 0; i < timeline.keys.Count; ++i)
            {   // Convert angles so that spin is taken into account...
                float destAngle = timeline.keys[i].info.angle;

                if (i > 0)
                {
                    int prevKeyIdx = i - 1; // Spin and previous angle is in the _previous_ key.

                    int spin = timeline.keys[prevKeyIdx].spin;

                    float prevAngle = timeline.keys[prevKeyIdx].info.angle;
                    float resultAngle = RotateAngle(prevAngle, destAngle, spin < 0);

                    timeline.keys[i].info.angle = resultAngle;
                }
            }

            if (timeline.keys[timeline.keys.Count - 1].time_s != animation.length && !WillNeedWrapAroundCurves(animation, timeline))
            {   // Create a temporary key for the last frame with the appropriate value.  This temporary key will need
                // to be removed once the rotation curve is created.  It will interfere with the other curves otherwise.
                float prevAngle = timeline.keys[timeline.keys.Count - 1].info.angle;
                float destAngle = GetFinalFrameInferredKeyValue<SpatialInfo>(timeline, x => x.angle, animation);
                float resultAngle = RotateAngle(prevAngle, destAngle, false, shortest: true);

                var newKey = timeline.keys[timeline.keys.Count - 1].Clone();
                newKey.time_s = animation.length;
                newKey.info.angle = resultAngle;

                timeline.keys.Add(newKey);

                return true;
            }

            return false;
        }

        private bool WillNeedWrapAroundCurves(Animation animation, Timeline timeline)
        {
            // If an animation is looping but there isn't a key in the timeline at time 0 then animation curves will
            // need to be created at the end of the timeline and beginning of the timeline.  The curves will join the
            // last key of the timeline with the first with the last timeline key controlling the curve type.

            bool result =
                animation.looping
                && timeline.keys.Count > 1
                && timeline.keys[0].time_s != 0f
                && (
                    timeline.objectType == ObjectType.bone
                        ? animation.mainlineKeys[0].boneRefs.Exists(k => k.timelineId == timeline.id)
                        : animation.mainlineKeys[0].objectRefs.Exists(k => k.timelineId == timeline.id)
                );

            return result;
        }

        /// <summary>
        /// Rotate from currentAngle toward destinationAngle, strictly along the
        /// chosen direction and return the resulting absolute angle (may be
        /// less than 0 or greater than 360.)
        /// </summary>
        /// <param name="currentAngle">Starting angle in degrees.</param>
        /// <param name="destinationAngle">Target angle in degrees.</param>
        /// <param name="clockwise">
        /// If true, enforces a clockwise (negative) sweep; otherwise enforces
        /// counter-clockwise (positive).
        /// </param>
        /// <param name="shortest">
        /// Disregard the clockwise argument and return shortest angle.
        /// </param>
        /// <returns>
        /// currentAngle + signedDelta, where signedDelta can exceed +/-180 to respect
        /// the required rotation direction.
        /// </returns>
        public static float RotateAngle(float currentAngle, float destinationAngle, bool clockwise, bool shortest = false)
        {
            // Gets shortest signed delta in range (–180, +180]:
            // positive = CCW, negative = CW
            float delta = Mathf.DeltaAngle(currentAngle, destinationAngle);

            // If the two angles are too close, floating point imprecision can cause a full rotation
            // instead of the desired (tiny) change.  Just leave the angle as-is in that case.
            if (Mathf.Abs(delta) < 0.01f)
            {
                return currentAngle;
            }

            if (!shortest)
            {
                if (clockwise && delta > 0f)
                {   // Shortest path is CCW (delta > 0) so go the long CW way.
                    delta -= 360f;
                }
                else if (!clockwise && delta < 0f)
                {   // Shortest path is CW (delta < 0) so go the long CCW way.
                    delta += 360f;
                }
            }

            // Return the absolute target angle (can be negative or > 360).
            return currentAngle + delta;
        }

        private void CreateSpriteVisibilityCurve(Transform child, List<MainlineKey> mlks, Timeline timeline, AnimationClip clip)
        {
            var rendererComponent = child.GetComponentInChildren<SpriteRenderer>(includeInactive: true);
            var enabledBindPoseValue = rendererComponent.enabled;

            var visibilityInfos =
            (
                from mlk in mlks

                // Find the timeline key (if any) that this mainline key references
                let tlk = (
                    from k in timeline.keys
                    where mlk.objectRefs.Any(or =>
                        or.timelineId == timeline.id &&
                        or.timelineKeyId == k.id)
                    select k
                ).FirstOrDefault()

                select new
                {
                    mlk.time_s,
                    isVisible = tlk != null // Is sprite visible at time_s?
                }
            )
            .ToList();

            // There will be one visibilityInfos element for each key in mainline keys.

            bool isVisibleForAllKeys = visibilityInfos.Exists(vi => !vi.isVisible) == false;
            bool isNotVisibleForAllKeys = visibilityInfos.Exists(vi => vi.isVisible) == false;
            bool isMixedVisibility = !isVisibleForAllKeys && !isNotVisibleForAllKeys;

            // A curve needs to be created if the visibility changes or if it doesn't but the visibility is different
            // than the bind pose value.
            bool needCurve = isMixedVisibility || (enabledBindPoseValue ? isNotVisibleForAllKeys : isVisibleForAllKeys);

            if (needCurve)
            {
                var visibilityKeyFrames = new List<Keyframe>();

                for (int i = 0; i < visibilityInfos.Count; ++i)
                {
                    // Filter-out redundant keyframes.
                    if (i == 0 || visibilityInfos[i].isVisible != visibilityInfos[i - 1].isVisible)
                    {
                        visibilityKeyFrames.Add(new Keyframe(TweakTime(visibilityInfos[i].time_s),
                            visibilityInfos[i].isVisible ? 1f : 0f, inf, inf));
                    }
                }

                var curve = new AnimationCurve(visibilityKeyFrames.ToArray());

                var rendererComponentTransformPath = GetPathToChild(rendererComponent.transform);
                var binding = EditorCurveBinding.FloatCurve(rendererComponentTransformPath, typeof(SpriteRenderer),
                    "m_Enabled");

                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
        }

        private void CreateColliderEnabledCurve(Transform child, List<MainlineKey> mlks, Timeline timeline, AnimationClip clip)
        {
            var colliderComponent = child.GetComponent<BoxCollider2D>();
            var colliderEnabledBindPoseValue = colliderComponent.enabled;

            var enabledInfos =
            (
                from mlk in mlks

                // Find the timeline key (if any) that this mainline key references
                let tlk = (
                    from k in timeline.keys
                    where mlk.objectRefs.Any(or =>
                        or.timelineId == timeline.id &&
                        or.timelineKeyId == k.id)
                    select k
                ).FirstOrDefault()

                select new
                {
                    mlk.time_s,
                    isEnabled = tlk != null // Is collision rectable enabled at time_s?
                }
            )
            .ToList();

            // There will be one enabledInfos element for each key in mainline keys.

            bool isEnabledForAllKeys = enabledInfos.Exists(vi => !vi.isEnabled) == false;
            bool isNotEnabledForAllKeys = enabledInfos.Exists(vi => vi.isEnabled) == false;
            bool isMixedEnabled = !isEnabledForAllKeys && !isNotEnabledForAllKeys;

            // A curve needs to be created if the flag changes or if it doesn't but the flag is different than the
            // bind pose value.
            bool needCurve = isMixedEnabled || (colliderEnabledBindPoseValue ? isNotEnabledForAllKeys : isEnabledForAllKeys);

            if (needCurve)
            {
                var enabledKeyFrames = new List<Keyframe>();

                for (int i = 0; i < enabledInfos.Count; ++i)
                {
                    // Filter-out redundant keyframes.
                    if (i == 0 || enabledInfos[i].isEnabled != enabledInfos[i - 1].isEnabled)
                    {
                        enabledKeyFrames.Add(new Keyframe(TweakTime(enabledInfos[i].time_s),
                            enabledInfos[i].isEnabled ? 1f : 0f, inf, inf));
                    }
                }

                var curve = new AnimationCurve(enabledKeyFrames.ToArray());

                var collisionComponentTransformPath = GetPathToChild(colliderComponent.transform);
                var binding = EditorCurveBinding.FloatCurve(collisionComponentTransformPath, typeof(BoxCollider2D),
                    "m_Enabled");

                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
        }

        private class SortingOrderInfo
        {
            public float time_s { get; set; }
            public bool isVisible { get; set; }
            public int sortingOrder { get; set; }
        }

        private void CreateSortingOrderCurve(Transform child, List<MainlineKey> mlks, Timeline timeline, AnimationClip clip)
        {

            var rendererComponent = child.GetComponentInChildren<SpriteRenderer>(includeInactive: true);
            var sortOrderBindPoseValue = rendererComponent.sortingOrder;

            var sortingOrderInfos =
            (
                from mlk in mlks

                // Find the timeline key (if any) that this mainline key references
                let tlk =
                    (from k in timeline.keys
                    where mlk.objectRefs.Any(or =>
                        or.timelineId == timeline.id &&
                        or.timelineKeyId == k.id)
                    select k
                    ).FirstOrDefault()

                // Find the matching objectRef (if any) so we can get z_index
                let oref =
                    mlk.objectRefs.FirstOrDefault(or =>
                        or.timelineId == timeline.id &&
                        or.timelineKeyId == tlk?.id)

                select new SortingOrderInfo
                {
                    time_s = mlk.time_s,
                    isVisible = tlk != null,
                    // Note: sortingOrder doesn't apply if the sprite isn't visible.
                    sortingOrder = tlk != null && oref != null ? Ref.ZIndexToSortingOrder(oref.z_index) : -1
                }
            )
            .ToList();

            // Remove any elements where the sprite isn't visible since a key will not be needed in that case.
            sortingOrderInfos.RemoveAll(i => !i.isVisible);

            bool needCurve = sortingOrderInfos.Exists(i => i.sortingOrder != sortOrderBindPoseValue);

            if (needCurve)
            {
                var sortingOrderFrames = new List<Keyframe>();

                // Make sure the first frame gets keyed at time 0.
                sortingOrderInfos[0].time_s = 0f;

                for (int i = 0; i < sortingOrderInfos.Count; ++i)
                {
                    // Filter-out redundant keyframes.
                    if (i == 0 || sortingOrderInfos[i].sortingOrder != sortingOrderInfos[i - 1].sortingOrder)
                    {
                        sortingOrderFrames.Add(new Keyframe(TweakTime(sortingOrderInfos[i].time_s),
                            sortingOrderInfos[i].sortingOrder, inf, inf));
                    }
                }

                var curve = new AnimationCurve(sortingOrderFrames.ToArray());

                var rendererComponentTransformPath = GetPathToChild(rendererComponent.transform);
                var binding = EditorCurveBinding.FloatCurve(rendererComponentTransformPath, typeof(SpriteRenderer), "m_SortingOrder");

                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
        }

        private void SetKeys<T>(AnimationCurve curve, Timeline timeline, Func<T, float> infoValue,
            Animation animation, bool mainlineBlending = true, CurveType overrideCurveType = CurveType.linear) where T : SpatialInfo
        {
            // Mainline keys that have a curve type other than linear will 'blend' (for lack of a better term) with the
            // timeline keys.  Basically, the mainline key easing curve's output is fed as input to the timeline key's
            // easing curve.
            bool needsMainlineBlending = mainlineBlending
                ? animation.mainlineKeys.FirstOrDefault(x => x.curve_type != CurveType.linear) != null
                : false;

            if (needsMainlineBlending)
            {
                SetKeysWithMainlineBlending(curve, timeline, infoValue, animation);
            }
            else
            {
                DoSetKeys(curve, timeline, infoValue, animation, overrideCurveType);
            }
        }

        private void GenerateWrapAroundCurves<T>(Animation animation, Timeline timeline, Func<T, float> infoValue,
            out AnimationCurve beginningCurve, out AnimationCurve endingCurve) where T : SpatialInfo
        {
            var lastKey = timeline.keys[timeline.keys.Count - 1];
            var firstKey = timeline.keys[0];

            float startTime = lastKey.time_s;
            float endTime = animation.length + firstKey.time_s;

            float startValue = infoValue(lastKey.info as T);
            float endValue = infoValue(firstKey.info as T);

            AnimationCurve joiningCurve = CreateCurve(lastKey.curve_type, startTime, endTime, startValue, endValue,
                lastKey.c1, lastKey.c2, lastKey.c3, lastKey.c4);

            AnimationCurve leftSideCurve;
            AnimationCurve rightSideCurve;

            CurveBuilder.SplitCurve(joiningCurve, animation.length, out leftSideCurve, out rightSideCurve);

            AnimationCurve startCurve = new AnimationCurve();

            // The right-hand side curve will be the first part of the timeline.  Adjust the keyframe times and
            // preserve all of the weights and tangents...
            foreach (var key in rightSideCurve.keys)
            {
                float newTime = key.time - animation.length;

                int index = startCurve.AddKey(newTime, key.value);

                var k = startCurve.keys[index];
                k.inTangent   = key.inTangent;
                k.outTangent  = key.outTangent;
                k.inWeight    = key.inWeight;
                k.outWeight   = key.outWeight;
                k.weightedMode = key.weightedMode;

                startCurve.MoveKey(index, k);
            }

            beginningCurve = startCurve;
            endingCurve = leftSideCurve;
        }

        private void DoSetKeys<T>(AnimationCurve curve, Timeline timeline, Func<T, float> infoValue,
            Animation animation, CurveType overrideCurveType = CurveType.linear) where T : SpatialInfo
        {
            List<AnimationCurve> allCurves = new List<AnimationCurve>();

            for (int i = 0; i < timeline.keys.Count; ++i)
            {   // Create a keyframe for every key on its personal Timeline...
                var key = timeline.keys[i];

                if (key.time_s >= animation.length)
                {   // This key is on the last frame of the animation.  A key will have already been
                    // created for it below.
                    break;
                }

                float startTime = key.time_s;
                float endTime = (i + 1 < timeline.keys.Count) ? timeline.keys[i + 1].time_s : animation.length;

                float startValue = infoValue(key.info as T);

                float endValue = (i + 1 < timeline.keys.Count)
                    ? infoValue(timeline.keys[i + 1].info as T)
                    : GetFinalFrameInferredKeyValue(timeline, infoValue, animation);

                CurveType curve_type = overrideCurveType != CurveType.linear
                    ? overrideCurveType
                    : key.curve_type;

                allCurves.Add(CreateCurve(curve_type, startTime, endTime, startValue, endValue, key.c1, key.c2, key.c3, key.c4));
            }

            if (WillNeedWrapAroundCurves(animation, timeline))
            {
                // A key doesn't exist for time 0 but it should so we will take care of it as well as regenerate the
                // curve for the last key here.  In this case, the animation loops so the curve needs to tween between
                // the last key of the timeline and the first key (which in this case will have a time other than 0.)

                AnimationCurve beginningCurve;
                AnimationCurve endingCurve;

                GenerateWrapAroundCurves(animation, timeline, infoValue, out beginningCurve, out endingCurve);

                allCurves.Insert(0, beginningCurve);
                allCurves[allCurves.Count - 1] = endingCurve;
            }

            CurveBuilder.ConcatenateCurvesInto(curve, allCurves.ToArray());
            CurveBuilder.RemoveRedundantKeys(curve);
        }

        private float GetFinalFrameInferredKeyValue<T>(Timeline timeline, Func<T, float> infoValue,
            Animation animation) where T : SpatialInfo
        {
            // This will return the appropriate value for the the last frame of an animation based on whether 1) there
            // is a time 0 auxiliary key, and if not 2) on whether the animation loops or not.

            var firstKey = timeline.keys[0];
            var lastKey = timeline.keys[timeline.keys.Count - 1];
            var timeZeroAuxKey = firstKey.timeZeroAuxKey;

            float endValue;

            if (timeZeroAuxKey != null)
            {
                endValue = infoValue(timeZeroAuxKey.info as T);
            }
            else if (animation.looping && firstKey.time_s == 0f)
            {
                endValue = infoValue(firstKey.info as T);
            }
            else
            {   // If it turns out that a key needs to be created at time 0 then this value will be adjusted.
                endValue = infoValue(lastKey.info as T);
            }

            return endValue;
        }

        private void SetKeysWithMainlineBlending<T>(AnimationCurve curve, Timeline timeline, Func<T, float> infoValue,
            Animation animation) where T : SpatialInfo
        {
            AnimationCurve timelineCurve = new AnimationCurve();
            DoSetKeys(timelineCurve, timeline, infoValue, animation); // The timeline curve without blending.

            List<AnimationCurve> allCurves = new List<AnimationCurve>();

            for (int i = 0; i < animation.mainlineKeys.Count; ++i)
            {
                var mlk = animation.mainlineKeys[i];
                var nextMlk = i + 1 < animation.mainlineKeys.Count ? animation.mainlineKeys[i + 1] : null;

                if (mlk.time_s >= animation.length)
                {   // This key is on the last frame of the animation.  A key will have already been
                    // created for it below.
                    break;
                }

                // tlk is the timeline key that mlk is blending with.
                var tlk = timeline.keys.LastOrDefault(k => k.time_s <= mlk.time_s); // May be null.

                var curve_type = tlk?.curve_type == CurveType.instant
                    ? CurveType.instant
                    : mlk.curve_type;

                if (curve_type == CurveType.instant || tlk?.curve_type == CurveType.linear)
                {   // An instant curve overrides any blending and if the timeline key's curve type is linear then
                    // the mainline key's curve type overrides it.  No blending is necessary in these cases.

                    float startTime = mlk.time_s;
                    float endTime = nextMlk != null ? nextMlk.time_s : animation.length;

                    float startValue = timelineCurve.Evaluate(TweakTime(startTime));
                    float endValue = timelineCurve.Evaluate(TweakTime(endTime));

                    var mainlineCurve = CreateCurve(curve_type, startTime, endTime, startValue, endValue, mlk.c1, mlk.c2, mlk.c3, mlk.c4);

                    allCurves.Add(mainlineCurve);
                }
                else if (tlk != null)
                {   // Note: The code here will likely be rarely used for real world Spriter animations.

                    // tlk (set above) and nextTlk are the two timeline keys that bracket the mainline key, mlk.  nextTlk may be null.
                    var nextTlk = timeline.keys.FirstOrDefault(k => k.time_s > mlk.time_s);

                    float mlkStartTime = mlk.time_s;
                    float mlkEndTime = nextMlk != null ? nextMlk.time_s : animation.length;

                    float tlkStartTime = tlk.time_s;
                    float tlkEndTime = nextTlk != null ? nextTlk.time_s : animation.length;

                    if (mlk.curve_type == CurveType.linear &&
                        nextTlk != null &&
                        Mathf.Approximately(mlkStartTime, tlkStartTime) &&
                        Mathf.Approximately(mlkEndTime, tlkEndTime))
                    {
                        // The mainline curve type doesn't affect the timeline curve and the timeline has keys at the
                        // same times as the mainline so just create the segment as would be done normally...

                        float startValue = infoValue(tlk.info as T);
                        float endValue = infoValue(nextTlk.info as T);

                        allCurves.Add(CreateCurve(tlk.curve_type, tlkStartTime, tlkEndTime, startValue, endValue,
                            tlk.c1, tlk.c2, tlk.c3, tlk.c4));
                    }
                    else
                    {
                        float tlkStartValue = timelineCurve.Evaluate(TweakTime(tlkStartTime));
                        float tlkEndValue = timelineCurve.Evaluate(TweakTime(tlkEndTime));

                        // These are normalized easing curves for the mainline key and the timeline key.  The mainline
                        // curve's output will be the input to the timeline curve.
                        var mlkCurve = CreateCurve(mlk.curve_type, 0f, 1f, 0f, 1f, mlk.c1, mlk.c2, mlk.c3, mlk.c4);
                        var tlkCurve = CreateCurve(tlk.curve_type, 0f, 1f, 0f, 1f, tlk.c1, tlk.c2, tlk.c3, tlk.c4);

                        float mlkDuration = mlkEndTime - mlkStartTime;
                        float tlkDuration = tlkEndTime - tlkStartTime;

                        float mlkScale = mlkDuration / tlkDuration; // The mlk time span, as a percentage, relative to the tlk span.
                        float mlkScaledOffset = (mlkStartTime - tlkStartTime) / tlkDuration; // Where within the tlk span does the mlk span start.

                        const float samplesPerSecond = 60f;
                        const float minNumSamples = 5f;

                        float dt = 1f / (mlkDuration * samplesPerSecond);
                        dt = Mathf.Min(1f / minNumSamples, dt);

                        List<float> samples = new List<float>();

                        for (float t = 0; t <= 1f; t += dt)
                        {
                            var mlkEasing = mlkCurve.Evaluate(t);
                            var tlkT = mlkScaledOffset + (mlkEasing * mlkScale);
                            var tlkEasingT = tlkCurve.Evaluate(tlkT);

                            float sampledValue = Mathf.Lerp(tlkStartValue, tlkEndValue, tlkEasingT);

                            samples.Add(sampledValue);
                        }

                        // The following is done to make sure there are no problems when concatenating all the curves.
                        samples[0] = timelineCurve.Evaluate(TweakTime(mlkStartTime));
                        samples[samples.Count - 1] = timelineCurve.Evaluate(TweakTime(mlkEndTime));

                        var resultCurve = CurveBuilder.CurveFitter.FromAdaptiveFit(samples, mlkDuration, mlkStartTime, 0.01f);

                        allCurves.Add(resultCurve);
                    }
                }
            }

            CurveBuilder.ConcatenateCurvesInto(curve, allCurves.ToArray());
            CurveBuilder.RemoveRedundantKeys(curve);
        }

        private void SetVirtualParentKeys(AnimationCurve curve, Timeline timeline, Func<SpatialInfo, string> infoValue, Animation animation, string childName)
        {
            List<AnimationCurve> allCurves = new List<AnimationCurve>();

            for (int i = 0; i < timeline.keys.Count; ++i)
            {   // Create a keyframe for every key on its personal Timeline...
                var key = timeline.keys[i];

                if (key.time_s >= animation.length)
                {   // This key is on the last frame of the animation.  A key will have already been
                    // created for it below.
                    break;
                }

                float startTime = key.time_s;
                float endTime = (i + 1 < timeline.keys.Count) ? timeline.keys[i + 1].time_s : animation.length;

                string startParentBoneName = infoValue(key.info); // Parent transform name.

                var firstKey = timeline.keys[0];
                var lastKey = timeline.keys[timeline.keys.Count - 1];
                var timeZeroAuxKey = firstKey.timeZeroAuxKey;

                string endParentBoneName = infoValue(
                    i + 1 < timeline.keys.Count
                        ? timeline.keys[i + 1].info
                        : timeZeroAuxKey?.info
                            ?? (animation.looping && firstKey.time_s == 0f
                                ? firstKey.info
                                : lastKey.info)
                );

                // The parent names will need to be converted to indexes.

                int startValue = (entityInfo.boneInfos?.ContainsKey(childName) == true
                    ? entityInfo.boneInfos[childName]?.parentBoneNames?.IndexOf(startParentBoneName)
                    : (int?)null)
                ?? (entityInfo.objectInfos?.ContainsKey(childName) == true
                    ? entityInfo.objectInfos[childName]?.parentBoneNames?.IndexOf(startParentBoneName)
                    : (int?)null)
                ?? -1;

                int endValue = (entityInfo.boneInfos?.ContainsKey(childName) == true
                        ? entityInfo.boneInfos[childName]?.parentBoneNames?.IndexOf(endParentBoneName)
                        : (int?)null)
                    ?? (entityInfo.objectInfos?.ContainsKey(childName) == true
                        ? entityInfo.objectInfos[childName]?.parentBoneNames?.IndexOf(endParentBoneName)
                        : (int?)null)
                    ?? -1;

                if (startValue >= 0 && endValue >= 0)
                {
                    // Parent changes always occur instantly.
                    allCurves.Add(CreateCurve(CurveType.instant, startTime, endTime, startValue, endValue));
                }
                else
                {
                    Debug.LogWarning("Stui: Cannot find virtual parent's transform index!");
                }
            }

            CurveBuilder.ConcatenateCurvesInto(curve, allCurves.ToArray());
            CurveBuilder.RemoveRedundantKeys(curve);
        }

        private AnimationCurve CreateCurve(CurveType curveType, float startTime, float endTime,
            float startValue, float endValue, float c1 = 0, float c2 = 0, float c3 = 0, float c4 = 0)
        {
            startTime = TweakTime(startTime);
            endTime = TweakTime(endTime);

            switch (curveType)
            {
                case CurveType.instant:
                    return CurveBuilder.CreateInstantCurve(startTime, endTime, startValue, endValue);
                case CurveType.linear:
                    return CurveBuilder.CreateLinearCurve(startTime, endTime, startValue, endValue);
                case CurveType.quadratic:
                    return CurveBuilder.Create1dQuadraticCurve(c1, startTime, endTime, startValue, endValue);
                case CurveType.cubic:
                    return CurveBuilder.Create1dCubicCurve(c1, c2, startTime, endTime, startValue, endValue);
                case CurveType.quartic:
                    return CurveBuilder.Create1dQuarticCurve(c1, c2, c3, startTime, endTime, startValue, endValue);
                case CurveType.quintic:
                    return CurveBuilder.Create1dQuinticCurve(c1, c2, c3, c4, startTime, endTime, startValue, endValue);
                case CurveType.bezier:
                    return CurveBuilder.Create2dCubicBezierCurve(c1, c2, c3, c4, startTime, endTime, startValue, endValue);
                default:
                    return new AnimationCurve();
            }
        }

        private float TweakTime(float t)
        {   // Ensure that the time is actually slightly before the intended time to account for the fact that certain
            // values can't be exactly represented by a 32-bit float.  If this isn't done then some keys happen a frame
            // too late.

            const float adjustSeconds = 0.0001f;

            if (t > adjustSeconds)
            {
                t -= adjustSeconds;
            }

            return t;
        }

        private void SetKeys(AnimationCurve curve, Timeline timeline, ref Sprite[] sprites, Animation animation)
        {
            List<AnimationCurve> allCurves = new List<AnimationCurve>();

            for (int i = 0; i < timeline.keys.Count; ++i)
            {   // Create a keyframe for every key on its personal Timeline...
                var key = timeline.keys[i];
                var info = (SpriteInfo)key.info;

                if (key.time_s >= animation.length)
                {   // This key is on the last frame of the animation.  A key will have already been
                    // created for it below.
                    break;
                }

                var nextKey = (i + 1 < timeline.keys.Count) ? timeline.keys[i + 1] : null;
                var nextInfo = nextKey != null ? (SpriteInfo)nextKey.info : null;

                float startTime = key.time_s;
                float endTime = nextKey != null ? nextKey.time_s : animation.length;

                float startValue = GetIndexOrAdd(ref sprites, Folders[info.folderId][info.fileId]);

                float endValue = nextInfo != null
                    ? GetIndexOrAdd(ref sprites, Folders[nextInfo.folderId][nextInfo.fileId])
                    : startValue;

                allCurves.Add(CreateCurve(CurveType.instant, startTime, endTime, startValue, endValue));
            }

            CurveBuilder.ConcatenateCurvesInto(curve, allCurves.ToArray());
            CurveBuilder.RemoveRedundantKeys(curve);
        }

        private int GetIndexOrAdd(ref Sprite[] sprites, Sprite sprite)
        {
            // If the list already contains the sprite, return index.  Otherwise, add sprite to list , then return index.
            var index = ArrayUtility.IndexOf(sprites, sprite); //If the array already contains the sprite, return index
            if (index < 0)
            {
                ArrayUtility.Add(ref sprites, sprite);
                index = ArrayUtility.IndexOf(sprites, sprite);
            }

            return index;
        }

        private AnimatorState GetStateFromController(string clipName)
        {
            foreach (var layer in Controller.layers)
            {
                var state = GetStateFromMachine(layer.stateMachine, clipName);
                if (state != null)
                {
                    return state;
                }
            }

            return null;
        }

        private AnimatorState GetStateFromMachine(AnimatorStateMachine machine, string clipName)
        {
            foreach (var state in machine.states)
            {
                if (state.state.name == clipName)
                {
                    return state.state;
                }
            }

            foreach (var cmachine in machine.stateMachines)
            {
                var state = GetStateFromMachine(cmachine.stateMachine, clipName);
                if (state != null)
                {
                    return state;
                }
            }

            return null;
        }

        private IDictionary<Transform, string> ChildPaths = new Dictionary<Transform, string>();

        private string GetPathToChild(Transform child)
        {   // Caches the relative paths to children so they only have to be calculated once
            string path;

            if (ChildPaths.TryGetValue(child, out path))
            {
                return path;
            }
            else
            {
                return ChildPaths[child] = AnimationUtility.CalculateTransformPath(child, Root);
            }
        }

        private enum ChangedValues
        {
            None,
            Sprite,
            PositionX,
            PositionY,
            PositionZ,
            RotationZ,
            ScaleX,
            ScaleY,
            ScaleZ,
            Alpha,
            PivotX,
            PivotY,
            VirtualParent,
            SpatialPositionX,
            SpatialPositionY,
            SpatialScaleX,
            SpatialScaleY,
            RawScaleX,
            RawScaleY,
            BoxPivotX,
            BoxPivotY
        }

        private IDictionary<ChangedValues, AnimationCurve> GetCurves(Animation animation, Timeline timeline,
            SpatialInfo defaultInfo, Transform child)
        {
            // This method checks every animatable property for changes and creates a curve
            // for that property if changes are detected.

            var rv = new Dictionary<ChangedValues, AnimationCurve>();

            SpriterInfoBase spriterInfo = (defaultInfo is SpriteInfo)
                ? (SpriterInfoBase)entityInfo.objectInfos[child.name] // No, the cast isn't redundant.  Not for C# 7.3.
                : (SpriterInfoBase)entityInfo.boneInfos[child.name];

            SpatialAdapter spatialAdapter = spriterInfo.spatialAdapter;
            ScaleTracker scaleTracker = spriterInfo.scaleTracker;

            foreach (var key in timeline.keys)
            {
                var info = key.info;

                if (!info.haveBaked && SpriterEntityInfo.IsBakedBoneOrObject(spriterInfo, animation))
                {
                    // The spatial info for this timeline's parent (possibly virtual) for this key frame.
                    SpatialInfo parentInfo =
                    (
                        from mlk in animation.mainlineKeys

                        let isBone = timeline.objectType == ObjectType.bone
                        let refs = isBone ? mlk.boneRefs : mlk.objectRefs

                        let mlkRef = refs.FirstOrDefault(r => r.timelineId == timeline.id && r.timelineKeyId == key.id)

                        let parentRef = mlk.boneRefs.FirstOrDefault(r => r.id == mlkRef?.parentRefId) // May be null.

                        let parentTimeline = parentRef != null
                            ? animation.timelines.FirstOrDefault(t => t.id == parentRef.timelineId)
                            : null

                        let parentTlk = parentTimeline != null
                            ? parentTimeline.keys.FirstOrDefault(k => k.id == parentRef.timelineKeyId)
                            : null

                        select parentTlk?.info // May be null.
                    )
                    .FirstOrDefault();

                    if (parentInfo != null && !parentInfo.haveBaked)
                    {
                        Debug.LogWarning($"For entity: {entityInfo.EntityName}, animation: {animation.name}, " +
                            $"timeline: {timeline.name}, time: {key.time_s:F3}: The timeline key info is being baked " +
                            "but its parent hasn't been baked yet.");
                    }

                    info.Bake(parentInfo);
                }

                if (spatialAdapter == null)
                {
                    if (!rv.ContainsKey(ChangedValues.PositionX) &&
                        (defaultInfo.x != info.x || defaultInfo.y != info.y))
                    {
                        rv[ChangedValues.PositionX] = new AnimationCurve(); // There will be irregular behaviour if curves aren't added for all members
                        rv[ChangedValues.PositionY] = new AnimationCurve(); // in a group, so when one is set, the others have to be set as well
                        rv[ChangedValues.PositionZ] = new AnimationCurve(); // Position.z is alway 0 and is created only when x and/or y is.
                    }
                }
                else
                {
                    if (!rv.ContainsKey(ChangedValues.SpatialPositionX) &&
                        (spatialAdapter.Position.x != info.x || spatialAdapter.Position.y != info.y))
                    {
                        rv[ChangedValues.SpatialPositionX] = new AnimationCurve();
                        rv[ChangedValues.SpatialPositionY] = new AnimationCurve();
                    }
                }

                if (!rv.ContainsKey(ChangedValues.RotationZ) && (defaultInfo.angle != info.angle))
                {
                    rv[ChangedValues.RotationZ] = new AnimationCurve();
                }

                if (spatialAdapter == null)
                {
                    if (!rv.ContainsKey(ChangedValues.ScaleX) &&
                        (defaultInfo.scale_x != info.scale_x || defaultInfo.scale_y != info.scale_y))
                    {
                        rv[ChangedValues.ScaleX] = new AnimationCurve();
                        rv[ChangedValues.ScaleY] = new AnimationCurve();
                        rv[ChangedValues.ScaleZ] = new AnimationCurve();
                    }

                    if (scaleTracker != null)
                    {
                        if (!rv.ContainsKey(ChangedValues.RawScaleX) &&
                            (defaultInfo.rawScaleX != info.rawScaleX || defaultInfo.rawScaleY != info.rawScaleY))
                        {
                            rv[ChangedValues.RawScaleX] = new AnimationCurve();
                            rv[ChangedValues.RawScaleY] = new AnimationCurve();
                        }
                    }
                }
                else
                {
                    if (!rv.ContainsKey(ChangedValues.SpatialScaleX) &&
                        (spatialAdapter.Scale.x != info.scale_x || spatialAdapter.Scale.y != info.scale_y))
                    {
                        rv[ChangedValues.SpatialScaleX] = new AnimationCurve();
                        rv[ChangedValues.SpatialScaleY] = new AnimationCurve();
                    }
                }

                if (!rv.ContainsKey(ChangedValues.Alpha) && defaultInfo.a != info.a)
                {
                    rv[ChangedValues.Alpha] = new AnimationCurve();
                }

                if (!rv.ContainsKey(ChangedValues.VirtualParent) && (defaultInfo.parentBoneName != info.parentBoneName))
                {
                    if (spriterInfo.hasVirtualParent)
                    {
                        rv[ChangedValues.VirtualParent] = new AnimationCurve();
                    }
                    else
                    {
                        Debug.LogWarning($"For entity: {entityInfo.EntityName}, animation: {animation.name}, " +
                            $"timeline: {child.name}, time: {key.time_s}, a virtual parent change was detected but a " +
                            "VirtualParent component wasn't setup.  This is likely due to there being timeline keys " +
                            "without a corresponding entry in <bone_ref> or <object_ref>.  This warning can likely " +
                            "be ignored.");
                    }
                }

                var spriteDefaultInfo = defaultInfo as SpriteInfo;

                if (spriteDefaultInfo != null)
                {
                    if (timeline.objectType == ObjectType.box)
                    {
                        var boxInfo = info as SpriteInfo;
                        var boxDefaultInfo = spriteDefaultInfo;

                        if (!rv.ContainsKey(ChangedValues.BoxPivotX) &&
                            (boxDefaultInfo.pivot_x != boxInfo.pivot_x || boxDefaultInfo.pivot_y != boxInfo.pivot_y))
                        {
                            rv[ChangedValues.BoxPivotX] = new AnimationCurve();
                            rv[ChangedValues.BoxPivotY] = new AnimationCurve();
                        }
                    }
                    else
                    {
                        // This is spatial data for a sprite...
                        var sinfo = info as SpriteInfo;

                        if (!rv.ContainsKey(ChangedValues.Sprite) && (spriteDefaultInfo.fileId != sinfo.fileId || spriteDefaultInfo.folderId != sinfo.folderId))
                        {
                            rv[ChangedValues.Sprite] = new AnimationCurve();
                        }

                        bool hasPivotController = (spriterInfo as SpriterObjectInfo).hasPivotController;

                        if (!rv.ContainsKey(ChangedValues.PivotX) && hasPivotController &&
                            (spriteDefaultInfo.pivot_x != sinfo.pivot_x || spriteDefaultInfo.pivot_y != sinfo.pivot_y))
                        {
                            rv[ChangedValues.PivotX] = new AnimationCurve();
                            rv[ChangedValues.PivotY] = new AnimationCurve();
                        }
                    }
                }
            }

            return rv;
        }
    }
}
