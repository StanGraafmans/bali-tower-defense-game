﻿using System;
using UnityEditor;
using UnityEngine;

namespace Game.Audio.Editor
{
    public class AudioLibraryEditor
    {
        private readonly SelectableList _SelectableAudioAssetList;
        public event Action<AudioAsset> OnSelected;
        public event Action OnRequestRepaint;

        private AudioAssetLibrary _RawTarget;
        private SerializedObject _SerializedTarget;
        private SerializedProperty _MappingList;

        private Vector2 _ScrollVector;

        public AudioLibraryEditor()
        {
            _SelectableAudioAssetList = new SelectableList(DrawElement);
            _SelectableAudioAssetList.OnSelect += OnAudioAssetSelected;
        }

        public void SetTarget(AudioAssetLibrary library)
        {
            _SelectableAudioAssetList.ResetSelection();
            _RawTarget = library;
            _SerializedTarget = new SerializedObject(library);
            _MappingList = _SerializedTarget.FindProperty("_AudioAssetIndentifierMappings");
        }

        public void DoLibraryEditor()
        {
            if (_RawTarget == null)
            {
                return;
            }

            _ScrollVector = GUILayout.BeginScrollView(_ScrollVector);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{nameof(AudioLibraryEditor)}");


            if (GUILayout.Button("Nuke"))
            {
                ScriptableObject.CreateInstance<ConfirmActionPopup>().SetQuestion("You're about to nuke all elements from this library, are you sure?").OnConfirm += () => 
                    ScriptableObject.CreateInstance<ConfirmActionPopup>().SetQuestion("Delete all audio asset files from the bindings too?").OnButton += (value) =>  
                        { if(value) { DeleteAllAudioAssets(); } DeleteAllElements(); };
            }

            if (GUILayout.Button("Add"))
            {
                EditorWindow.CreateInstance<CreateAudioAssetPopup>()
                    .SetFolder("Assets/Audio/AudioAssets/")
                    .OnCreated += AddElement;
            }

            GUILayout.EndHorizontal();

            _SelectableAudioAssetList.DoList(_MappingList.arraySize, _ScrollVector);
            _SerializedTarget.ApplyModifiedProperties();
            GUILayout.EndScrollView();
        }


        private void DeleteAllAudioAssets()
        {
        }

        private void DrawElement(int index)
        {
            SerializedProperty currentMapping = _MappingList.GetArrayElementAtIndex(index);
            SerializedProperty audioAssetProperty = currentMapping.FindPropertyRelative("_AudioAsset");
            SerializedProperty audioIdentifierProperty = currentMapping.FindPropertyRelative("_Identifier");

            audioIdentifierProperty.stringValue = EditorGUILayout.TextField(audioIdentifierProperty.stringValue);
            EditorGUILayout.ObjectField(audioAssetProperty);
        }

        private void AddElement(AudioAsset asset)
        {
            int index = _MappingList.arraySize;
            _MappingList.InsertArrayElementAtIndex(_MappingList.arraySize);
            SerializedProperty currentMapping = _MappingList.GetArrayElementAtIndex(index);
            SerializedProperty audioAssetProperty = currentMapping.FindPropertyRelative("_AudioAsset");
            audioAssetProperty.objectReferenceValue = asset;
            _SerializedTarget.ApplyModifiedProperties();
            OnRequestRepaint?.Invoke();
        }

        private void DeleteAllElements()
        {
            _MappingList.ClearArray();
            _SerializedTarget.ApplyModifiedProperties();
        }

        private void OnAudioAssetSelected(int index)
        {
            if (index == -1)
            {
                OnSelected?.Invoke(null);
                return;
            }

            SerializedProperty audioAssetProperty =
                EditorScriptUtil.FromPropertyRelativeFromIndex(_MappingList, index, "_AudioAsset");

            if (!EditorScriptUtil.TryFetchPropertyGuid(audioAssetProperty, out string guid))
            {
                OnSelected?.Invoke(null);
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioAsset audioAsset = AssetDatabase.LoadAssetAtPath<AudioAsset>(path);

            OnSelected?.Invoke(audioAsset);
        }
    }
}
