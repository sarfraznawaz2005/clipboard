using System.Threading;

namespace ClipboardApp.Services;

public sealed class SingleInstance : IDisposable
{
    Mutex? _mutex;
    EventWaitHandle? _signal;
    Thread? _listener;

    public bool IsFirstInstance { get; private set; }

    public void Acquire()
    {
        _mutex = new Mutex(initiallyOwned: true, @"Local\Clipboard.SingleInstance", out var created);
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Clipboard.Show");
        IsFirstInstance = created;

        if (!created)
        {
            _signal.Set();
            return;
        }
    }

    public void ListenForActivation(Action onShowRequested)
    {
        _listener = new Thread(() =>
        {
            while (_signal!.WaitOne())
                onShowRequested();
        })
        { IsBackground = true };
        _listener.Start();
    }

    public void Dispose()
    {
        _mutex?.Dispose();
        _signal?.Dispose();
    }
}
