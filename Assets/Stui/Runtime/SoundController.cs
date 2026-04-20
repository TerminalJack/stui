// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spriter2UnityDX
{
    [Serializable]
    public class SpriterSoundItem
    {
        public string soundItemName;

        public string soundlineName;
        public string animationName;
        public float time;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Range(-1f, 1f)]
        public float panning = 0f;

        public AudioClip audioClip;

        public override bool Equals(object obj)
        {
            if (obj is SpriterSoundItem other)
            {
                return this.soundlineName == other.soundlineName
                    && this.animationName == other.animationName
                    && this.time == other.time;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(soundlineName, animationName, time);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class SoundController : MonoBehaviour
    {
        public List<SpriterSoundItem> soundItems = new List<SpriterSoundItem>();

        private AudioSource _audioSource;

        void OnEnable()
        {
            _audioSource = GetComponent<AudioSource>();

            if (_audioSource == null)
            {
                Debug.LogWarning($"SoundController.Awake(): An AudioSource wasn't found.");
            }
        }

        public void SoundController_PlaySound(int soundIdx)
        {
            // Note: The name of this method is meant to be unique to this component so that it can be easily found
            // during reimports and removed from animations (while leaving user-defined animation events alone.)

            if (_audioSource != null)
            {
                if (soundIdx >= 0 && soundIdx < soundItems.Count)
                {
                    var soundItem = soundItems[soundIdx];

                    _audioSource.panStereo = soundItem.panning;
                    _audioSource.PlayOneShot(soundItem.audioClip, soundItem.volume);
                }
                else
                {
                    Debug.LogWarning($"SoundController.PlaySound(): The soundIdx parameter value ({soundIdx}) is out-of-bounds.");
                }
            }
        }
    }
}
