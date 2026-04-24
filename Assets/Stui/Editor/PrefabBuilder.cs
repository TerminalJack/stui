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
    using Unity.VisualScripting;

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

            var transforms = new Dictionary<string, Transform>(); //All of the bones and sprites, identified by Timeline.name, because those are truly unique
            transforms["rootTransform"] = instance.transform; //The root GameObject needs to be part of this hierarchy as well

            var defaultBones = new Dictionary<string, SpatialInfo>();  // These are basically the object states on the first frame of the first animation
            var defaultSprites = new Dictionary<string, SpriteInfo>(); // They are used as control values in determining whether something has changed
            var defaultActionPoints = new Dictionary<string, SpatialInfo>();

            var animBuilder = new AnimationBuilder(ProcessingInfo, folders, transforms, defaultBones, defaultSprites, defaultActionPoints, prefabPath, controller, entityInfo);
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

            foreach (var animation in entity.animations)
            {
                buildCtx.AnimationName = animation.name;

                if (buildCtx.IsCanceled) { yield break; }
                yield return $"{buildCtx.MessagePrefix}: processing";

                var timeLines = new Dictionary<int, Timeline>();
                foreach (var timeLine in animation.timelines) // Timelines hold all the critical data such as positioning and graphics used
                {
                    timeLines[timeLine.id] = timeLine;
                }

                foreach (var key in animation.mainlineKeys)
                {
                    var parents = new Dictionary<int, string>(); //Parents are referenced by different IDs V_V
                    parents[-1] = "rootTransform"; //This is where "-1 == no parent" comes in handy

                    if (buildCtx.IsCanceled) { yield break; }
                    yield return $"{buildCtx.MessagePrefix}, mainline key time: {key.time_s}, processing bones";

                    ProcessBones(parents, transforms, timeLines, key, defaultBones, entityInfo);

                    if (buildCtx.IsCanceled) { yield break; }
                    yield return $"{buildCtx.MessagePrefix}, mainline key time: {key.time_s}, processing sprites";

                    ProcessSprites(parents, transforms, timeLines, key, defaultBones, defaultSprites, entityInfo, folders, firstAnim);

                    if (buildCtx.IsCanceled) { yield break; }
                    yield return $"{buildCtx.MessagePrefix}, mainline key time: {key.time_s}, processing action points";

                    ProcessActionPoints(parents, transforms, timeLines, key, defaultActionPoints, entityInfo);

                    firstAnim = false;
                }

                var animBuildProcess =
                    IteratorUtils.SafeEnumerable(
                        () => animBuilder.Build(animation, timeLines, buildCtx),
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

                var floatComponent = varTransform.gameObject.GetComponent<SpriterFloat>();
                if (floatComponent)
                {
                    DestroyImmediate(floatComponent);
                }

                var intComponent = varTransform.gameObject.GetComponent<SpriterInt>();
                if (intComponent)
                {
                    DestroyImmediate(intComponent);
                }

                var stringComponent = varTransform.gameObject.GetComponent<SpriterString>();
                if (stringComponent)
                {
                    DestroyImmediate(stringComponent);
                }

                // Create the appropriate variable component...
                switch (varDef.type)
                {
                    case VarType.Float:
                        var floatVarComponent = varTransform.gameObject.AddComponent<SpriterFloat>();
                        floatVarComponent.variableName = varDef.name;
                        if (!float.TryParse(varDef.defaultValue, out floatVarComponent.defaultValue))
                        {
                            floatVarComponent.defaultValue = 0f;
                        }

                        floatVarComponent.value = floatVarComponent.defaultValue;
                        break;

                    case VarType.Int:
                        var intVarComponent = varTransform.gameObject.AddComponent<SpriterInt>();
                        intVarComponent.variableName = varDef.name;
                        if (!int.TryParse(varDef.defaultValue, out intVarComponent.defaultValue))
                        {
                            intVarComponent.defaultValue = -1;
                        }

                        intVarComponent.valueAsFloat = (float)intVarComponent.defaultValue;
                        break;

                    case VarType.String:
                        var stringVarComponent = varTransform.gameObject.AddComponent<SpriterString>();
                        stringVarComponent.variableName = varDef.name;
                        stringVarComponent.possibleValues = varDef.possibleStringValues.ToList();
                        stringVarComponent.valueIndex = stringVarComponent.possibleValues.Count > 0 ? 0 : -1;
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

                // Remove any preexisting tag component.
                var tagComponent = tagTransform.gameObject.GetComponent<SpriterTag>();
                if (tagComponent != null)
                {
                    DestroyImmediate(tagComponent);
                }

                // Create tag component.
                tagComponent = tagTransform.gameObject.AddComponent<SpriterTag>();

                tagComponent.tagName = tagDef.name;
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

            if (entityInfo.HasMetadata)
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

            if (objInfo.HasMetadata)
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

            var allEvents = entityInfo.objectInfo.Values.Where(o => o.type == ObjectType.spriterEvent).ToList();

            var eventControllerComponent = parentTransform.GetComponent<EventController>();

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
                    var spriterEventListenerComponent = thisEventTransform.gameObject.GetComponent<SpriterEventListener>();
                    if (spriterEventListenerComponent != null)
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
                    eventControllerComponent = parentTransform.AddComponent<EventController>();
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
            var soundController = parentTransform.GetComponent<SoundController>();

            if (soundController != null)
            {
                DestroyImmediate(soundController);
            }

            if (entityInfo.soundItems.Count > 0)
            {
                soundController = parentTransform.AddComponent<SoundController>();

                foreach (var soundItem in entityInfo.soundItems)
                {
                    soundController.soundItems.Add(soundItem);
                }
            }
        }

        private void FinalizeVirtualParentProcessing(SpriterEntityInfo entityInfo, Dictionary<string, Transform> transforms)
        {
            // Add 'possible parents' to all of the virtual parent components.
            foreach (var info in entityInfo.boneInfo.Values.Cast<SpriterInfoBase>()
                .Concat(entityInfo.objectInfo.Values))
            {
                if (info.hasVirtualParent && info.virtualParentTransform != null)
                {
                    var vp = info.virtualParentTransform.GetComponent<VirtualParent>();

                    if (vp != null)
                    {
                        vp.possibleParents.Clear();

                        foreach (var parentName in info.parentBoneNames)
                        {
                            var possibleParentTransform = transforms[parentName];
                            vp.possibleParents.Add(possibleParentTransform);
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
            var characterController = instance.GetComponent<CharacterMapController>();
            if (characterController != null)
            {
                _previousActiveMapNames = characterController.activeMapNames.ToList();
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
                var c = instance.GetComponent<CharacterMapController>();
                if (c != null)
                {
                    DestroyImmediate(c);
                }

                return;
            }

            var characterMapController = instance.GetOrAddComponent<CharacterMapController>();

            // Build characterMapController.baseMap...

            characterMapController.baseMap.Clear();

            // Note: This code here is the reason why all active maps have to be temporarily removed.
            foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(includeInactive: true))
            {
                // Map sprites to the appropriate transform and, if appropriate, the texture controller index.

                Transform targetTransform = renderer.transform;
                var textureController = targetTransform.GetComponent<TextureController>();

                if (textureController)
                {
                    for (int i = 0; i < textureController.Sprites.Length; ++i)
                    {
                        var sprite = textureController.Sprites[i];
                        characterMapController.baseMap.Add(sprite, new SpriteMapTarget(targetTransform, i));
                    }
                }
                else
                {
                    characterMapController.baseMap.Add(renderer.sprite, new SpriteMapTarget(targetTransform, 0));
                }
            }

            characterMapController.Refresh(); // Apply _just_ the base map.

            // Build characterMapController.availableMaps...

            characterMapController.availableMaps.Clear();

            foreach (var characterMap in entity.characterMaps)
            {
                var charMap = new CharacterMapping(characterMap.name);

                foreach (var mapInstruction in characterMap.maps)
                {
                    Sprite srcSprite = TryGetSprite(folders, mapInstruction.folder, mapInstruction.file);

                    if (srcSprite == null)
                    {
                        Debug.LogWarning($"Stui: ProcessCharacterMaps(): For entity '{entity.name}', " +
                            $"character map '{characterMap.name}', the source sprite at folder: {mapInstruction.folder}, " +
                            $"file: {mapInstruction.file} wasn't found.");

                        continue;
                    }

                    Sprite targetSprite = null;

                    if (mapInstruction.target_folder != -1 && mapInstruction.target_file != -1)
                    {
                        targetSprite = TryGetSprite(folders, mapInstruction.target_folder, mapInstruction.target_file);

                        if (targetSprite == null)
                        {
                            Debug.LogWarning($"Stui: ProcessCharacterMaps(): For entity '{entity.name}', " +
                                $"character map '{characterMap.name}', the target sprite at folder: {mapInstruction.folder}, " +
                                $"file: {mapInstruction.file} wasn't found.");

                            continue;
                        }
                    }

                    var spriteMapping = characterMapController.baseMap.spriteMaps.Find(s => s.sprite == srcSprite);

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
                            $"character map '{characterMap.name}', the source sprite at folder: {mapInstruction.folder}, " +
                            $"file: {mapInstruction.file} doesn't exist in the base map.");
                    }
                }

                characterMapController.availableMaps.Add(charMap);
            }

            if (_previousActiveMapNames != null)
            {
                // Remove any invalid character map names from _previousActiveMapNames (from pre-existing
                // character map controllers.)
                _previousActiveMapNames.RemoveAll(name =>
                {
                    return characterMapController.availableMaps.Find(m => m.name == name) == null;
                });

                characterMapController.activeMapNames = _previousActiveMapNames.ToList();
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
            Dictionary<int, Timeline> timeLines, MainlineKey key, Dictionary<string, SpatialInfo> defaultBones,
            SpriterEntityInfo entityInfo)
        {
            var boneRefs = new Queue<Ref>(key.boneRefs);

            while (boneRefs.Count > 0)
            {
                var bone = boneRefs.Dequeue();
                var timeLine = timeLines[bone.timeline];
                parents[bone.id] = timeLine.name;

                if (!transforms.ContainsKey(timeLine.name))
                {   //We only need to go through this once, so ignore it if it's already in the dict

                    SpriterBoneInfo spriterBoneInfo;
                    entityInfo.boneInfo.TryGetValue(timeLine.name, out spriterBoneInfo);

                    if (spriterBoneInfo == null || spriterBoneInfo.type != ObjectType.bone)
                    {
                        Debug.LogWarning($"Stui: ProcessBones() was unable to find bone info for bone '{timeLine.name}'.");
                        continue;
                    }

                    if (parents.ContainsKey(bone.parent))
                    {   //If the parent cannot be found, it will probably be found later, so save it
                        var parentName = parents[bone.parent];
                        var parentTransform = transforms[parentName];

                        ProcessVirtualParent(parentName, ref parentTransform, spriterBoneInfo);

                        var child = parentTransform.Find(timeLine.name); //Try to find the child transform if it exists
                        if (child == null)
                        {   //Or create a new one
                            child = new GameObject(timeLine.name).transform;
                            child.SetParent(parentTransform);
                        }

                        transforms[timeLine.name] = child;
                        var spatialInfo = defaultBones[timeLine.name] = timeLine.keys.Find(x => x.id == bone.key).info;

                        if (!spatialInfo.processed)
                        {
                            SpatialInfo parentInfo;
                            defaultBones.TryGetValue(parentName, out parentInfo); // 'parentName' may be grandparent if a virtual parent was created.
                            spatialInfo.Process(parentInfo);
                        }

                        child.localPosition = new Vector3(spatialInfo.x, spatialInfo.y, 0f);
                        child.localRotation = Quaternion.Euler(0, 0, spatialInfo.angle);
                        child.localScale = new Vector3(spatialInfo.scale_x, spatialInfo.scale_y, 1f);

                        ProcessObjectScopedMetadata(child, spriterBoneInfo);
                    }
                    else
                    {
                        boneRefs.Enqueue(bone);
                    }
                }
            }
        }

        private void ProcessSprites(Dictionary<int, string> parents, Dictionary<string, Transform> transforms,
            Dictionary<int, Timeline> timeLines, MainlineKey key, Dictionary<string, SpatialInfo> defaultBones,
            Dictionary<string, SpriteInfo> defaultSprites, SpriterEntityInfo entityInfo,
            IDictionary<int, IDictionary<int, Sprite>> folders, bool firstAnim)
        {
            foreach (var oref in key.objectRefs)
            {
                var timeLine = timeLines[oref.timeline];

                SpriterObjectInfo spriterObjectInfo;
                entityInfo.objectInfo.TryGetValue(timeLine.name, out spriterObjectInfo);

                if (spriterObjectInfo == null || spriterObjectInfo.type != ObjectType.sprite)
                {   // Don't log a warning if this was one of the unsupported Spriter object types.
                    if (spriterObjectInfo == null)
                    {
                        Debug.LogWarning($"Stui: ProcessSprites() was unable to find object info for sprite '{timeLine.name}'.");
                    }

                    continue;
                }

                if (transforms.ContainsKey(timeLine.name))
                {
                    continue;
                }

                var parentName = parents[oref.parent];
                var parentTransform = transforms[parentName];

                ProcessVirtualParent(parentName, ref parentTransform, spriterObjectInfo);

                // 'child' is the name without a suffix, so will be a pivot or a renderer (without a pivot parent.)
                var child = parentTransform.Find(timeLine.name);
                if (child == null)
                {
                    child = new GameObject(timeLine.name).transform;
                }

                child.SetParent(parentTransform);
                transforms[timeLine.name] = child; // Note that virtual parents and renderers w/ a pivot parent aren't added to this.

                var spriteInfo = defaultSprites[timeLine.name] = (SpriteInfo)timeLine.keys[0].info;

                if (!spriteInfo.processed)
                {
                    SpatialInfo parentInfo;
                    defaultBones.TryGetValue(parentName, out parentInfo); // 'parentName' may be grandparent if a virtual parent was created.
                    spriteInfo.Process(parentInfo);
                }

                // If this sprite (for any animation of the entity) has one or more non-default
                // pivots then a pivot controller will need to be created for it.  Otherwise,
                // don't create one and be sure to remove any that might already exist.

                bool needsPivotController = spriterObjectInfo.hasPivotController;

                var pivotController = child.GetComponent<DynamicPivot2D>();

                if (needsPivotController)
                {
                    if (pivotController == null)
                    {
                        pivotController = child.gameObject.AddComponent<DynamicPivot2D>();
                    }

                    pivotController.pivot = new Vector2(spriteInfo.pivot_x, spriteInfo.pivot_y);
                }
                else if (pivotController != null)
                {
                    DestroyImmediate(pivotController);
                }

                child.localPosition = new Vector3(spriteInfo.x, spriteInfo.y, spriteInfo.z_index); // Z-index (sprite sorting order / -10000f) is stored in z.
                child.localEulerAngles = new Vector3(0f, 0f, spriteInfo.angle);
                child.localScale = new Vector3(spriteInfo.scale_x, spriteInfo.scale_y, 1f);

                ProcessObjectScopedMetadata(child, spriterObjectInfo);

                // Get or create a Sorting Order Updater.  If a pivot controller was created then it must be on the
                // same game object.
                child.GetOrAddComponent<SortingOrderUpdater>();

                // If a pivot controller is used then the sprite renderer has to go on a child game object.
                string spriteRendererName = spriterObjectInfo.spriteRenderTransformName;
                var rendererTransform = needsPivotController ? child.Find(spriteRendererName) : child;

                if (needsPivotController && rendererTransform == null)
                {
                    rendererTransform = new GameObject(spriteRendererName).transform;
                    rendererTransform.SetParent(child);
                }

                var renderer = rendererTransform.GetOrAddComponent<SpriteRenderer>(); // Get or create a Sprite Renderer

                renderer.sprite = folders[spriteInfo.folder][spriteInfo.file];
                renderer.sortingOrder = spriteInfo.SortingOrder;

                if (needsPivotController)
                {
                    rendererTransform.localPosition = Vector3.zero; // The pivot script will adjust this.
                    rendererTransform.localEulerAngles = Vector3.zero;
                    rendererTransform.localScale = Vector3.one;
                }

                var color = renderer.color;
                color.a = spriteInfo.a;
                renderer.color = color;

                var spriteVisibility = rendererTransform.GetOrAddComponent<SpriteVisibility>();

                // Disable the Sprite Renderer if this isn't the first frame of the first animation
                renderer.enabled = firstAnim;
                spriteVisibility.isVisible = firstAnim ? 1f : 0f;
            }
        }

        private void ProcessActionPoints(Dictionary<int, string> parents, Dictionary<string, Transform> transforms,
            Dictionary<int, Timeline> timeLines, MainlineKey key, Dictionary<string, SpatialInfo> defaultActionPoints,
            SpriterEntityInfo entityInfo)
        {
            foreach (var oref in key.objectRefs)
            {
                var timeLine = timeLines[oref.timeline];

                SpriterObjectInfo spriterObjectInfo;
                entityInfo.objectInfo.TryGetValue(timeLine.name, out spriterObjectInfo);

                if (spriterObjectInfo == null || spriterObjectInfo.type != ObjectType.point)
                {   // Don't log a warning if this was one of the unsupported Spriter object types.
                    if (spriterObjectInfo == null)
                    {
                        Debug.LogWarning($"Stui: ProcessActionPoints() was unable to find object info for action point '{timeLine.name}'.");
                    }

                    continue;
                }

                if (transforms.ContainsKey(timeLine.name))
                {
                    continue;
                }

                var parentName = parents[oref.parent];
                var parentTransform = transforms[parentName];

                ProcessVirtualParent(parentName, ref parentTransform, spriterObjectInfo);

                // 'child' is the name without a suffix, so will not be the virtual parent, if any.
                var child = parentTransform.Find(timeLine.name);
                if (child == null)
                {
                    child = new GameObject(timeLine.name).transform;
                }

                child.SetParent(parentTransform);
                transforms[timeLine.name] = child; // Note that virtual parents and renderers w/ a pivot parent aren't added to this.

                var pointInfo = defaultActionPoints[timeLine.name] = timeLine.keys[0].info;

                child.localPosition = new Vector3(pointInfo.x, pointInfo.y, 0f);
                child.localEulerAngles = new Vector3(0f, 0f, pointInfo.angle);
                child.localScale = new Vector3(pointInfo.scale_x, pointInfo.scale_y, 1f);

                ProcessObjectScopedMetadata(child, spriterObjectInfo);
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

                virtualParentInfo.virtualParentTransform = virtualParentTransform; // Post proccessing needs this.

                virtualParentTransform.SetParent(parentTransform, false);
                parentTransform = virtualParentTransform; // The virtual parent will be the next stage's parent.

                var virtualParentComponent = virtualParentTransform.GetOrAddComponent<VirtualParent>();

                virtualParentComponent.possibleParents.Clear();
                virtualParentComponent.parentIndex = 0; // We know this is the bind pose parent index from above.
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

        public static Sprite GetSpriteAtPath(string path)
        {
            Sprite result = (Sprite)AssetDatabase.LoadAssetAtPath(path, typeof(Sprite));
            if (result == null)
            {
                Debug.LogWarning($"The Spriter .scml file references a sprite at '{path}' but it was not found.  " +
                    "The sprite may not be needed so the import will continue.");
            }

            return result;
        }
    }
}
