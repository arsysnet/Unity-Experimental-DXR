using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace UnityEditor.Timeline.Signals
{
    class SignalListItem : TreeViewItem, ISignalAssetProvider
    {
        static readonly SignalEventDrawer k_EvtDrawer = new SignalEventDrawer();

        protected SerializedProperty m_Asset;
        SerializedProperty m_Evt;
        bool m_ShouldRefreshParent;

        int m_CurrentRowIdx;
        SignalReceiver m_CurrentReceiver;

        public SignalListItem(SerializedProperty signalAsset, SerializedProperty eventListEntry, int id)
            : base(id, 0)
        {
            m_Asset = signalAsset;
            m_Evt = eventListEntry;
        }

        public SignalAsset signalAsset
        {
            get { return m_CurrentReceiver.GetSignalAssetAtIndex(m_CurrentRowIdx); }
            set
            {
                Undo.RegisterCompleteObjectUndo(m_CurrentReceiver, Styles.UndoCreateSignalAsset);
                m_CurrentReceiver.ChangeSignalAtIndex(m_CurrentRowIdx, value);
            }
        }

        public float GetHeight()
        {
            return k_EvtDrawer.GetPropertyHeight(m_Evt, EditorGUIUtility.TrTempContent(string.Empty));
        }

        public void Draw(Rect rect, int colIdx, int rowIdx, float padding, SignalReceiver target)
        {
            switch (colIdx)
            {
                case 0:
                    DrawSignalNameColumn(rect, padding, target, rowIdx);
                    break;
                case 1:
                    DrawReactionColumn(rect);
                    break;
                case 2:
                    DrawOptionsColumn(rect, rowIdx, target);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected virtual void DrawSignalNameColumn(Rect rect, float padding, SignalReceiver target, int rowIdx)
        {
            m_CurrentRowIdx = rowIdx;
            m_CurrentReceiver = target;

            rect.x += padding;
            rect.width -= padding;
            rect.height = EditorGUIUtility.singleLineHeight;
            SignalUtility.DrawSignalNames(this, rect, GUIContent.none, false);
        }

        protected virtual void DrawReactionColumn(Rect rect)
        {
            var nameAsString = m_Asset.objectReferenceValue == null ? "Null" : m_Asset.objectReferenceValue.name;
            using (var change = new EditorGUI.ChangeCheckScope())
            {
                EditorGUI.PropertyField(rect, m_Evt, EditorGUIUtility.TrTextContent(nameAsString));
                if (change.changed)
                    m_ShouldRefreshParent = true;
            }
        }

        protected virtual void DrawOptionsColumn(Rect rect, int rowIdx, SignalReceiver target)
        {
            rect.height = Styles.OptionsStyle.normal.background.height;
            rect.width = Styles.OptionsStyle.normal.background.width;

            if (GUI.Button(rect, Styles.OptionsStyle.normal.background, GUIStyle.none))
            {
                var menu = new GenericMenu();
                menu.AddItem(EditorGUIUtility.TrTextContent(Styles.SignalListDuplicateOption), false, () =>
                {
                    var evtCloner = ScriptableObject.CreateInstance<UnityEventCloner>();
                    evtCloner.evt = target.GetReactionAtIndex(rowIdx);
                    var clone = Object.Instantiate(evtCloner);
                    target.AddEmptyReaction(clone.evt);
                    m_ShouldRefreshParent = true;
                });
                menu.AddItem(EditorGUIUtility.TrTextContent(Styles.SignalListDeleteOption), false, () =>
                {
                    target.RemoveAtIndex(rowIdx);
                    m_ShouldRefreshParent = true;
                });
                menu.ShowAsContext();
            }
        }

        public bool ShouldRefreshParent()
        {
            var result = m_ShouldRefreshParent;
            m_ShouldRefreshParent = false;
            return result;
        }

        IEnumerable<SignalAsset> ISignalAssetProvider.AvailableSignalAssets()
        {
            var ret = SignalManager.assets.Except(m_CurrentReceiver.GetRegisteredSignals());
            return signalAsset == null ? ret : ret.Union(new List<SignalAsset> {signalAsset}).ToList();
        }

        void ISignalAssetProvider.CreateNewSignalAsset(string path)
        {
            var newSignalAsset = SignalManager.CreateSignalAssetInstance(path);
            m_CurrentReceiver.ChangeSignalAtIndex(m_CurrentRowIdx, newSignalAsset);
            AssetDatabase.CreateAsset(newSignalAsset, path);
            GUIUtility.ExitGUI();
        }

        class UnityEventCloner : ScriptableObject
        {
            public UnityEvent evt;
        }
    }
}
