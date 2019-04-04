using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline.Signals
{
    class SignalInspectorList : SignalList
    {
        SerializedProperty signalAssetContext { get; set; }

        public SignalInspectorList(TreeViewState state, MultiColumnHeader multiColumnHeader, SignalReceiver receiver)
            : base(state, multiColumnHeader, receiver) {}

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(-1, -1) { children = new List<TreeViewItem>() };

            var matchingId = signalAssetContext != null ? FindIdForSignal(signals, signalAssetContext) : -1;
            if (matchingId >= 0)
                AddItem(root, matchingId);

            for (var i = 0; i < signals.arraySize; ++i)
            {
                if (i == matchingId) continue;
                AddItem(root, i, false);
            }

            return root;
        }

        void AddItem(TreeViewItem root, int id, bool enabled = true)
        {
            var signal = signals.GetArrayElementAtIndex(id);
            var evt = events.GetArrayElementAtIndex(id);
            root.children.Add(new SignalInspectorItem(signal, evt, enabled, id));
        }

        public void RefreshData(SerializedProperty signalsProperty, SerializedProperty eventsProperty, SerializedProperty assetContext = null)
        {
            signals = signalsProperty;
            events = eventsProperty;
            signalAssetContext = assetContext;

            Reload();
        }
    }
}
