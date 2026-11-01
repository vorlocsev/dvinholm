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
        lock (_gate)
        {
            ExpireIfNeeded();
            if (_bytes is null) return false;
            reveal(_bytes);
            return true;
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
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() { lock (_gate) Clear(); }
    public override string ToString() => "CEM PIN material (redacted)";
}
