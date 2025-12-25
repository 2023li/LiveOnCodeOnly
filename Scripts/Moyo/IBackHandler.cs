namespace Moyo.Unity
{
    public interface IBackHandler
    {
        short Priority { get; set; }

        bool TryHandleBack();
    }
}
