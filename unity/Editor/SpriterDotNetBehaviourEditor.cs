using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using N;

namespace SpriterDotNetUnity {

  /// Base class for binding inspectors
  [CustomEditor(typeof(SpriterDotNetBehaviour))]
  public class SpriterDotNetBehaviourInspector : Editor {

    /// Currently selected value
    int index = 0;

    /// The associated controller
    SpriterDotNetBehaviour data;

    /// Available options
    string[] options = null;

    public override void OnInspectorGUI() {
      base.OnInspectorGUI();
      if (options == null) { EnumerateValues(); }
      EditorGUILayout.BeginHorizontal();
      var new_index = EditorGUILayout.Popup("Animation", index, options, EditorStyles.popup);
      EditorGUILayout.EndHorizontal();
      if (index != new_index) {
        index = new_index;
        if (!data.Running) {
          N.Console.Log("Setting default to: {0}", options[index]);
          data.DefaultAnimation = options[index];
        }
        else {
          N.Console.Log("Transition to: {0}", options[index]);
          data.SpriterAnimator.Transition(options[index], 1f);
        }
      }
    }

    /// Enumerate available values
    void EnumerateValues() {
      data = (SpriterDotNetBehaviour) target;
      var animator = data.EditorData();
      options = animator.GetAnimations().ToList().ToArray();
      var current = animator.CurrentAnimation.Name;
      index = Array.IndexOf(options, current);
      N.Console.Log("Current index is: {0} ie. {1} of {2}", current, index, options.Length);
    }
  }
}
