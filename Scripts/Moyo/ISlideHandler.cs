using UnityEngine;

namespace Moyo.Unity
{
    public interface ISlideHandler
    {
        short Priority { get; set; }

        bool TryHandleSlide(float delta);
    }
}
