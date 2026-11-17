using System.Security.Cryptography;

namespace Dvinholm.App;

internal enum CemPinResultOrigin { Synthetic, AdapterUnattested }
internal delegate void CemPinReveal(ReadOnlySpan<byte> bytes);

/// <summary>Owns six sensitive bytes. No public payload property or serialization.</summary>
internal sealed class CemPinSecret : IDisposable
{
    private readonly object _gate = new();
    private readonly TimeProvider _clock;
    private readonly long _created;
    private readonly TimeSpan _lifetime;
    private byte[]? _bytes;
    private ITimer? _timer;
    // Monitor locks are reentrant. Deny recursive disclosure from within an
    // active callback, including through CemPinAttemptResult.TryReveal.
    private bool _revealing;
    // Only an explicitly invoked reveal callback owns this ephemeral copy.
    // Timer expiry or external Dispose can wipe it without waiting for the
    // callback to return. Finally wipes it again before relinquishing it.
    private byte[]? _activeReveal;
    internal CemPinResultOrigin Origin { get; }

    // Ownership transfers after validation. Caller must not retain the buffer.
    // Tests retain synthetic fixtures solely to verify erasure.
    internal CemPinSecret(byte[] ownedBytes, CemPinResultOrigin origin,
        TimeSpan lifetime = default, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(ownedBytes);
        if (ownedBytes.Length != 6 || ownedBytes.Any(b => (b >> 4) > 9 || (b & 15) > 9))
            throw new ArgumentException("Six packed-decimal bytes are required.", nameof(ownedBytes));
        if (!Enum.IsDefined(origin)) throw new ArgumentOutOfRangeException(nameof(origin));
        if (lifetime == default) lifetime = TimeSpan.FromSeconds(30);
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        _clock = clock ?? TimeProvider.System;
        _created = _clock.GetTimestamp();
        _lifetime = lifetime;
        _bytes = ownedBytes;
        Origin = origin;
        lock (_gate)
        {
            try
            {
                _timer = _clock.CreateTimer(_ => Dispose(), null, lifetime, Timeout.InfiniteTimeSpan);
                if (_bytes is null) { _timer.Dispose(); _timer = null; }
            }
            catch (Exception)
            {
                Clear();
                throw new InvalidOperationException("Unable to establish the confidential result expiry timer.");
            }
        }
    }

    internal bool IsAvailable
    {
        get { lock (_gate) { ExpireIfNeeded(); return _bytes is not null; } }
    }

    internal bool TryReveal(CemPinReveal reveal)
    {
        ArgumentNullException.ThrowIfNull(reveal);
        byte[] presentation;
        lock (_gate)
        {
            ExpireIfNeeded();
            if (_bytes is null || _revealing) return false;
            // Calling external code while holding the ownership lock can
            // indefinitely delay the timer or another thread's Dispose().
            // A bounded six-byte temporary buffer isolates the callback.
            presentation = (byte[])_bytes.Clone();
            _activeReveal = presentation;
            _revealing = true;
        }
        try
        {
            reveal(presentation);
            lock (_gate)
            {
                // The callback can reenter Dispose, another thread can revoke
                // this result, or monotonic expiry may occur during display.
                ExpireIfNeeded();
                return _bytes is not null;
            }
        }
        catch (Exception)
        {
            // Never leak private callback exception text. Revoke both the
            // original and in-flight presentation buffer on any failure.
            lock (_gate) Clear();
            return false;
        }
        finally
        {
            lock (_gate)
            {
                CryptographicOperations.ZeroMemory(presentation);
                _activeReveal = null;
                _revealing = false;
            }
        }
    }

    private void ExpireIfNeeded()
    {
        if (_clock.GetElapsedTime(_created) >= _lifetime) Clear();
    }

    private void Clear()
    {
        if (_bytes is not null) CryptographicOperations.ZeroMemory(_bytes);
        _bytes = null;
        // The reveal callback is not holding _gate. Expiry and revocation
        // can zero its temporary bytes immediately even if it is stalled.
        if (_activeReveal is not null) CryptographicOperations.ZeroMemory(_activeReveal);
        // Dispose of a timer owned by a custom TimeProvider only after
        // revoking all confidential buffers. A faulty timer's Dispose must
        // not throw private implementation details through a PIN operation.
        var timer = _timer;
        _timer = null;
        try { timer?.Dispose(); }
        catch (Exception) { /* Already revoked; never leak timer errors. */ }
    }

    public void Dispose() { lock (_gate) Clear(); }
    public override string ToString() => "CEM PIN material (redacted)";
}
