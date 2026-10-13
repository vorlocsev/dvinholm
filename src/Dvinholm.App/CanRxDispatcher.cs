using System.Threading.Channels;

namespace Dvinholm.App;

internal sealed class CanRxDispatcher : IDisposable
{
    internal sealed class Subscription : IDisposable
    {
        private const int DefaultQueueCapacity = 4096;

        private readonly CanRxDispatcher _owner;
        private readonly Channel<CanFrame> _channel;
        private readonly int _capacity;
        private readonly Func<CanFrame, bool>? _filter;
        private long _droppedFrameCount;
        private long _lastFilterErrorTimestamp;
        private int _disposed;

        internal Subscription(CanRxDispatcher owner, int capacity = DefaultQueueCapacity, Func<CanFrame, bool>? filter = null)
        {
            _owner = owner;
            _filter = filter;
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _channel = Channel.CreateBounded<CanFrame>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            }, _ => Interlocked.Increment(ref _droppedFrameCount));
        }

        // Counts frames evicted by the bounded queue's DropOldest policy.
        // The channel invokes the callback atomically with its drop operation,
        // so this remains accurate even when publishers and readers race.
        internal long DroppedFrameCount => Interlocked.Read(ref _droppedFrameCount);

        internal bool TryRead(TimeSpan timeout, CancellationToken cancellationToken, out CanFrame frame)
        {
            frame = default;
            if (Dvinholm.App.CanRxDispatcher.IsDisposed(_disposed))
                return false;

            if (timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            try
            {
                // Another reader may consume an item after WaitToReadAsync
                // completes but before TryRead. Loop until we get a frame or
                // the original timeout expires; never report a false timeout
                // while a concurrent reader merely won the race.
                //
                // Cancel each pending channel wait when its remaining budget
                // expires. Waiting on the task with a separate timed Wait()
                // leaves WaitToReadAsync registered on the channel after
                // TryRead returns false, accumulating orphaned waiters during
                // repeated timeouts.
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                while (true)
                {
                    if (_channel.Reader.TryRead(out frame))
                        return true;

                    var remaining = timeout - stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        return false;

                    using var waitCancellation =
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    // CancellationTokenSource timers have a finite maximum
                    // delay (roughly 49 days). Long caller timeouts are valid,
                    // so wait in bounded timer windows and re-check the original
                    // elapsed-time budget after each window expires.
                    var timerWindow = TimeSpan.FromMilliseconds(uint.MaxValue - 1d);
                    waitCancellation.CancelAfter(remaining > timerWindow ? timerWindow : remaining);

                    try
                    {
                        if (!_channel.Reader.WaitToReadAsync(waitCancellation.Token)
                                .AsTask().GetAwaiter().GetResult())
                            return false;
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // The local timer window expired. If the caller's total
                        // timeout has not elapsed, start another bounded wait.
                        if (stopwatch.Elapsed < timeout)
                            continue;

                        return false;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ChannelClosedException)
            {
                return false;
            }
        }

        // Remove frames already buffered before a new ISO-TP transaction begins.
        // Frames that arrive after this operation still require UDS-level correlation.
        internal int DrainPendingFrames()
        {
            var drained = 0;
            while (_channel.Reader.TryRead(out _))
                drained++;
            return drained;
        }

        internal void Publish(in CanFrame frame)
        {
            if (Dvinholm.App.CanRxDispatcher.IsDisposed(_disposed))
                return;

            // Filter before enqueueing so unrelated bus traffic cannot evict
            // diagnostic frames from a bounded ISO-TP subscription. A faulty
            // subscriber predicate must not abort publication to other subscribers.
            if (_filter is not null)
            {
                try
                {
                    if (!_filter(frame))
                        return;
                }
                catch (Exception ex)
                {
                    ReportFilterError(ex);
                    return;
                }
            }

            // DropOldest preserves non-blocking publication for the native
            // read worker. The channel's item-dropped callback records evictions;
            // TryWrite alone cannot report them in this full mode.
            _channel.Writer.TryWrite(frame);
        }

        private void ReportFilterError(Exception error)
        {
            // A broken predicate can fail for every frame at bus speed. Report
            // at most once per second per subscription to keep diagnostics useful
            // without flooding the UI/log; always report the first failure.
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            while (true)
            {
                var last = Interlocked.Read(ref _lastFilterErrorTimestamp);
                if (last != 0 && now - last < System.Diagnostics.Stopwatch.Frequency)
                    return;

                if (Interlocked.CompareExchange(ref _lastFilterErrorTimestamp, now, last) == last)
                    break;
            }

            _owner.ReportReadError(new InvalidOperationException(
                "CAN subscription filter failed; the frame was skipped for that subscription. Repeated failures are rate-limited.", error));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _owner.Remove(this);
            _channel.Writer.TryComplete();
        }
    }

    private readonly IJ2534Adapter _j2534;
    private readonly object _gate = new();
    private readonly object _lifecycleGate = new();
    private readonly HashSet<Subscription> _subscriptions = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private int _running;
    private int _disposed;
    private int _workerThreadId;
    private readonly TaskCompletionSource<bool> _disposeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal event Action<Exception>? ReadError;

    // Lifecycle operations that may unload the vendor DLL must not run inline
    // on the thread currently executing the adapter's native Read call.
    internal bool IsOnReadWorker =>
        Volatile.Read(ref _workerThreadId) == Environment.CurrentManagedThreadId;

    internal CanRxDispatcher(IJ2534Adapter j2534)
        => _j2534 = j2534 ?? throw new ArgumentNullException(nameof(j2534));

    internal Subscription Subscribe(int capacity = 4096, Func<CanFrame, bool>? filter = null)
    {
        ObjectDisposedException.ThrowIf(Dvinholm.App.CanRxDispatcher.IsDisposed(_disposed), this);
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        var subscription = new Subscription(this, capacity, filter);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(Dvinholm.App.CanRxDispatcher.IsDisposed(_disposed), this);
            _subscriptions.Add(subscription);
        }
        return subscription;
    }

    internal void Start()
    {
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(Dvinholm.App.CanRxDispatcher.IsDisposed(_disposed), this);
            if (_running != 0)
                return;

            if (_worker is { IsCompleted: false })
                throw new InvalidOperationException("The previous CAN receive worker is still stopping.");

            _worker = null;
            _cts?.Dispose();
            _cts = null;

            var cts = new CancellationTokenSource();
            _cts = cts;
            _running = 1;
            _worker = Task.Run(() => ReadLoop(cts.Token));
        }
    }

    internal void Stop()
    {
        lock (_lifecycleGate)
        {
            // Stop may be called more than once. Even if the running flag was
            // cleared by an earlier timed-out Stop(), still wait for the
            // existing worker before allowing Dispose to tear down resources.
            var worker = _worker;
            var cts = _cts;
            if (_running == 0 && (worker is null || worker.IsCompleted))
                return;

            _running = 0;
            cts?.Cancel();

            // Stop can be invoked by a ReadError callback on the receive
            // worker itself. Never synchronously wait for the current thread.
            if (Volatile.Read(ref _workerThreadId) == Environment.CurrentManagedThreadId)
                return;

            try
            {
                worker?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException)
            {
                // ReadLoop reports recoverable adapter errors via ReadError.
            }

            if (worker is null || worker.IsCompleted)
            {
                cts?.Dispose();
                _cts = null;
                _worker = null;
                return;
            }

            // A native read can outlive the bounded wait. Keep the worker and
            // CTS alive, and prevent Start() from creating a second reader.
            _ = worker.ContinueWith(
                completed =>
                {
                    cts?.Dispose();
                    lock (_lifecycleGate)
                    {
                        if (ReferenceEquals(_worker, completed))
                        {
                            _worker = null;
                            _cts = null;
                        }
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private void ReadLoop(CancellationToken token)
    {
        Volatile.Write(ref _workerThreadId, Environment.CurrentManagedThreadId);

        // Malformed frames can arrive at bus speed. Keep diagnostics useful
        // without flooding the UI/log with one error per bad frame.
        long lastDecodeErrorTimestamp = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
            try
            {
                if (!_j2534.Read(out var message, 250, out var error))
                {
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        ReportReadError(new InvalidOperationException(error));

                        // Some adapters return an error immediately instead of
                        // respecting the read timeout. Back off to avoid a hot
                        // loop that can flood the UI/log and consume a CPU core.
                        if (token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(100)))
                            break;
                    }
                    else
                    {
                        // A few adapters return "no message" immediately with
                        // no error instead of honoring the requested timeout.
                        // A small idle backoff prevents a busy loop without
                        // adding meaningful latency when the bus is active.
                        if (token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(5)))
                            break;
                    }

                    continue;
                }

                if (!CanDecoder.TryDecode(message, out var frame, out error))
                {
                    var now = System.Diagnostics.Stopwatch.GetTimestamp();
                    var elapsedTicks = now - lastDecodeErrorTimestamp;
                    if (lastDecodeErrorTimestamp == 0 ||
                        elapsedTicks >= System.Diagnostics.Stopwatch.Frequency)
                    {
                        lastDecodeErrorTimestamp = now;
                        ReportReadError(new InvalidOperationException("CAN decode error: " + error));
                    }
                    continue;
                }

                Subscription[] subscribers;
                lock (_gate)
                    subscribers = _subscriptions.ToArray();

                foreach (var subscriber in subscribers)
                    subscriber.Publish(frame);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Normal shutdown: cancellation is expected only when the
                // dispatcher itself requested it.
                break;
            }
            catch (Exception)
            {
                // Vendor interop exceptions may include machine-specific paths.
                // Keep the read failure visible without exposing raw driver details.
                ReportReadError(new InvalidOperationException(
                    "J2534 read failed due to an unexpected adapter error."));

                // Avoid a hot loop if the adapter keeps throwing immediately.
                if (token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(100)))
                    break;
            }
            }
        }
        finally
        {
            // Avoid stale thread IDs being mistaken for the worker if the
            // runtime later reuses this managed thread for unrelated work.
            Volatile.Write(ref _workerThreadId, 0);
        }
    }

    private void ReportReadError(Exception error)
    {
        var handlers = ReadError;
        if (handlers is null)
            return;

        foreach (Action<Exception> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(error);
            }
            catch
            {
                // A UI/logging subscriber must not terminate the CAN read loop.
            }
        }
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate)
            _subscriptions.Remove(subscription);
    }

    private static bool IsDisposed(int value) => value != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            // A second caller must not mistake disposal-started for disposal-completed.
            // If the first call came from a ReadError callback, cleanup is deferred
            // until the worker exits; external callers must wait for that lifetime barrier.
            if (Volatile.Read(ref _workerThreadId) != Environment.CurrentManagedThreadId)
                _disposeCompletion.Task.GetAwaiter().GetResult();
            return;
        }

        try
        {
            Stop();

            // Do not unload the native adapter while ReadLoop is inside a native call.
            Task? worker;
            lock (_lifecycleGate)
                worker = _worker;

            // Waiting for the worker from its own callback would deadlock. Defer
            // cleanup until the worker exits, while other callers wait on completion.
            if (Volatile.Read(ref _workerThreadId) == Environment.CurrentManagedThreadId)
            {
                if (worker is not null)
                    _ = worker.ContinueWith(
                        _ => CompleteDispose(),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                else
                    CompleteDispose();
                return;
            }

            try
            {
                worker?.GetAwaiter().GetResult();
            }
            catch
            {
                // Read errors are reported through ReadError; cleanup still proceeds.
            }

            CompleteDispose();
        }
        catch
        {
            _disposeCompletion.TrySetResult(true);
            throw;
        }
    }

    private void CompleteDispose()
    {
        try
        {
            DisposeSubscriptions();
        }
        finally
        {
            _disposeCompletion.TrySetResult(true);
        }
    }
    private void DisposeSubscriptions()
    {
        Subscription[] subscribers;
        lock (_gate)
        {
            subscribers = _subscriptions.ToArray();
            _subscriptions.Clear();
        }

        foreach (var subscriber in subscribers)
            subscriber.Dispose();
    }
}
