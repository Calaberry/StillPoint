using System.Threading;
using StillPoint.Services;

namespace StillPoint;

/// <summary>Keeps a single StillPoint instance alive; a second launch just logs and exits.</summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\StillPoint.SingleInstance.v1";
    private Mutex? _mutex;
    private bool _owned;

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: false, MutexName);
        try
        {
            _owned = _mutex.WaitOne(0, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            _owned = true;
        }

        if (!_owned)
        {
            Log.Warn("Another StillPoint instance is already running; exiting this one.");
        }

        return _owned;
    }

    public void Dispose()
    {
        if (_mutex is null)
        {
            return;
        }

        if (_owned)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Already released.
            }
        }

        _mutex.Dispose();
        _mutex = null;
        _owned = false;
    }
}
