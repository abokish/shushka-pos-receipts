namespace ShushkaReceipt;

public sealed class AppState
{
    private volatile bool _listenerActive;

    public bool ListenerActive => _listenerActive;

    public event Action<bool>? ListenerStatusChanged;

    public void SetListenerActive(bool active)
    {
        _listenerActive = active;
        ListenerStatusChanged?.Invoke(active);
    }
}
