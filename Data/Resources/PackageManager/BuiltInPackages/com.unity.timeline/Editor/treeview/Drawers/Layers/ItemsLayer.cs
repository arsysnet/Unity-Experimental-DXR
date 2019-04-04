using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Timeline
{
    interface IZOrderProvider
    {
        LayerZOrder Next();
    }

    struct LayerZOrder : IComparable<LayerZOrder>
    {
        byte m_Layer;
        int m_ZOrder;

        public LayerZOrder(byte layer, int zOrder)
        {
            m_Layer = layer;
            m_ZOrder = zOrder;
        }

        public int CompareTo(LayerZOrder other)
        {
            if (m_Layer == other.m_Layer)
                return m_ZOrder.CompareTo(other.m_ZOrder);
            return m_Layer.CompareTo(other.m_Layer);
        }

        public static LayerZOrder operator++(LayerZOrder x)
        {
            return new LayerZOrder(x.m_Layer, x.m_ZOrder + 1);
        }
    }

    abstract class ItemsLayer : IZOrderProvider
    {
        LayerZOrder m_LastZOrder;

        public LayerZOrder Next()
        {
            return m_LastZOrder++;
        }

        readonly List<TimelineItemGUI> m_Items = new List<TimelineItemGUI>();
        bool m_NeedSort = true;

        public virtual void Draw(Rect rect, TrackDrawer drawer, WindowState state)
        {
            if (!m_Items.Any()) return;

            Sort();
            var visibleTime = state.timeAreaShownRange;
            foreach (var item in m_Items)
            {
                item.visible = item.end > visibleTime.x && item.start < visibleTime.y;

                if (!item.visible)
                    continue;

                item.Draw(rect, drawer, state);
            }
        }

        public IEnumerable<TimelineItemGUI> items
        {
            get
            {
                return m_Items;
            }
        }

        protected void AddItem(TimelineItemGUI item)
        {
            m_Items.Add(item);
            m_NeedSort = true;
        }

        protected ItemsLayer(byte layerOrder)
        {
            m_LastZOrder = new LayerZOrder(layerOrder, 0);
        }

        void Sort()
        {
            if (!m_NeedSort)
                return;

            m_Items.Sort((a, b) => a.zOrder.CompareTo(b.zOrder));
            m_NeedSort = false;
        }
    }
}
