// Copyright (c) 2015 The original author or authors
//
// This software may be modified and distributed under the terms
// of the zlib license.  See the LICENSE file for details.

using SpriterDotNet;
using System;
using UnityEngine;

namespace SpriterDotNetUnity
{
    [Serializable]
    public class ChildData
    {
        public GameObject[] SpritePivots;
        public GameObject[] Sprites;
        public GameObject[] BoxPivots;
        public GameObject[] Boxes;
        public GameObject[] Points;
    }

    [ExecuteInEditMode]
    public class SpriterDotNetBehaviour : MonoBehaviour
    {
        [HideInInspector]
        public ChildData ChildData;

        [HideInInspector]
        public int EntityIndex;

        [HideInInspector]
        public SpriterData SpriterData;

        [HideInInspector]
        public bool UseNativeTags;

        public UnitySpriterAnimator SpriterAnimator { get; private set; }

        [Tooltip("The animation to load when this sprite is initially manifest")]
        public string DefaultAnimation = null;

        private string defaultTag;

        /// Is this animation currently running?
        [HideInInspector]
        public bool Running = false;

        #if UNITY_EDITOR
        public UnitySpriterAnimator EditorData() {
          if (SpriterAnimator == null) {
            InitAnimator();
            Running = false;
          }
          return SpriterAnimator;
        }
        #endif

        /// Initialize the animatior
        private void InitAnimator() {
          SpriterEntity entity = SpriterData.Spriter.Entities[EntityIndex];
          AudioSource audioSource = gameObject.GetComponent<AudioSource>();

          SpriterAnimator = new UnitySpriterAnimator(entity, ChildData, audioSource);
          RegisterSpritesAndSounds();

          if (UseNativeTags) defaultTag = gameObject.tag;

          if (!String.IsNullOrEmpty(DefaultAnimation)) {
            N.Console.Log("Trying to set animation {0}", DefaultAnimation);
            foreach (var i in SpriterAnimator.GetAnimations()) {
              N.Console.Log(i);
            }
            SpriterAnimator.Transition(DefaultAnimation, 0);
          }
          SpriterAnimator.Step(0);
        }

        public void Start() {
            Running = true;
            InitAnimator();
        }

        public void Update()
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
#endif

            if (SpriterAnimator == null) return;
            SpriterAnimator.Step(Time.deltaTime * 1000.0f);

            if (UseNativeTags)
            {
                var tags = SpriterAnimator.Metadata.AnimationTags;
                if (tags != null && tags.Count > 0) gameObject.tag = tags[0];
                else gameObject.tag = defaultTag;
            }
        }

        private void RegisterSpritesAndSounds()
        {
            foreach (SdnFileEntry entry in SpriterData.FileEntries)
            {
                if (entry.Sprite != null) SpriterAnimator.Register(entry.FolderId, entry.FileId, entry.Sprite);
                else SpriterAnimator.Register(entry.FolderId, entry.FileId, entry.Sound);
            }
        }
    }
}
