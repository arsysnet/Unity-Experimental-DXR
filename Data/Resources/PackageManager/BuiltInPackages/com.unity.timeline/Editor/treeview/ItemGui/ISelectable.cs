using System;

namespace UnityEditor.Timeline
{
    interface ISelectable
    {
        LayerZOrder zOrder { get; }
        void Select();
        bool IsSelected();
        void Deselect();
    }
}
