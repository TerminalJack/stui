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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Stui.Prefabs
{
    using Importing;
    using Animations;
    using EntityInfo;

    using UnityEngine.Rendering;
    using Stui.Extensions;

    public class PrefabBuilder : UnityEngine.Object
    {
        private ScmlProcessingInfo ProcessingInfo;
        private List<string> _previousActiveMapNames;

        public PrefabBuilder(ScmlProcessingInfo info)
        {
            ProcessingInfo = info;
        }

        public IEnumerator Build(ScmlObject scmlObj, string scmlPath, IBuildTaskContext buildCtx)
        {
            // The process begins by loading up all the textures
            var spriterProjDirectory = Path.GetDirectoryName(scmlPath);

            // Make sure all of the image files have the proper import settings...

            AssetDatabase.StartAssetEditing();

            foreach (var folder in scmlObj.folders)
            {
                foreach (var file in folder.files)
                {
                    if (file.objectType == ObjectType.sprite)
                    {
                        var path = string.Format("{0}/{1}", spriterProjDirectory, file.name);

                        if (buildCtx.IsCanceled) { yield break; }
                        yield return $"{buildCtx.MessagePrefix}: Setting texture import options for {path}";

                        SetTextureImportSettings(path, file);
                    }
                }
            }

            AssetDatabase.StopAssetEditing();

            // Now that the image files have the proper import settings, populate the folders and fileInfo collections...

            var folders = new Dictionary<int, IDictionary<int, Sprite>>();
            var fileInfo = new Dictionary<int, IDictionary<int, File>>();

            foreach (var folder in scmlObj.folders)
            {
                var files = folders[folder.id] = new Dictionary<int, Sprite>();
                var fi = fileInfo[folder.id] = new Dictionary<int, File>();

                foreach (var file in folder.files)
                {
                    if (file.objectType == ObjectType.sprite)
                    {
                        var path = string.Format("{0}/{1}", spriterProjDirectory, file.name);

                        if (buildCtx.IsCanceled) { yield break; }
                        yield return $"{buildCtx.MessagePrefix}: Getting sprite at {path}";

                        files[file.id] = GetSpriteAtPath(path);
                        fi[file.id] = file;
                    }
                }
            }

            foreach (var entity in scmlObj.entities)
            {   // Now begins the real prefab build process
                var prefabPath = string.Format("{0}/{1}.prefab", spriterProjDirectory, entity.name);

                if (buildCtx.IsCanceled) { yield break; }
                yield return $"{buildCtx.MessagePrefix}: Getting/creating prefab at {prefabPath}";

                var prefab = (GameObject)AssetDatabase.LoadAssetAtPath(prefabPath, typeof(GameObject));
                GameObject instance;

                if (prefab == null)
                {   // Creates an empty prefab if one doesn't already exists
                    instance = new GameObject(entity.name);
                    prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(instance, prefabPath, InteractionMode.AutomatedAction);
                    ProcessingInfo.NewPrefabs.Add(prefab);
                }
                else
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab); //instantiates the prefab if it does exist
                    ProcessingInfo.ModifiedPrefabs.Add(prefab);
                }

                SaveAndRemoveCharacterMaps(instance);

                var prefabBuildProcess =
                    IteratorUtils.SafeEnumerable(
                        () => TryBuild(scmlObj, entity, prefab, instance, spriterProjDirectory, prefabPath, folders, fileInfo, buildCtx),
                        ex =>
                        {
                            DestroyImmediate(instance);
                            Debug.LogErrorFormat("Build failed for '{0}': {1}", entity.name, ex);
                        });

                while (prefabBuildProcess.MoveNext())
                {
                    yield return prefabBuildProcess.Current;
                }

                if (buildCtx.IsCanceled)
                {
                    DestroyImmediate(instance);
                    yield break;
                }
            }
        }

        private IEnumerator TryBuild(ScmlObject scmlObj, Entity entity, GameObject prefab, GameObject instance,
            string spriterProjDirectory, string prefabPath, IDictionary<int, IDictionary<int, Sprite>> folders,
            Dictionary<int, IDictionary<int, File>> fileInfo, IBuildTaskContext buildCtx)
        {
            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}: Processing entity '{entity.name}'";

            buildCtx.EntityName = entity.name;

            // SpriterEntityInfo will initialize and gather info about the bones and sprites for this entity.
            SpriterEntityInfo entityInfo = new SpriterEntityInfo();

            var entityInfoProcess = entityInfo.Process(spriterProjDirectory, scmlObj, entity, fileInfo, buildCtx);
            while (entityInfoProcess.MoveNext())
            {
                yield return entityInfoProcess.Current;
            }

            if (buildCtx.IsCanceled) { yield break; }

            var controllerPath = string.Format("{0}/{1}.controller", spriterProjDirectory, entity.name);
            var animator = instance.GetOrAddComponent<Animator>(); // Fetches/creates the prefab's Animator

            AnimatorController controller = null;

            if (animator.runtimeAnimatorController != null)
            {   // The controller we use is hopefully the controller attached to the animator
                controller = animator.runtimeAnimatorController as AnimatorController ?? //Or the one that's referenced by an OverrideController
                    (AnimatorController)((AnimatorOverrideController)animator.runtimeAnimatorController).runtimeAnimatorController;
            }

            if (controller == null)
            {   // Otherwise we have to check the AssetDatabase for our controller
                controller = (AnimatorController)AssetDatabase.LoadAssetAtPath(controllerPath, typeof(AnimatorController));
                if (controller == null)
                {
                    controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath); //Or create a new one if it doesn't exist.
                    ProcessingInfo.NewControllers.Add(controller);
                }

                animator.runtimeAnimatorController = controller;
            }

            bool advancedBoneScaleSettingEnabled = ScmlImportOptions.options?.IsAdvancedBoneScales ?? false;
            bool needSpatialController = advancedBoneScaleSettingEnabled && entity.animations.Exists(a => a.hasAnimatedBoneScales);

            SpatialController spatialController;
            instance.TryGetComponent(out spatialController);

            if (needSpatialController)
            {
                if (spatialController == null)
                {
                    spatialController = instance.AddComponent<SpatialController>();
                }
            }
            else if (spatialController != null)
            {
                foreach (var spatialAdapter in instance.GetComponentsInChildren<SpatialAdapter>())
                {
                    DestroyImmediate(spatialAdapter);
                }

                foreach (var scaleTracker in instance.GetComponentsInChildren<ScaleTracker>())
                {
                    DestroyImmediate(scaleTracker);
                }

                DestroyImmediate(spatialController);

                spatialController = null;
            }

            bool needDependencyResolver =
                needSpatialController ||
                entityInfo.boneInfos.Values.Any(i => i.hasVirtualParent) ||
                entityInfo.objectInfos.Values.Any(i => i.hasVirtualParent);

            DependencyResolver dependencyResolver;
            instance.TryGetComponent(out dependencyResolver);

            if (needDependencyResolver)
            {
                if (dependencyResolver == null)
                {
                    dependencyResolver = instance.AddComponent<DependencyResolver>();
                }
            }
            else if (dependencyResolver != null)
            {
                DestroyImmediate(dependencyResolver);

                dependencyResolver = null;
            }

            var transforms = new Dictionary<string, Transform>(); //All of the bones and sprites, identified by Timeline.name, because those are truly unique
            transforms["rootTransform"] = instance.transform; //The root GameObject needs to be part of this hierarchy as well

            var defaultBones = new Dictionary<string, SpatialInfo>();  // These are basically the object states on the first frame of the first animation
            var defaultSprites = new Dictionary<string, SpriteInfo>(); // They are used as control values in determining whether something has changed
            var defaultActionPoints = new Dictionary<string, SpatialInfo>();
            var defaultCollisionRectangles = new Dictionary<string, SpriteInfo>();

            var animBuilder = new AnimationBuilder(ProcessingInfo, folders, transforms, defaultBones, defaultSprites,
                defaultActionPoints, defaultCollisionRectangles, prefabPath, controller, entityInfo);
            var firstAnim = true; //The prefab's graphic will be determined by the first frame of the first animation

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}: processing entity-scoped metadata";

            ProcessEntityScopedMetadata(instance.transform, entityInfo);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}: processing entity events";

            ProcessEntityEvents(instance.transform, entityInfo);

            if (buildCtx.IsCanceled) { yield break; }
            yield return $"{buildCtx.MessagePrefix}: processing entity sounds";

            ProcessEntitySounds(instance.transform, entityInfo);

            bool bakeAnimatedBoneScales = ScmlImportOptions.options?.IsNormalBoneScales ?? true;

            foreach (var animation in entity.animations)
            {
                buildCtx.AnimationName = animation.name;

                if (buildCtx.IsCanceled) { yield break; }
                yield return $"{buildCtx.MessagePrefix}: processing";

                animation.usesBakedSpatialData = !animation.hasAnimatedBoneScales || !advancedBoneScaleSettingEnabled;

                if (firstAnim && !animation.usesBakedSpatialData && spatialController != null)
                {
                    spatialController.UseSpriterScaling = true; // This will be the Spatial Controller's bind pose value.
                }

                var timelines = new Dictionary<int, Timeline>();

                foreach (var timeline in animation.timelines) // Timelines hold all the critical data such as positioning and graphics used
                {
                    timelines[timeline.id] = timeline;

                    if (entityInfo.boneInfos.GetOrDefault(timeline.name) != null && bakeAnimatedBoneScales)
                    {   // This is a bone and animated bone scales are being baked.  Bake them for this timeline.
                        BoneBaker.BakeAnimatedBoneScales(animation, timeline, entityInfo);
                    }
                }

                foreach (var key in animation.mainlineKeys)
                {
                    var parents = new Dictionary<int, string>(); //Parents are referenced by different IDs V_V
                    parents[-1] = "rootTransform"; //This is where "-1 == no parent" comes in handy

                    if (buildCtx.IsCanceled) { yield break; }
                    yield return $"{buildCtx.MessagePrefix}, mainline key time: {key.time_s:F3}, processing bones";

                    ProcessBones(parents, transforms, animation, timelines, key, defaultBones, entityInfo);

                    if (buildCtx.IsCanceled) { yield break; }
                    yield return $"{buildCtx.MessagePrefix}, mainline key time: {key.time_s:F3}, processing sprites";

                    ProcessSprites(parents, transforms, animation, timelines, key, defaultBones, defaultSprites, entityInfo,
                        folders, firstAnim, animation.mainlineKeys);

                    if (buildCtx.IsCanceled) { yield break; }
                    yield return $"{buildCtx.MessagePrefix}, mainline key time: {key.time_s:F3}, processing action points";

                    ProcessActionPoints(parents, transforms, animation, timelines, key, defaultBones, defaultActionPoints, entityInfo);

                    if (buildCtx.IsCanceled) { yield break; }
                    yield return $"{buildCtx.MessagePrefix}, mainline key time: {key.time_s:F3}, processing collision rectangles";

                    ProcessCollisionRectangles(parents, transforms, animation, timelines, key, defaultBones,
                        defaultCollisionRectangles, entityInfo, entity, firstAnim);

                    firstAnim = false;
                }

                if (dependencyResolver != null)
                {
                    dependencyResolver.MarkDirty();
                    yield return null; // Allow the dependencyResolver to update.
                }

                var animBuildProcess =
                    IteratorUtils.SafeEnumerable(
                        () => animBuilder.Build(animation, timelines, buildCtx),
                        ex =>
                        {
                            Debug.LogErrorFormat("Unable to build animation '{0}' for '{1}', reason: {2}", animation.name, entity.name, ex);
                        });

                while (animBuildProcess.MoveNext())
                {
                    yield return animBuildProcess.Current;
                }

                if (buildCtx.IsCanceled) { yield break; }
            }

            buildCtx.AnimationName = "";

            instance.GetOrAddComponent<SortingGroup>();

            FinalizeVirtualParentProcessing(entityInfo, transforms);
            ProcessCharacterMaps(entity, instance, folders);

            if (dependencyResolver != null)
            {
                dependencyResolver.MarkDirty();
                yield return null; // Allow the dependencyResolver to update.
            }

            EditorUtility.SetDirty(instance);

            PrefabUtility.SaveAsPrefabAssetAndConnect(instance, prefabPath, InteractionMode.AutomatedAction);
            DestroyImmediate(instance); //Apply the instance's changes to the prefab, then destroy the instance.

            buildCtx.EntityName = "";

            if (buildCtx.ImportedPrefabs.Contains(prefabPath))
            {
                Debug.LogWarning($"The prefab at '{prefabPath}' has been imported more than once in the same import " +
                    "session.  This is likely due to 1) multiple .scml files in the same folder that have entities " +
                    "that share the same name, or 2) a single .scml file with duplicate entity names.");
            }

            buildCtx.ImportedPrefabs.Add(prefabPath);
        }

        private void BuildSpriterVariableTransforms(Transform metadataTransform, List<VarInstanceInfo> varInstanceInfos)
        {
            // Build the variable transforms under the given metadata transform.
            //
            //   ...
            //   └── ... metadata
            //       └── varname1 (Float variable)
            //       └── varname2 (Int variable)
            //       └── varname3 (String variable)

            foreach (var varInfo in varInstanceInfos)
            {
                var varDef = varInfo.varDef;

                var varTransformName = $"{varDef.name} ({varDef.type} variable)";

                var varTransform = metadataTransform.Find(varTransformName);
                if (varTransform == null)
                {
                    varTransform = new GameObject(varTransformName).transform;
                }

                varTransform.SetParent(metadataTransform, worldPositionStays: false);

                varInfo.gameObject = varTransform.gameObject;

                // Remove any and all Spriter variable components that alread exist on this game object.  There
                // should be at most one but the type may have changed between imports so remove all previous
                // components to be safe.

                if (varTransform.gameObject.TryGetComponent<SpriterFloat>(out var floatComponent))
                {
                    DestroyImmediate(floatComponent);
                }

                if (varTransform.gameObject.TryGetComponent<SpriterInt>(out var intComponent))
                {
                    DestroyImmediate(intComponent);
                }

                if (varTransform.gameObject.TryGetComponent<SpriterString>(out var stringComponent))
                {
                    DestroyImmediate(stringComponent);
                }

                // Create the appropriate variable component...
                switch (varDef.type)
                {
                    case VarType.Float:
                        var floatVarComponent = varTransform.gameObject.AddComponent<SpriterFloat>();
                        floatVarComponent.VariableName = varDef.name;
                        if (!float.TryParse(varDef.defaultValue, out floatVarComponent.DefaultValue))
                        {
                            floatVarComponent.DefaultValue = 0f;
                        }

                        floatVarComponent.Value = floatVarComponent.DefaultValue;
                        break;

                    case VarType.Int:
                        var intVarComponent = varTransform.gameObject.AddComponent<SpriterInt>();
                        intVarComponent.VariableName = varDef.name;
                        if (!int.TryParse(varDef.defaultValue, out intVarComponent.DefaultValue))
                        {
                            intVarComponent.DefaultValue = -1;
                        }

                        intVarComponent.Value = intVarComponent.DefaultValue;
                        break;

                    case VarType.String:
                        var stringVarComponent = varTransform.gameObject.AddComponent<SpriterString>();
                        stringVarComponent.VariableName = varDef.name;
                        stringVarComponent.PossibleValues = varDef.possibleStringValues.ToList();
                        stringVarComponent.ValueIndex = stringVarComponent.PossibleValues.Count > 0 ? 0 : -1;
                        break;

                    default:
                        break;
                }
            }
        }

        private void BuildSpriterTagTransforms(Transform metadataTransform, List<TagInstanceInfo> tagInstanceInfos)
        {
            // Build the tag transforms under the given metadata transform.
            //
            //   ...
            //   └── ... metadata
            //       └── tagname1 (Tag)
            //       └── tagname2 (Tag)

            foreach (var tagInfo in tagInstanceInfos)
            {
                var tagDef = tagInfo.tagDef;

                var tagTransformName = $"{tagDef.name} (Tag)";

                var tagTransform = metadataTransform.Find(tagTransformName);
                if (tagTransform == null)
                {
                    tagTransform = new GameObject(tagTransformName).transform;
                }

                tagTransform.SetParent(metadataTransform, worldPositionStays: false);

                tagInfo.gameObject = tagTransform.gameObject;

                SpriterTag tagComponent;

                // Remove any preexisting tag component.
                if (tagTransform.gameObject.TryGetComponent(out tagComponent))
                {
                    DestroyImmediate(tagComponent);
                }

                // Create tag component.
                tagComponent = tagTransform.gameObject.AddComponent<SpriterTag>();

                tagComponent.TagName = tagDef.name;
            }
        }

        private void ProcessEntityScopedMetadata(Transform parentTransform, SpriterEntityInfo entityInfo)
        {
            // If this entity has any metadata (variables and/or tags) at the entity-level then create the neccessary
            // hierarchy.  The hierarchy will look like the following:
            //
            //   entity_name
            //   └── entity_name metadata
            //       └── varname1 (Float variable)
            //       └── varname2 (Int variable)
            //       └── varname3 (String variable)
            //       └── tagname1 (Tag)
            //       └── tagname2 (Tag)

            string metadataGameObjectName = $"{parentTransform.name} metadata";
            var metadataTransform = parentTransform.Find(metadataGameObjectName);

            if (entityInfo.hasMetadata)
            {
                if (metadataTransform == null)
                {
                    metadataTransform = new GameObject(metadataGameObjectName).transform;
                }

                metadataTransform.SetParent(parentTransform, worldPositionStays: false);

                BuildSpriterVariableTransforms(metadataTransform, entityInfo.varInstanceInfos.Values.ToList());

                BuildSpriterTagTransforms(metadataTransform, entityInfo.tagInstanceInfos.Values.ToList());
            }
            else if (metadataTransform != null)
            {   // If a metadata game object exists, remove it.
                DestroyImmediate(metadataTransform.gameObject);
            }
        }

        private void ProcessObjectScopedMetadata(Transform parentTransform, SpriterInfoBase objInfo)
        {
            // If this object (aka timeline, which may also be an event) has any metadata then create the neccessary
            // hierarchy.  The hierarchy will look like the following:
            //
            //   ...
            //   └── object_name (parentTransform)
            //       └── object_name metadata
            //           └── object_varname1 (String variable)
            //           └── object_varname2 (Float variable)
            //           └── object_tagname1 (Tag)
            //           └── object_tagname2 (Tag)

            string metadataGameObjectName = $"{parentTransform.name} metadata";
            var metadataTransform = parentTransform.Find(metadataGameObjectName);

            if (objInfo.hasMetadata)
            {
                if (metadataTransform == null)
                {
                    metadataTransform = new GameObject(metadataGameObjectName).transform;
                }

                metadataTransform.SetParent(parentTransform, worldPositionStays: false);

                BuildSpriterVariableTransforms(metadataTransform, objInfo.varInstanceInfos.Values.ToList());

                BuildSpriterTagTransforms(metadataTransform, objInfo.tagInstanceInfos.Values.ToList());
            }
            else if (metadataTransform != null)
            {   // If a metadata game object exists, remove it.
                DestroyImmediate(metadataTransform.gameObject);
            }
        }

        private void ProcessEntityEvents(Transform parentTransform, SpriterEntityInfo entityInfo)
        {
            // If this entity has any events then create the neccessary hierarchy.
            // The hierarchy will look like the following:
            //
            //   entity_name
            //   └── entity_name events
            //       └── fire weapon (Event)
            //       └── event_with_metadata (Event)
            //           └── event_with_metadata metadata
            //               └── event_varname1 (Float variable)
            //               └── event_tagname1 (Tag)
            //
            // Note that ProcessObjectScopedMetadata() will handle creation of the metadata game objects.
            //
            // There will also be a EventController added to the root of the prefab.

            string eventsGameObjectName = $"{parentTransform.name} events";
            var eventsTransform = parentTransform.Find(eventsGameObjectName);

            var allEvents = entityInfo.objectInfos.Values.Where(o => o.type == ObjectType.spriterEvent).ToList();

            EventController eventControllerComponent;
            parentTransform.TryGetComponent(out eventControllerComponent);

            if (allEvents.Count > 0)
            {
                if (eventsTransform == null)
                {
                    eventsTransform = new GameObject(eventsGameObjectName).transform;
                }

                eventsTransform.SetParent(parentTransform, worldPositionStays: false);

                foreach (var eventInfo in allEvents)
                {
                    var thisEventTransformName = $"{eventInfo.name} event";

                    var thisEventTransform = eventsTransform.Find(thisEventTransformName);
                    if (thisEventTransform == null)
                    {
                        thisEventTransform = new GameObject(thisEventTransformName).transform;
                    }

                    thisEventTransform.SetParent(eventsTransform, worldPositionStays: false);

                    // Remove any preexisting SpriterEventListener component.
                    SpriterEventListener spriterEventListenerComponent;

                    if (thisEventTransform.gameObject.TryGetComponent(out spriterEventListenerComponent))
                    {
                        DestroyImmediate(spriterEventListenerComponent);
                    }

                    // Create SpriterEventListener component.
                    spriterEventListenerComponent = thisEventTransform.gameObject.AddComponent<SpriterEventListener>();

                    spriterEventListenerComponent._eventName = eventInfo.name;

                    ProcessObjectScopedMetadata(thisEventTransform, eventInfo);
                }

                // If an EventController component doesn't exist on the parent (which is assumed to be the root) then create one.
                if (eventControllerComponent == null)
                {
                    eventControllerComponent = parentTransform.gameObject.AddComponent<EventController>();
                }
            }
            else if (eventsTransform != null)
            {
                // If an events game object exists, remove it.
                DestroyImmediate(eventsTransform.gameObject);

                // If an EventController component exists on the parent (which is assumed to be the root) then remove it.
                if (eventControllerComponent != null)
                {
                    DestroyImmediate(eventControllerComponent);
                }

            }
        }

        private void ProcessEntitySounds(Transform parentTransform, SpriterEntityInfo entityInfo)
        {   // All of the sound-related info. will go into a SoundController component at the root of the prefab.

            SoundController soundController;

            if (parentTransform.TryGetComponent(out soundController))
            {
                DestroyImmediate(soundController);
            }

            if (entityInfo.soundItems.Count > 0)
            {
                soundController = parentTransform.gameObject.AddComponent<SoundController>();

                foreach (var soundItem in entityInfo.soundItems)
                {
                    soundController.SoundItems.Add(soundItem);
                }
            }
        }

        private void FinalizeVirtualParentProcessing(SpriterEntityInfo entityInfo, Dictionary<string, Transform> transforms)
        {
            // Add 'possible parents' to all of the virtual parent components.
            foreach (var info in entityInfo.boneInfos.Values.Cast<SpriterInfoBase>()
                .Concat(entityInfo.objectInfos.Values))
            {
                if (info.hasVirtualParent && info.virtualParentTransform != null)
                {
                    if (info.virtualParentTransform.TryGetComponent<VirtualParent>(out var vp))
                    {
                        vp.PossibleParents.Clear();

                        foreach (var parentName in info.parentBoneNames)
                        {
                            var possibleParentTransform = transforms[parentName];
                            vp.PossibleParents.Add(possibleParentTransform);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"VirtualParent missing on: {info.name}");
                    }
                }
            }
        }

        private void SaveAndRemoveCharacterMaps(GameObject instance)
        {
            // If the prefab already exists during an import AND it has a character map controller, all of the active
            // maps have to be disabled during the import and re-applied afterward.
            if (instance.TryGetComponent<CharacterMapController>(out var characterController))
            {
                _previousActiveMapNames = characterController.ActiveMapNames.ToList();
                characterController.Clear();
            }
            else
            {
                _previousActiveMapNames = null;
            }
        }

        private void ProcessCharacterMaps(Entity entity, GameObject instance, IDictionary<int, IDictionary<int, Sprite>> folders)
        {
            if (entity.characterMaps.Count == 0 || ScmlImportOptions.options == null || !ScmlImportOptions.options.createCharacterMaps)
            {   // Either the feature is disabled or this entity doesn't have any character maps.
                if (instance.TryGetComponent<CharacterMapController>(out var c))
                {
                    DestroyImmediate(c);
                }

                return;
            }

            var characterMapController = instance.GetOrAddComponent<CharacterMapController>();

            // Build characterMapController.baseMap...

            characterMapController.BaseMap.Clear();

            // Note: This code here is the reason why all active maps have to be temporarily removed.
            foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(includeInactive: true))
            {
                // Map sprites to the appropriate transform and, if appropriate, the texture controller index.

                Transform targetTransform = renderer.transform;

                if (targetTransform.TryGetComponent<TextureController>(out var textureController))
                {
                    for (int i = 0; i < textureController.Sprites.Length; ++i)
                    {
                        var sprite = textureController.Sprites[i];
                        characterMapController.BaseMap.Add(sprite, new SpriteMapTarget(targetTransform, i));
                    }
                }
                else
                {
                    characterMapController.BaseMap.Add(renderer.sprite, new SpriteMapTarget(targetTransform, 0));
                }
            }

            characterMapController.Refresh(); // Apply _just_ the base map.

            // Build characterMapController.availableMaps...

            characterMapController.AvailableMaps.Clear();

            foreach (var characterMap in entity.characterMaps)
            {
                var charMap = new CharacterMapping(characterMap.name);

                foreach (var mapInstruction in characterMap.maps)
                {
                    Sprite srcSprite = TryGetSprite(folders, mapInstruction.folderId, mapInstruction.fileId);

                    if (srcSprite == null)
                    {
                        Debug.LogWarning($"Stui: ProcessCharacterMaps(): For entity '{entity.name}', " +
                            $"character map '{characterMap.name}', the source sprite at folderId: {mapInstruction.folderId}, " +
                            $"fileId: {mapInstruction.fileId} wasn't found.");

                        continue;
                    }

                    Sprite targetSprite = null;

                    if (mapInstruction.targetFolderId != -1 && mapInstruction.targetFileId != -1)
                    {
                        targetSprite = TryGetSprite(folders, mapInstruction.targetFolderId, mapInstruction.targetFileId);

                        if (targetSprite == null)
                        {
                            Debug.LogWarning($"Stui: ProcessCharacterMaps(): For entity '{entity.name}', " +
                                $"character map '{characterMap.name}', the target sprite at folderId: {mapInstruction.folderId}, " +
                                $"fileId: {mapInstruction.fileId} wasn't found.");

                            continue;
                        }
                    }

                    var spriteMapping = characterMapController.BaseMap.spriteMaps.Find(s => s.sprite == srcSprite);

                    if (spriteMapping != null)
                    {
                        foreach (var target in spriteMapping.targets)
                        {
                            charMap.Add(targetSprite, target);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Stui: ProcessCharacterMaps(): For entity '{entity.name}', " +
                            $"character map '{characterMap.name}', the source sprite at folderId: {mapInstruction.folderId}, " +
                            $"fileId: {mapInstruction.fileId} doesn't exist in the base map.");
                    }
                }

                characterMapController.AvailableMaps.Add(charMap);
            }

            if (_previousActiveMapNames != null)
            {
                // Remove any invalid character map names from _previousActiveMapNames (from pre-existing
                // character map controllers.)
                _previousActiveMapNames.RemoveAll(name =>
                {
                    return characterMapController.AvailableMaps.Find(m => m.name == name) == null;
                });

                characterMapController.ActiveMapNames = _previousActiveMapNames.ToList();
                characterMapController.Refresh(); // Apply any user-defined mappings.

                _previousActiveMapNames = null;
            }
        }

        private Sprite TryGetSprite(IDictionary<int, IDictionary<int, Sprite>> folders, int folderIdx, int fileIdx)
        {
            try
            {
                return folders[folderIdx][fileIdx];
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ProcessBones(Dictionary<int, string> parents, Dictionary<string, Transform> transforms,
            Animation animation, Dictionary<int, Timeline> timelines, MainlineKey key,
            Dictionary<string, SpatialInfo> defaultBones, SpriterEntityInfo entityInfo)
        {
            var boneRefs = new Queue<Ref>(key.boneRefs);

            while (boneRefs.Count > 0)
            {
                var bone = boneRefs.Dequeue();
                var timeline = timelines[bone.timelineId];
                parents[bone.id] = timeline.name;

                // We only need to go through this once, so ignore it if it's already in the dict.
                if (!transforms.ContainsKey(timeline.name))
                {
                    if (parents.ContainsKey(bone.parentRefId))
                    {
                        ProcessBone(parents, transforms, animation, defaultBones, entityInfo, timeline, bone);
                    }
                    else
                    {   // If the parent cannot be found, it will probably be found later, so save it
                        boneRefs.Enqueue(bone);
                    }
                }
            }
        }

        private void ProcessBone(Dictionary<int, string> parents, Dictionary<string, Transform> transforms,
            Animation animation, Dictionary<string, SpatialInfo> defaultBones, SpriterEntityInfo entityInfo,
            Timeline timeline, Ref boneRef)
        {
            var parentName = parents[boneRef.parentRefId];
            var parentTransform = transforms[parentName];

            SpriterBoneInfo spriterBoneInfo;
            entityInfo.boneInfos.TryGetValue(timeline.name, out spriterBoneInfo);

            if (spriterBoneInfo == null || spriterBoneInfo.type != ObjectType.bone)
            {
                throw new Exception($"Stui: ProcessBone() was unable to find bone info for bone '{timeline.name}'.");
            }

            ProcessVirtualParent(parentName, ref parentTransform, spriterBoneInfo);

            var child = parentTransform.Find(timeline.name); //Try to find the child transform if it exists
            if (child == null)
            {   //Or create a new one
                child = new GameObject(timeline.name).transform;
                child.SetParent(parentTransform);
            }

            transforms[timeline.name] = child;

            var spatialInfo = defaultBones[timeline.name] = timeline.keys.Find(x => x.id == boneRef.timelineKeyId).info;

            if (!spatialInfo.haveBaked && SpriterEntityInfo.IsBakedBoneOrObject(spriterBoneInfo, animation))
            {
                SpatialInfo parentInfo;
                defaultBones.TryGetValue(parentName, out parentInfo); // 'parentName' may be grandparent if a virtual parent was created.
                spatialInfo.Bake(parentInfo);
            }

            // The transform gets initialized with the baked or unbaked, regardless.  If a Spatial Adapter
            // is used then it will initialize the position and scale when it is enabled.
            child.localPosition = new Vector3(spatialInfo.x, spatialInfo.y, 0f);
            child.localRotation = Quaternion.Euler(0, 0, spatialInfo.angle);
            child.localScale = new Vector3(spatialInfo.scale_x, spatialInfo.scale_y, 1f);

            SpatialAdapter spatialAdapter;
            child.gameObject.TryGetComponent(out spatialAdapter);

            if (SpriterEntityInfo.UseTransformForPositionAndScale(spriterBoneInfo))
            {   // The Transform's Position and Scale are always used for animating this bone and they
                // will always be baked.  The game object may also need a ScaleTracker if any of its
                // decendants are animated bones.

                if (spatialAdapter != null)
                {
                    DestroyImmediate(spatialAdapter);
                }

                ScaleTracker scaleTracker;
                child.gameObject.TryGetComponent(out scaleTracker);

                if (SpriterEntityInfo.BoneUsesScaleTracker(spriterBoneInfo))
                {
                    if (scaleTracker == null)
                    {
                        scaleTracker = child.gameObject.AddComponent<ScaleTracker>();
                    }

                    scaleTracker.RawScale = new Vector2(spatialInfo.rawScaleX, spatialInfo.rawScaleY);

                    spriterBoneInfo.scaleTracker = scaleTracker;
                }
                else if (scaleTracker != null)
                {
                    DestroyImmediate(scaleTracker);
                }
            }
            else
            {   // The SpatialAdapter's Position and Scale are always used for animating this bone.  The
                // position and scale may be baked or not, depending on the animation.
                if (spatialAdapter == null)
                {
                    spatialAdapter = child.gameObject.AddComponent<SpatialAdapter>();
                }

                spatialAdapter.Position = new Vector2(spatialInfo.x, spatialInfo.y);
                spatialAdapter.Scale = new Vector2(spatialInfo.scale_x, spatialInfo.scale_y);

                spriterBoneInfo.spatialAdapter = spatialAdapter;
            }

            AlphaController alphaController;
            child.gameObject.TryGetComponent(out alphaController);

            if (spriterBoneInfo.hasAlphaController)
            {
                if (alphaController == null)
                {
                    alphaController = child.gameObject.AddComponent<AlphaController>();
                }

                alphaController.Alpha = spatialInfo.a;
            }
            else if (alphaController != null)
            {
                DestroyImmediate(alphaController);
            }

            ProcessObjectScopedMetadata(child, spriterBoneInfo);
        }

        private void ProcessSprites(Dictionary<int, string> parents, Dictionary<string, Transform> transforms,
            Animation animation, Dictionary<int, Timeline> timelines, MainlineKey key,
            Dictionary<string, SpatialInfo> defaultBones, Dictionary<string, SpriteInfo> defaultSprites,
            SpriterEntityInfo entityInfo, IDictionary<int, IDictionary<int, Sprite>> folders, bool firstAnim,
            List<MainlineKey> mlks)
        {
            foreach (var oref in key.objectRefs)
            {
                var timeline = timelines[oref.timelineId];

                SpriterObjectInfo spriterObjectInfo;
                entityInfo.objectInfos.TryGetValue(timeline.name, out spriterObjectInfo);

                if (spriterObjectInfo == null || spriterObjectInfo.type != ObjectType.sprite)
                {   // Don't log a warning if this was one of the unsupported Spriter object types.
                    if (spriterObjectInfo == null)
                    {
                        Debug.LogWarning($"Stui: ProcessSprites() was unable to find object info for sprite '{timeline.name}'.");
                    }

                    continue;
                }

                if (transforms.ContainsKey(timeline.name))
                {
                    continue;
                }

                var parentName = parents[oref.parentRefId];
                var parentTransform = transforms[parentName];

                ProcessVirtualParent(parentName, ref parentTransform, spriterObjectInfo);

                // 'child' is the name without a suffix, so will be a pivot or a renderer (without a pivot parent.)
                var child = parentTransform.Find(timeline.name);
                if (child == null)
                {
                    child = new GameObject(timeline.name).transform;
                }

                child.SetParent(parentTransform);
                transforms[timeline.name] = child; // Note that virtual parents and renderers w/ a pivot parent aren't added to this.

                var spriteInfo = defaultSprites[timeline.name] = (SpriteInfo)timeline.keys[0].info;

                if (!spriteInfo.haveBaked && SpriterEntityInfo.IsBakedBoneOrObject(spriterObjectInfo, animation))
                {
                    SpatialInfo parentInfo;
                    defaultBones.TryGetValue(parentName, out parentInfo); // 'parentName' may be grandparent if a virtual parent was created.
                    spriteInfo.Bake(parentInfo);
                }

                // If this sprite (for any animation of the entity) has one or more non-default
                // pivots then a pivot controller will need to be created for it.  Otherwise,
                // don't create one and be sure to remove any that might already exist.

                bool needsPivotController = spriterObjectInfo.hasPivotController;

                DynamicPivot2D pivotController;
                child.TryGetComponent(out pivotController);

                if (needsPivotController)
                {
                    if (pivotController == null)
                    {
                        pivotController = child.gameObject.AddComponent<DynamicPivot2D>();
                    }

                    pivotController.Pivot = new Vector2(spriteInfo.pivot_x, spriteInfo.pivot_y);
                }
                else if (pivotController != null)
                {
                    DestroyImmediate(pivotController);
                    pivotController = null;
                }

                // The transform gets initialized with the baked or unbaked, regardless.  If a Spatial Adapter
                // is used then it will initialize the position and scale when it is enabled.
                child.localPosition = new Vector3(spriteInfo.x, spriteInfo.y, 0f);
                child.localEulerAngles = new Vector3(0f, 0f, spriteInfo.angle);
                child.localScale = new Vector3(spriteInfo.scale_x, spriteInfo.scale_y, 1f);

                SpatialAdapter spatialAdapter;
                child.gameObject.TryGetComponent(out spatialAdapter);

                if (SpriterEntityInfo.UseTransformForPositionAndScale(spriterObjectInfo))
                {   // The Transform's Position and Scale are always used for animating this sprite and they
                    // will always be baked.  Remove any preexisting Spatial Adapter.
                    if (spatialAdapter != null)
                    {
                        DestroyImmediate(spatialAdapter);
                    }
                }
                else
                {   // The SpatialAdapter's Position and Scale are always used for animating this sprite.  The
                    // position and scale may be baked or not, depending on the animation.
                    if (spatialAdapter == null)
                    {
                        spatialAdapter = child.gameObject.AddComponent<SpatialAdapter>();
                    }

                    spatialAdapter.Position = new Vector2(spriteInfo.x, spriteInfo.y);
                    spatialAdapter.Scale = new Vector2(spriteInfo.scale_x, spriteInfo.scale_y);

                    spriterObjectInfo.spatialAdapter = spatialAdapter;
                }

                ProcessObjectScopedMetadata(child, spriterObjectInfo);

                // If a pivot controller is used then the sprite renderer has to go on a child game object.
                string spriteRendererName = spriterObjectInfo.spriteRendererTransformName;
                var rendererTransform = needsPivotController ? child.Find(spriteRendererName) : child;

                if (needsPivotController && rendererTransform == null)
                {
                    rendererTransform = new GameObject(spriteRendererName).transform;
                    rendererTransform.SetParent(child);
                }

                var renderer = rendererTransform.GetOrAddComponent<SpriteRenderer>(); // Get or create a Sprite Renderer

                renderer.sprite = folders[spriteInfo.folderId][spriteInfo.fileId];
                renderer.sortingOrder = GetSortingOrderBindPoseValue(mlks, timeline);

                if (needsPivotController)
                {
                    rendererTransform.localPosition = Vector3.zero; // The pivot script will adjust this.
                    rendererTransform.localEulerAngles = Vector3.zero;
                    rendererTransform.localScale = Vector3.one;
                }

                var color = renderer.color;
                color.a = spriteInfo.a;
                renderer.color = color;

                // An Alpha Controller component will need to be added if any of this sprite's parent bones use alpha.

                AlphaController alphaController;
                child.TryGetComponent(out alphaController);

                if (spriterObjectInfo.hasAlphaController)
                {
                    if (alphaController == null)
                    {
                        alphaController = child.gameObject.AddComponent<AlphaController>();
                    }

                    alphaController.Alpha = spriteInfo.a;
                }
                else if (alphaController != null)
                {
                    DestroyImmediate(alphaController);
                }

                // Disable the Sprite Renderer if this isn't the first frame of the first animation
                renderer.enabled = firstAnim;

                if (pivotController != null)
                {
                    pivotController.Refresh();
                }
            }
        }

        private int GetSortingOrderBindPoseValue(List<MainlineKey> mlks, Timeline timeline)
        {
            var sortingOrderInfos =
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

                // Find the matching objectRef (if any) so we can get z_index
                let oref =
                    mlk.objectRefs.FirstOrDefault(or =>
                        or.timelineId == timeline.id &&
                        or.timelineKeyId == tlk?.id)

                select new
                {
                    mlk.time_s,
                    isVisible = tlk != null,
                    // Note: sortingOrder doesn't apply if the sprite isn't visible.
                    sortingOrder = tlk != null && oref != null ? Ref.ZIndexToSortingOrder(oref.z_index) : -1
                }
            )
            .ToList();

            sortingOrderInfos.RemoveAll(i => !i.isVisible);

            return sortingOrderInfos.Count > 0 ? sortingOrderInfos[0].sortingOrder : 0;
        }

        private void ProcessActionPoints(Dictionary<int, string> parents, Dictionary<string, Transform> transforms,
            Animation animation, Dictionary<int, Timeline> timelines, MainlineKey key,
            Dictionary<string, SpatialInfo> defaultBones, Dictionary<string, SpatialInfo> defaultActionPoints,
            SpriterEntityInfo entityInfo)
        {
            foreach (var oref in key.objectRefs)
            {
                var timeline = timelines[oref.timelineId];

                SpriterObjectInfo spriterObjectInfo;
                entityInfo.objectInfos.TryGetValue(timeline.name, out spriterObjectInfo);

                if (spriterObjectInfo == null || spriterObjectInfo.type != ObjectType.point)
                {   // Don't log a warning if this was one of the unsupported Spriter object types.
                    if (spriterObjectInfo == null)
                    {
                        Debug.LogWarning($"Stui: ProcessActionPoints() was unable to find object info for action point '{timeline.name}'.");
                    }

                    continue;
                }

                if (transforms.ContainsKey(timeline.name))
                {
                    continue;
                }

                var parentName = parents[oref.parentRefId];
                var parentTransform = transforms[parentName];

                ProcessVirtualParent(parentName, ref parentTransform, spriterObjectInfo);

                // 'child' is the name without a suffix, so will not be the virtual parent, if any.
                var child = parentTransform.Find(timeline.name);
                if (child == null)
                {
                    child = new GameObject(timeline.name).transform;
                }

                child.SetParent(parentTransform);
                transforms[timeline.name] = child; // Note that virtual parents and renderers w/ a pivot parent aren't added to this.

                var pointInfo = defaultActionPoints[timeline.name] = timeline.keys[0].info;

                if (!pointInfo.haveBaked && SpriterEntityInfo.IsBakedBoneOrObject(spriterObjectInfo, animation))
                {
                    SpatialInfo parentInfo;
                    defaultBones.TryGetValue(parentName, out parentInfo); // 'parentName' may be grandparent if a virtual parent was created.
                    pointInfo.Bake(parentInfo);
                }

                // The transform gets initialized with the baked or unbaked, regardless.  If a Spatial Adapter
                // is used then it will initialize the position and scale when it is enabled.
                child.localPosition = new Vector3(pointInfo.x, pointInfo.y, 0f);
                child.localEulerAngles = new Vector3(0f, 0f, pointInfo.angle);
                child.localScale = new Vector3(pointInfo.scale_x, pointInfo.scale_y, 1f);

                SpatialAdapter spatialAdapter;
                child.gameObject.TryGetComponent(out spatialAdapter);

                if (SpriterEntityInfo.UseTransformForPositionAndScale(spriterObjectInfo))
                {   // The Transform's Position and Scale are always used for animating this action point and they
                    // will always be baked.  Remove any preexisting Spatial Adapter.
                    if (spatialAdapter != null)
                    {
                        DestroyImmediate(spatialAdapter);
                    }
                }
                else
                {   // The SpatialAdapter's Position and Scale are always used for animating this action point.  The
                    // position and scale may be baked or not, depending on the animation.
                    if (spatialAdapter == null)
                    {
                        spatialAdapter = child.gameObject.AddComponent<SpatialAdapter>();
                    }

                    spatialAdapter.Position = new Vector2(pointInfo.x, pointInfo.y);
                    spatialAdapter.Scale = new Vector2(pointInfo.scale_x, pointInfo.scale_y);

                    spriterObjectInfo.spatialAdapter = spatialAdapter;
                }

                ProcessObjectScopedMetadata(child, spriterObjectInfo);
            }
        }

        private void ProcessCollisionRectangles(Dictionary<int, string> parents, Dictionary<string, Transform> transforms,
            Animation animation, Dictionary<int, Timeline> timelines, MainlineKey key,
            Dictionary<string, SpatialInfo> defaultBones, Dictionary<string, SpriteInfo> defaultCollisionRectangles,
            SpriterEntityInfo entityInfo, Entity entity, bool firstAnim)
        {
            foreach (var oref in key.objectRefs)
            {
                var timeline = timelines[oref.timelineId];

                SpriterObjectInfo spriterObjectInfo;
                entityInfo.objectInfos.TryGetValue(timeline.name, out spriterObjectInfo);

                if (spriterObjectInfo == null || spriterObjectInfo.type != ObjectType.box)
                {   // Don't log a warning if this was one of the unsupported Spriter object types.
                    if (spriterObjectInfo == null)
                    {
                        Debug.LogWarning($"Stui: ProcessCollisionRectangles() was unable to find object info for " +
                            $"collision rectangle '{timeline.name}'.");
                    }

                    continue;
                }

                if (transforms.ContainsKey(timeline.name))
                {
                    continue;
                }

                var parentName = parents[oref.parentRefId];
                var parentTransform = transforms[parentName];

                ProcessVirtualParent(parentName, ref parentTransform, spriterObjectInfo);

                var child = parentTransform.Find(timeline.name);
                if (child == null)
                {
                    child = new GameObject(timeline.name).transform;
                }

                child.SetParent(parentTransform);
                transforms[timeline.name] = child; // Note that virtual parents and colliders w/ a pivot parent aren't added to this.

                var boxInfo = defaultCollisionRectangles[timeline.name] = (SpriteInfo)timeline.keys[0].info;

                if (!boxInfo.haveBaked && SpriterEntityInfo.IsBakedBoneOrObject(spriterObjectInfo, animation))
                {
                    SpatialInfo parentInfo;
                    defaultBones.TryGetValue(parentName, out parentInfo); // 'parentName' may be grandparent if a virtual parent was created.
                    boxInfo.Bake(parentInfo);
                }

                // The transform gets initialized with the baked or unbaked, regardless.  If a Spatial Adapter
                // is used then it will initialize the position and scale when it is enabled.
                child.localPosition = new Vector3(boxInfo.x, boxInfo.y, 0f);
                child.localEulerAngles = new Vector3(0f, 0f, boxInfo.angle);
                child.localScale = new Vector3(boxInfo.scale_x, boxInfo.scale_y, 1f);

                SpatialAdapter spatialAdapter;
                child.gameObject.TryGetComponent(out spatialAdapter);

                if (SpriterEntityInfo.UseTransformForPositionAndScale(spriterObjectInfo))
                {   // The Transform's Position and Scale are always used for animating this collision rectangle and
                    // they will always be baked.  Remove any preexisting Spatial Adapter.
                    if (spatialAdapter != null)
                    {
                        DestroyImmediate(spatialAdapter);
                    }
                }
                else
                {   // The SpatialAdapter's Position and Scale are always used for animating this collision rectangle.
                    // The position and scale may be baked or not, depending on the animation.
                    if (spatialAdapter == null)
                    {
                        spatialAdapter = child.gameObject.AddComponent<SpatialAdapter>();
                    }

                    spatialAdapter.Position = new Vector2(boxInfo.x, boxInfo.y);
                    spatialAdapter.Scale = new Vector2(boxInfo.scale_x, boxInfo.scale_y);

                    spriterObjectInfo.spatialAdapter = spatialAdapter;
                }

                ProcessObjectScopedMetadata(child, spriterObjectInfo);

                var rigidBody2D = child.GetOrAddComponent<Rigidbody2D>();

                rigidBody2D.bodyType = RigidbodyType2D.Kinematic;
                rigidBody2D.simulated = true;
                rigidBody2D.useFullKinematicContacts = true;
                rigidBody2D.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
                rigidBody2D.sleepMode = RigidbodySleepMode2D.NeverSleep;
                rigidBody2D.interpolation = RigidbodyInterpolation2D.None;

                var colliderInfo = (
                    from objInfo in entity.objectInfos
                    where objInfo.name == timeline.name && objInfo.objectType == ObjectType.box
                    select objInfo
                )
                .FirstOrDefault();

                if (colliderInfo == null)
                {
                    Debug.LogWarning("Stui: ProcessCollisionRectangles() was unable to find an ObjectInfo entry " +
                        $"for collision rectangle '{timeline.name}'.");
                }

                var collider = child.GetOrAddComponent<BoxCollider2D>();

                collider.isTrigger = true;
                collider.offset = colliderInfo != null
                    ? new Vector2(
                        (0.5f - boxInfo.pivot_x) * colliderInfo.width,
                        (0.5f - boxInfo.pivot_y) * colliderInfo.height)
                    : Vector2.zero;
                collider.size = colliderInfo != null
                    ? new Vector2(colliderInfo.width, colliderInfo.height)
                    : new Vector2(0f, 0f);

                var collisionRectangle = child.GetOrAddComponent<CollisionRectangle>();

                // Disable the collider if this isn't the first frame of the first animation
                collider.enabled = firstAnim;
            }
        }

        private void ProcessVirtualParent(string parentName, ref Transform parentTransform,
            SpriterInfoBase virtualParentInfo)
        {
            // The hierarchy for a sprite will look like the following:
            //
            //     Virtual Parent (optional)
            //     └── Pivot (optional) or Sprite Render
            //         └── Sprite Renderer w/ pivot parent
            //
            // The naming will look like one of the following examples, in this case,
            // for a sprite with a name of "lower_leg":
            //
            //     lower_leg virtual parent     lower_leg virtual parent   lower_leg
            //     └── lower_leg                └── lower_leg              └── lower_leg renderer
            //         └── lower_leg renderer
            //
            // ...or just a transform named "lower_leg" in the case where there isn't a virtual parent
            // and there isn't a pivot controller.  (This will be the most common case.)
            //
            // Bones can have virtual parents but can't have sprite renderers or pivots so their
            // hierarchy will look like this:
            //
            //     Virtual Parent (optional)
            //     └── Bone
            //
            // The bone transform will have the name of the bone from the Spriter file.  The virtual
            // parent, if any, will be named boneName + " virtual parent".
            //
            // Action points can have virtual parents but can't have sprite renderers or pivots so their
            // hierarchy will look like this:
            //
            //     Virtual Parent (optional)
            //     └── Action Point
            //
            // The action point transform will have the name of the action point from the Spriter file.  The virtual
            // parent, if any, will be named pointName + " virtual parent".

            if (virtualParentInfo.hasVirtualParent)
            {   // Find or create a transform for the virtual parent.

                if (virtualParentInfo.parentBoneNames[0] != parentName)
                {   // The bone/sprite/point's first parent isn't the bone that would actually be its parent when the
                    // character is in the bind/default pose.  (The "bind pose" refers to the character’s
                    // default bone and sprite positions, etc., which are defined by the first frame of the
                    // entity's first animation.)  Make it the first so that it shows up at index zero of
                    // the virtual parent component's 'possibleParents' list.
                    var swapIdx = virtualParentInfo.parentBoneNames.FindIndex(x => x == parentName);

                    var tmp = virtualParentInfo.parentBoneNames[0];
                    virtualParentInfo.parentBoneNames[0] = virtualParentInfo.parentBoneNames[swapIdx];
                    virtualParentInfo.parentBoneNames[swapIdx] = tmp;
                }

                var virtualParentTransform = parentTransform.Find(virtualParentInfo.virtualParentTransformName);
                if (virtualParentTransform == null)
                {
                    virtualParentTransform = new GameObject(virtualParentInfo.virtualParentTransformName).transform;
                }

                virtualParentInfo.virtualParentTransform = virtualParentTransform; // Post processing needs this.

                virtualParentTransform.SetParent(parentTransform, false);
                parentTransform = virtualParentTransform; // The virtual parent will be the next stage's parent.

                var virtualParentComponent = virtualParentTransform.GetOrAddComponent<VirtualParent>();

                virtualParentComponent.PossibleParents.Clear();
                virtualParentComponent.ParentIndex = 0; // We know this is the bind pose parent index from above.
            }
            else
            {   // If a virtual parent exists, remove it.
                var virtualParentTransform = parentTransform.Find(virtualParentInfo.virtualParentTransformName);
                if (virtualParentTransform != null)
                {
                    DestroyImmediate(virtualParentTransform);
                }
            }
        }

        private void SetTextureImportSettings(string path, File file)
        {
            var importer = TextureImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {   // If no TextureImporter exists, there's no texture to be found
                return;
            }

            bool requiresSettingsUpdate =
                importer.textureType != TextureImporterType.Sprite
                || importer.spritePivot.x != file.pivot_x
                || importer.spritePivot.y != file.pivot_y
                || importer.spriteImportMode != SpriteImportMode.Single
                || (ScmlImportOptions.options != null && importer.spritePixelsPerUnit != ScmlImportOptions.options.pixelsPerUnit);

            if (requiresSettingsUpdate)
            {
                // Make sure the texture has the required settings...

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);

                settings.ApplyTextureType(TextureImporterType.Sprite);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(file.pivot_x, file.pivot_y);

                if (ScmlImportOptions.options != null)
                {
                    settings.spritePixelsPerUnit = ScmlImportOptions.options.pixelsPerUnit;
                }

                importer.SetTextureSettings(settings);

                importer.spriteImportMode = SpriteImportMode.Single; // Set this last!  It won't work in some cases otherwise.

                importer.SaveAndReimport();
            }
        }

        public static Sprite GetSpriteAtPath(string path, bool logWarning = true)
        {
            Sprite result = (Sprite)AssetDatabase.LoadAssetAtPath(path, typeof(Sprite));

            if (result == null && logWarning)
            {
                Debug.LogWarning($"The Spriter .scml file references a sprite at '{path}' but it was not found.  " +
                    "The sprite may not be needed so the import will continue.");
            }

            return result;
        }
    }
}
