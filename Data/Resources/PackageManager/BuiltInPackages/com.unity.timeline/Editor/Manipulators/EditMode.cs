using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    static class EditMode
    {
        public enum EditType
        {
            None = -1,
            Mix = 0,
            Ripple = 1,
            Replace = 2
        }

        class SubEditMode
        {
            public IMoveItemMode moveItemMode { get; }
            public IMoveItemDrawer moveItemDrawer { get; }
            public ITrimItemMode trimItemMode { get; }
            public ITrimItemDrawer trimItemDrawer { get; }
            public IAddDeleteItemMode addDeleteItemMode { get; }
            public Color color { get; }
            public KeyCode clutchKey { get; }

            SubEditMode(IMoveItemMode moveMode, IMoveItemDrawer moveDrawer, ITrimItemMode trimMode, ITrimItemDrawer trimDrawer, IAddDeleteItemMode addDeleteMode, Color guiColor, KeyCode key)
            {
                moveItemMode = moveMode;
                moveItemDrawer = moveDrawer;
                trimItemMode = trimMode;
                trimItemDrawer = trimDrawer;
                addDeleteItemMode = addDeleteMode;
                color = guiColor;

                clutchKey = key;
            }

            public static SubEditMode MixSubEditMode(Color guiColor, KeyCode key)
            {
                var move = new MoveItemModeMix();
                var trim = new TrimItemModeMix();
                return new SubEditMode(move, move, trim, trim, new AddDeleteItemModeMix(), guiColor, key);
            }

            public static SubEditMode RippleSubEditMode(Color guiColor, KeyCode key)
            {
                var move = new MoveItemModeRipple();
                var trim = new TrimItemModeRipple();
                return new SubEditMode(move, move, trim, trim, new AddDeleteItemModeRipple(), guiColor, key);
            }

            public static SubEditMode ReplaceSubEditMode(Color guiColor, KeyCode key)
            {
                var move = new MoveItemModeReplace();
                var trim = new TrimItemModeReplace();
                return new SubEditMode(move, move, trim, trim, new AddDeleteItemModeReplace(), guiColor, key);
            }
        }

        //Use GetSubEditMode to iterate on the EditModes
        static readonly Dictionary<EditType, SubEditMode> s_EditModes = new Dictionary<EditType, SubEditMode>
        {
            { EditType.Mix,     SubEditMode.MixSubEditMode(DirectorStyles.kMixToolColor, KeyCode.Alpha1) },
            { EditType.Ripple,  SubEditMode.RippleSubEditMode(DirectorStyles.kRippleToolColor, KeyCode.Alpha2) },
            { EditType.Replace, SubEditMode.ReplaceSubEditMode(DirectorStyles.kReplaceToolColor, KeyCode.Alpha3) }
        };

        static EditType s_CurrentEditType = EditType.Mix;
        static EditType s_OverrideEditType = EditType.None;

        static ITrimmable s_CurrentTrimItem;
        static TrimEdge s_CurrentTrimDirection;
        static MoveItemHandler s_CurrentMoveItemHandler;
        static EditModeInputHandler s_InputHandler = new EditModeInputHandler();

        static ITrimItemMode trimMode
        {
            get { return GetSubEditMode(editType).trimItemMode; }
        }

        static ITrimItemDrawer trimDrawer
        {
            get { return GetSubEditMode(editType).trimItemDrawer; }
        }

        static IMoveItemMode moveMode
        {
            get { return GetSubEditMode(editType).moveItemMode; }
        }

        static IMoveItemDrawer moveDrawer
        {
            get { return GetSubEditMode(editType).moveItemDrawer; }
        }

        static IAddDeleteItemMode addDeleteMode
        {
            get { return GetSubEditMode(editType).addDeleteItemMode; }
        }

        public static EditModeInputHandler inputHandler
        {
            get { return s_InputHandler; }
        }

        static Color modeColor
        {
            get { return GetSubEditMode(editType).color; }
        }

        public static EditType editType
        {
            get
            {
                if (s_OverrideEditType != EditType.None)
                    return s_OverrideEditType;

                var window = TimelineWindow.instance;
                if (window != null)
                    s_CurrentEditType = window.state.editType;

                return s_CurrentEditType;
            }
            set
            {
                s_CurrentEditType = value;

                var window = TimelineWindow.instance;
                if (window != null)
                    window.state.editType = value;

                s_OverrideEditType = EditType.None;
            }
        }

        static SubEditMode GetSubEditMode(EditType type)
        {
            var ret = s_EditModes[type];
            if (ret != null)
                return ret;

            switch (editType)
            {
                case EditType.Mix:
                    ret = SubEditMode.MixSubEditMode(DirectorStyles.kMixToolColor, KeyCode.Alpha1);
                    break;
                case EditType.Ripple:
                    ret = SubEditMode.RippleSubEditMode(DirectorStyles.kRippleToolColor, KeyCode.Alpha2);
                    break;
                case EditType.Replace:
                    ret = SubEditMode.ReplaceSubEditMode(DirectorStyles.kReplaceToolColor, KeyCode.Alpha3);
                    break;
                case EditType.None:
                    Debug.LogError("Unsupported editmode type");
                    break;
            }

            s_EditModes[type] = ret;
            return ret;
        }

        public static void ClearEditMode()
        {
            s_EditModes[editType] = null;
        }

        public static void BeginTrim(ITimelineItem item, TrimEdge trimDirection)
        {
            var itemToTrim = item as ITrimmable;
            if (itemToTrim == null) return;

            s_CurrentTrimItem = itemToTrim;
            s_CurrentTrimDirection = trimDirection;
            trimMode.OnBeforeTrim(itemToTrim, trimDirection);
            TimelineUndo.PushUndo(itemToTrim.parentTrack, "Trim Clip");
        }

        public static void TrimStart(ITimelineItem item, double time)
        {
            var itemToTrim = item as ITrimmable;
            if (itemToTrim == null) return;

            trimMode.TrimStart(itemToTrim, time);
        }

        public static void TrimEnd(ITimelineItem item, double time, bool affectTimeScale)
        {
            var itemToTrim = item as ITrimmable;
            if (itemToTrim == null) return;

            trimMode.TrimEnd(itemToTrim, time, affectTimeScale);
        }

        public static void DrawTrimGUI(WindowState state, TimelineItemGUI item, TrimEdge edge)
        {
            trimDrawer.DrawGUI(state, item.boundingRect, modeColor, edge);
        }

        public static void FinishTrim()
        {
            s_CurrentTrimItem = null;

            TimelineCursors.ClearCursor();
            ClearEditMode();
        }

        public static void BeginMove(MoveItemHandler moveItemHandler)
        {
            s_CurrentMoveItemHandler = moveItemHandler;
            moveMode.BeginMove(s_CurrentMoveItemHandler.movingItems);
        }

        public static void UpdateMove()
        {
            moveMode.UpdateMove(s_CurrentMoveItemHandler.movingItems);
        }

        public static void OnTrackDetach(IEnumerable<ItemsPerTrack> grabbedTrackItems)
        {
            moveMode.OnTrackDetach(grabbedTrackItems);
        }

        public static void HandleTrackSwitch(IEnumerable<ItemsPerTrack> grabbedTrackItems)
        {
            moveMode.HandleTrackSwitch(grabbedTrackItems);
        }

        public static bool AllowTrackSwitch()
        {
            return moveMode.AllowTrackSwitch();
        }

        public static double AdjustStartTime(WindowState state, ItemsPerTrack itemsGroup, double time)
        {
            return moveMode.AdjustStartTime(state, itemsGroup, time);
        }

        public static bool ValidateDrag(ItemsPerTrack itemsGroup)
        {
            return moveMode.ValidateMove(itemsGroup);
        }

        public static void DrawMoveGUI(WindowState state, IEnumerable<MovingItems> movingItems)
        {
            moveDrawer.DrawGUI(state, movingItems, modeColor);
        }

        public static void FinishMove()
        {
            var manipulatedItemsList = s_CurrentMoveItemHandler.movingItems;
            moveMode.FinishMove(manipulatedItemsList);

            foreach (var itemsGroup in manipulatedItemsList)
                foreach (var item in itemsGroup.items)
                    item.parentTrack = itemsGroup.targetTrack;

            s_CurrentMoveItemHandler = null;

            TimelineCursors.ClearCursor();
            ClearEditMode();
        }

        public static void FinalizeInsertItemsAtTime(IEnumerable<ItemsPerTrack> newItems, double requestedTime)
        {
            addDeleteMode.InsertItemsAtTime(newItems, requestedTime);
        }

        public static void PrepareItemsDelete(IEnumerable<ItemsPerTrack> newItems)
        {
            addDeleteMode.RemoveItems(newItems);
        }

        public static void HandleModeClutch()
        {
            if (Event.current.type == EventType.KeyDown && EditorGUI.IsEditingTextField())
                return;

            var prevType = editType;

            if (Event.current.type == EventType.KeyDown)
            {
                var editTypes = Enum.GetValues(typeof(EditType)).Cast<EditType>();
                foreach (var type in editTypes)
                {
                    if (type == EditType.None)
                        continue;

                    var mode = GetSubEditMode(type);
                    if (Event.current.keyCode == mode.clutchKey)
                    {
                        s_OverrideEditType = type;
                        Event.current.Use();
                        break;
                    }
                }
            }
            else if (Event.current.type == EventType.KeyUp)
            {
                var editTypes = Enum.GetValues(typeof(EditType)).Cast<EditType>();
                foreach (var type in editTypes)
                {
                    if (type == EditType.None)
                        continue;

                    var mode = GetSubEditMode(type);
                    if (Event.current.keyCode == mode.clutchKey)
                    {
                        s_OverrideEditType = EditType.None;
                        Event.current.Use();
                        break;
                    }
                }
            }

            if (prevType != editType)
            {
                if (s_CurrentTrimItem != null)
                {
                    trimMode.OnBeforeTrim(s_CurrentTrimItem, s_CurrentTrimDirection);
                }
                else if (s_CurrentMoveItemHandler != null)
                {
                    if (s_CurrentMoveItemHandler.movingItems == null)
                    {
                        s_CurrentMoveItemHandler = null;
                        return;
                    }

                    foreach (var movingItems in s_CurrentMoveItemHandler.movingItems)
                    {
                        if (movingItems != null && movingItems.parentTrack == null)
                        {
                            foreach (var items in movingItems.items)
                            {
                                items.parentTrack = movingItems.originalTrack;
                            }
                        }
                    }

                    var movingSelection = s_CurrentMoveItemHandler.movingItems;

                    // Handle clutch key transition if needed
                    GetSubEditMode(prevType).moveItemMode.OnModeClutchExit(movingSelection);
                    moveMode.OnModeClutchEnter(movingSelection);

                    moveMode.BeginMove(movingSelection);
                    moveMode.HandleTrackSwitch(movingSelection);

                    UpdateMove();
                    s_CurrentMoveItemHandler.RefreshPreviewItems();

                    TimelineWindow.instance.state.rebuildGraph = true; // TODO Rebuild only if parent changed
                }

                TimelineWindow.instance.Repaint(); // TODO Refresh the toolbar without doing a full repaint?
            }
        }
    }
}
