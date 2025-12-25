using Moyo.Unity;

public enum LOAppEventType
{
    开始游戏,
}

public struct LOAppEvent
{
    public LOAppEventType eventType;



    private static LOAppEvent e;
    public static void Tigger(LOAppEventType eType)
    {
        e.eventType = eType;
        MoyoEventManager.TriggerEvent(e);
    }


}
