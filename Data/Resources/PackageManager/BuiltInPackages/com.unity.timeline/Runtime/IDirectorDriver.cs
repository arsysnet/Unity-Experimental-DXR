using System.Collections.Generic;
using UnityEngine.Playables;

namespace UnityEngine.Timeline
{
    interface IDirectorDriver
    {
        IList<PlayableDirector> GetDrivenDirectors(IExposedPropertyTable resolver);
    }
}
