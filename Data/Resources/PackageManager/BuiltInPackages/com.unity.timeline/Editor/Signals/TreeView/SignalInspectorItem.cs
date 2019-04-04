using System;
using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline.Signals
{
    class SignalInspectorItem : SignalListItem
    {
        bool enabled { get; }

        public SignalInspectorItem(SerializedProperty signalAsset, SerializedProperty eventListEntry, bool enabled, int id)
            : base(signalAsset, eventListEntry, id)
        {
            this.enabled = enabled;
        }

        protected override void DrawSignalNameColumn(Rect rect, float padding, SignalReceiver target, int rowIdx)
        {
            var prevState = GUI.enabled;
            GUI.enabled = enabled;

            var signalAsset = m_Asset.objectReferenceValue;
            GUI.Label(rect, signalAsset != null ? EditorGUIUtility.TrTempContent(signalAsset.name) : Styles.EmptySignalList);

            GUI.enabled = prevState;
        }

        protected override void DrawReactionColumn(Rect rect)
        {
            var prevState = GUI.enabled;
            GUI.enabled = enabled;
            base.DrawReactionColumn(rect);
            GUI.enabled = prevState;
        }

        protected override void DrawOptionsColumn(Rect rect, int rowIdx, SignalReceiver target)
        {}
    }
}
