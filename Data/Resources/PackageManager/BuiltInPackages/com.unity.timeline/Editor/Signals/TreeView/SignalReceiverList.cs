using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline.Signals
{
    class SignalReceiverList : SignalList
    {
        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(-1, -1) { children = new List<TreeViewItem>() };

            for (var i = 0; i < signals.arraySize; ++i)
                AddItem(root, i);

            return root;
        }

        void AddItem(TreeViewItem root, int id)
        {
            var signal = signals.GetArrayElementAtIndex(id);
            var evt = events.GetArrayElementAtIndex(id);
            root.children.Add(new SignalListItem(signal, evt, id));
        }

        public SignalReceiverList(TreeViewState state, MultiColumnHeader multiColumnHeader, SignalReceiver receiver)
            : base(state, multiColumnHeader, receiver) {}
    }
}
