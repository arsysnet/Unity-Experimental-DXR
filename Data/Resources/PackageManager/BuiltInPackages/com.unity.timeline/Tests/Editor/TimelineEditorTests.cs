using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.Timeline;
using UnityEditor.Playables;

namespace Tests
{
    // trivial editor tests.
    internal class TimelineEditorTests
    {
        // A Test behaves as an ordinary method
        [Test]
        public void TimelineEditor_IsLoadedFromDll()
        {
            Assert.That(typeof(TimelineEditor).Assembly.FullName, Contains.Substring("Unity.Timeline.Editor"));
        }
    }
}
