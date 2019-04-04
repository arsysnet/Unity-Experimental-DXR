using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Timeline
{
    class TrackItemsDrawer
    {
        List<ItemsLayer> m_Layers;
        ClipsLayer m_ClipsLayer;

        public IEnumerable<TimelineClipGUI> clips
        {
            get { return m_ClipsLayer.items.Cast<TimelineClipGUI>(); }
        }

        public TrackItemsDrawer(IRowGUI parent)
        {
            BuildGUICache(parent);
        }

        void BuildGUICache(IRowGUI parent)
        {
            m_ClipsLayer = new ClipsLayer(0, parent);
            m_Layers = new List<ItemsLayer>
            {
                m_ClipsLayer,
                new MarkersLayer(1, parent)
            };
        }

        public void Draw(Rect rect, TrackDrawer drawer, WindowState state)
        {
            foreach (var layer in m_Layers)
            {
                layer.Draw(rect, drawer, state);
            }
        }
    }
}
