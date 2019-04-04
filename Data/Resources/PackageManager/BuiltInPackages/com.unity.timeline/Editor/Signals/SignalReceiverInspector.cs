using UnityEngine;
using UnityObject = UnityEngine.Object;
using UnityEditor.IMGUI.Controls;
using UnityEngine.Events;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline.Signals
{
    [CustomEditor(typeof(SignalReceiver))]
    class SignalReceiverInspector : Editor
    {
        SignalReceiver m_Target;
        SerializedProperty m_EventsProperty;
        SerializedProperty m_SignalNameProperty;

        [SerializeField] TreeViewState m_TreeState;
        [SerializeField] MultiColumnHeaderState m_MultiColumnHeaderState;
        internal SignalReceiverTreeView m_TreeView;

        void OnEnable()
        {
            m_Target = target as SignalReceiver;
            m_SignalNameProperty = SignalReceiverUtility.FindSignalsProperty(serializedObject);
            m_EventsProperty = SignalReceiverUtility.FindEventsProperty(serializedObject);
            InitTreeView(m_SignalNameProperty, m_EventsProperty);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            m_TreeView.RefreshIfDirty();
            DrawEmitterControls(); // Draws buttons coming from the Context (SignalEmitter)

            m_TreeView.Draw();

            if (!m_Context)
                DrawAddRemoveButtons();

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                m_TreeView.dirty = true;
            }
        }

        void DrawEmitterControls()
        {
            if (m_Context != null)
            {
                var currentSignal = (m_Context as SignalEmitter).asset;
                if (currentSignal != null && !m_Target.IsSignalAssetHandled(currentSignal))
                {
                    EditorGUILayout.Separator();
                    var message = string.Format("No reaction for {0} has been defined in this receiver",
                        currentSignal.name);
                    SignalUtility.DrawCenteredMessage(message);
                    SignalUtility.DrawCenteredButton(Styles.AddReactionButton,
                        () => m_Target.AddNewReaction(currentSignal)); // Add reaction on the first
                    EditorGUILayout.Separator();
                }
            }
        }

        internal void SetAssetContext(SignalAsset asset)
        {
            m_TreeView.SetSignalContext(asset);
        }

        void DrawAddRemoveButtons()
        {
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(Styles.AddReactionButton))
                {
                    Undo.RegisterCompleteObjectUndo(m_Target, Styles.UndoAddReaction);
                    m_Target.AddEmptyReaction(new UnityEvent());
                }
                GUILayout.Space(18.0f);
            }

            EditorGUILayout.Separator();
        }

        void InitTreeView(SerializedProperty signals, SerializedProperty events)
        {
            m_TreeState = SignalListFactory.CreateViewState();
            m_MultiColumnHeaderState = SignalListFactory.CreateHeaderState();

            m_TreeView = SignalListFactory.CreateSignalInspectorList(m_TreeState, m_MultiColumnHeaderState, target as SignalReceiver, SignalReceiverUtility.headerHeight, m_Context != null);
            m_TreeView.SetSerializedProperties(signals, events);
            if (m_Context != null)
            {
                m_TreeView.SetSignalContext((m_Context as SignalEmitter).asset);
            }
        }
    }
}
