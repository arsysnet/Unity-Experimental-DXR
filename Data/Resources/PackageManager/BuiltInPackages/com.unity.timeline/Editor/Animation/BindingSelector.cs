using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Timeline;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor
{
    class BindingSelector
    {
        TreeViewController m_TreeView;
        TreeViewState m_TreeViewState;
        BindingTreeViewDataSource m_TreeViewDataSource;
        CurveDataSource m_ClipDataSource;
        TimelineWindow m_Window;
        CurveEditor m_CurveEditor;
        ReorderableList m_DopeLines;
        string[] m_StringList = {};
        public static float kBottomPadding = 5.0f;
        int[] m_Selection;
        bool m_PartOfSelection;
        public BindingSelector(EditorWindow window, CurveEditor curveEditor)
        {
            m_Window = window as TimelineWindow;
            m_CurveEditor = curveEditor;

            m_DopeLines = new ReorderableList(m_StringList, typeof(string), false, false, false, false);
            m_DopeLines.drawElementBackgroundCallback = null;
            m_DopeLines.showDefaultBackground = false;
            m_DopeLines.index = 0;
            m_DopeLines.headerHeight = 0;
            m_DopeLines.elementHeight = 20;
            m_DopeLines.draggable = false;
        }

        public bool selectable { get { return true; } }

        public object selectableObject
        {
            get { return this; }
        }

        public bool selected
        {
            get { return m_PartOfSelection; }
            set
            {
                m_PartOfSelection = value;

                if (!m_PartOfSelection)
                {
                    m_DopeLines.index = -1;
                }
            }
        }

        public virtual void Delete(WindowState state)
        {
            // we dont support deleting the summary
            if (m_DopeLines.index < 1)
                return;

            if (m_ClipDataSource == null)
                return;

            var clip = m_ClipDataSource.animationClip;
            if (clip == null)
                return;

            int curveIndexToDelete = m_DopeLines.index - 1;
            var bindings = AnimationUtility.GetCurveBindings(clip);

            if (curveIndexToDelete >= bindings.Length)
                return;

            TimelineUndo.PushUndo(clip, "Delete Curve");
            AnimationUtility.SetEditorCurve(clip, bindings[m_DopeLines.index - 1], null);
            state.rebuildGraph = true;
        }

        public void OnGUI(Rect targetRect)
        {
            if (m_TreeView == null)
                return;

            m_TreeView.OnEvent();
            m_TreeView.OnGUI(targetRect, GUIUtility.GetControlID(FocusType.Passive));
        }

        public void InitIfNeeded(Rect rect, CurveDataSource dataSource)
        {
            if (Event.current.type != EventType.Layout)
                return;

            m_ClipDataSource = dataSource;
            var clip = dataSource.animationClip;

            List<EditorCurveBinding> allBindings = new List<EditorCurveBinding>();
            allBindings.Add(new EditorCurveBinding { propertyName = "Summary" });
            if (clip != null)
                allBindings.AddRange(AnimationUtility.GetCurveBindings(clip));

            m_DopeLines.list = allBindings.ToArray();

            if (m_TreeViewState != null)
                return;

            m_TreeViewState = new TreeViewState();

            m_TreeView = new TreeViewController(m_Window, m_TreeViewState)
            {
                useExpansionAnimation = false,
                deselectOnUnhandledMouseDown = true
            };

            m_TreeView.selectionChangedCallback += OnItemSelectionChanged;

            m_TreeViewDataSource = new BindingTreeViewDataSource(m_TreeView, clip);

            m_TreeView.Init(rect, m_TreeViewDataSource, new BindingTreeViewGUI(m_TreeView), null);

            m_TreeViewDataSource.UpdateData();

            OnItemSelectionChanged(null);
        }

        void OnItemSelectionChanged(int[] selection)
        {
            if (selection == null || selection.Length == 0)
            {
                // select all.
                if (m_TreeViewDataSource.GetRows().Count > 0)
                {
                    m_Selection = m_TreeViewDataSource.GetRows().Select(r => r.id).ToArray();
                }
            }
            else
            {
                m_Selection = selection.ToArray();
            }

            RefreshCurves();
        }

        public void RefreshCurves()
        {
            if (m_ClipDataSource == null || m_Selection == null)
                return;

            var bindings = new List<EditorCurveBinding>();
            foreach (int s in m_Selection)
            {
                var item = (CurveTreeViewNode)m_TreeView.FindItem(s);
                if (item != null && item.bindings != null)
                    bindings.AddRange(item.bindings);
            }

            AnimationClip clip = m_ClipDataSource.animationClip;
            var wrappers = new List<CurveWrapper>();
            int curveWrapperId = 0;

            foreach (EditorCurveBinding b in bindings)
            {
                var wrapper = new CurveWrapper
                {
                    id = curveWrapperId++,
                    binding = b,
                    groupId = -1,
                    color = CurveUtility.GetPropertyColor(b.propertyName),
                    hidden = false,
                    readOnly = false,
                    renderer = new NormalCurveRenderer(AnimationUtility.GetEditorCurve(clip, b)),
                    getAxisUiScalarsCallback = GetAxisScalars
                };

                wrapper.renderer.SetCustomRange(0.0f, clip.length);
                wrappers.Add(wrapper);
            }

            m_CurveEditor.animationCurves = wrappers.ToArray();
        }

        public void RefreshTree()
        {
            if (m_TreeViewDataSource == null)
                return;

            if (m_Selection == null)
                m_Selection = new int[0];

            // get the names of the previous items
            var selected = m_Selection.Select(x => m_TreeViewDataSource.FindItem(x)).Where(t => t != null).Select(c => c.displayName).ToArray();

            // update the source
            m_TreeViewDataSource.UpdateData();

            // find the same items
            var reselected = m_TreeViewDataSource.GetRows().Where(x => selected.Contains(x.displayName)).Select(x => x.id).ToArray();
            if (!reselected.Any())
            {
                if (m_TreeViewDataSource.GetRows().Count > 0)
                {
                    reselected = new[] { m_TreeViewDataSource.GetItem(0).id };
                }
            }

            // update the selection
            OnItemSelectionChanged(reselected);
        }

        Vector2 GetAxisScalars()
        {
            return new Vector2(1, 1);
        }

        internal virtual bool IsRenamingNodeAllowed(TreeViewItem node)
        {
            return false;
        }
    }
}
