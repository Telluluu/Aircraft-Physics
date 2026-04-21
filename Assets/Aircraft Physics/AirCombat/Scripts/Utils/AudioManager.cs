using Gamelogic;
using System;
using UnityEngine;

namespace Audio
{
    [Serializable]
    public class AudioType
    {
        [HideInInspector]
        public AudioSource source;

        public string soundName;
        public AudioClip clip;

        [Range(0.0f, 1.0f)]
        public float volume = 0.5f;

        [Range(0.1f, 5.0f)]
        public float pitch = 1.0f;

        public bool loop = false;
    }

    public class AudioManager : Singleton<AudioManager>
    {
        public int idx;

        [Header("音频列表")]
        public AudioType[] audioTypes;

        private void Start()
        {
        }

        private void OnEnable()
        {
            foreach (var audioType in audioTypes)
            {
                audioType.source = gameObject.AddComponent<AudioSource>();
                audioType.source.name = audioType.soundName;
                audioType.source.volume = audioType.volume;
                audioType.source.pitch = audioType.pitch;
                audioType.source.loop = audioType.loop;
                audioType.source.playOnAwake = false;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.V))
            {
                UpdateAudioSource();
                PlayFX(idx);
            }
        }

        public void UpdateAudioSource()
        {
            foreach (var audioType in audioTypes)
            {
                audioType.source.name = audioType.soundName;
                audioType.source.volume = audioType.volume;
                audioType.source.pitch = audioType.pitch;
                audioType.source.loop = audioType.loop;
                audioType.source.playOnAwake = false;
            }
        }

        public void PlayFX(string name)
        {
            foreach (var audioType in audioTypes)
            {
                if (audioType.soundName == name)
                {
                    audioType.source.PlayOneShot(audioType.clip);
                    return;
                }
            }
        }

        private AudioType GetAudioType(string name)
        {
            foreach (var audioType in audioTypes)
            {
                if (audioType.soundName == name)
                {
                    return audioType;
                }
            }
            return null;
        }

        public void PlayFX(int idx)
        {
            if (idx >= audioTypes.Length)
            {
                Debug.Log("超出音频列表索引");
                return;
            }
            audioTypes[idx].source.PlayOneShot(audioTypes[idx].clip);
        }
    }
}
