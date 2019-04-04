using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline.Signals
{
    abstract class SignalList : TreeView
    {
        public bool dirty { get; set; }

        protected SerializedProperty signals { get; set; }
        protected SerializedProperty events { get; set; }

        readonly SignalReceiver m_Target;

        const float k_VerticalPadding = 5;
        const float k_HorizontalPadding = 5;

        protected SignalList(TreeViewState state, MultiColumnHeader multiColumnHeader, SignalReceiver receiver) : base(state, multiColumnHeader)
        {
            m_Target = receiver;
            useScrollView = false;
            getNewSelectionOverride = (item, selection, shift) => new List<int>(); // Disable Selection
        }

        public void RefreshData(SerializedProperty signalsProperty, SerializedProperty eventsProperty)
        {
            signals = signalsProperty;
            events = eventsProperty;

            Reload();
        }

        public void Draw()
        {
            var rect = EditorGUILayout.GetControlRect(true, GetTotalHeight());
            rect.y += 10;
            OnGUI(rect);
        }

        public void RefreshIfDirty()
        {
            if (dirty)
                Reload();
            dirty = false;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (SignalListItem)args.item;
            for (var i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                var rect = args.GetCellRect(i);
                rect.y += k_VerticalPadding;
                item.Draw(rect, args.GetColumn(i), args.row, k_HorizontalPadding, m_Target);
                dirty |= item.ShouldRefreshParent();
            }
        }

        protected override float GetCustomRowHeight(int row, TreeViewItem treeItem)
        {
            var item = treeItem as SignalListItem;
            return item.GetHeight() + k_VerticalPadding;
        }

        protected static int FindIdForSignal(SerializedProperty signals, SerializedProperty signalToFind)
        {
            for (var i = 0; i < signals.arraySize; ++i)
            {
                //signal in the receiver that matches the current signal asset will be displayed first
                var serializedProperty = signals.GetArrayElementAtIndex(i);
                var signalReferenceValue = serializedProperty.objectReferenceValue;
                var signalToFindRefValue = signalToFind.objectReferenceValue;
                if (signalReferenceValue != null && signalReferenceValue == signalToFindRefValue)
                    return i;
            }

            return -1;
        }

        float GetTotalHeight()
        {
            var height = 0.0f;
            foreach (var item in GetRows())
            {
                var signalListItem = item as SignalListItem;
                height += signalListItem.GetHeight() + k_VerticalPadding;
            }

            return height + multiColumnHeader.height;
        }

        public static MultiColumnHeaderState.Column[] GetColumns()
        {
            return new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = EditorGUIUtility.TrTextContent("Signal"),
                    contextMenuText = "",
                    headerTextAlignment = TextAlignment.Center,
                    width = 100,
                    minWidth = 100,
                    maxWidth = 100,
                    autoResize = false,
                    allowToggleVisibility = false,
                    canSort = false
                },

                new MultiColumnHeaderState.Column
                {
                    headerContent = EditorGUIUtility.TrTextContent("Reaction"),
                    contextMenuText = "",
                    headerTextAlignment = TextAlignment.Center,
                    width = 120,
                    minWidth = 50,
                    maxWidth = 5000,
                    autoResize = true,
                    allowToggleVisibility = false,
                    canSort = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent(""),
                    contextMenuText = "",
                    headerTextAlignment = TextAlignment.Center,
                    width = 0,
                    minWidth = 0,
                    maxWidth = 0,
                    autoResize = false,
                    allowToggleVisibility = false,
                    canSort = false
                }
            };
        }
    }
}
