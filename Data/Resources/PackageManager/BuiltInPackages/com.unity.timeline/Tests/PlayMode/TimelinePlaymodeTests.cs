using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace Tests
{
    internal class TimelinePlaymodeTests
    {
        [Test]
        public void TimelineAsset_SimpleValidation()
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            Assert.That(timeline.outputTrackCount, Is.EqualTo(0));
        }
    }
}
