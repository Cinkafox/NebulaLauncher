using System.Text;

namespace Nebula.Shared.Utils;

public class BitStream : Stream
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private long _bitPosition;

    // Read buffer state
    private int _readByte;
    private int _readBitsConsumed; // 0 to 8

    // Write buffer state
    private int _writeByte;
    private int _writeBitsConsumed; // 0 to 8

    /// <summary>
    /// Creates a BitStream that wraps an existing Stream.
    /// </summary>
    /// <param name="stream">The underlying stream to read from/write to.</param>
    /// <param name="leaveOpen">If true, the underlying stream is not disposed when this BitStream is disposed.</param>
    public BitStream(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
        
        _readBitsConsumed = 8; // Force first read to fetch a byte
        _writeBitsConsumed = 0;
        
        // Initialize position based on the underlying stream's current position
        _bitPosition = stream.CanSeek ? stream.Position * 8 : 0;
    }

    #region System.IO.Stream Overrides

    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => _stream.CanWrite;
    public override bool CanTimeout => _stream.CanTimeout;

    public override long Length 
    {
        get 
        {
            var len = _stream.Length;
            // If we have pending write bits, they will form an additional byte
            if (_writeBitsConsumed > 0) len++;
            return len;
        }
    }

    public override long Position
    {
        get => _bitPosition / 8;
        set
        {
            if (!_stream.CanSeek) throw new NotSupportedException("Stream does not support seeking.");
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            
            FlushWriteBuffer();
            _readBitsConsumed = 8; // Discard read buffer
            
            _stream.Position = value;
            _bitPosition = value * 8;
        }
    }

    /// <summary>
    /// Gets or sets the current position in bits (allows sub-byte positioning).
    /// </summary>
    public long BitPosition
    {
        get => _bitPosition;
        set
        {
            if (!_stream.CanSeek) throw new NotSupportedException("Stream does not support seeking.");
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            
            FlushWriteBuffer();
            _readBitsConsumed = 8; // Discard read buffer
            
            var bytePos = value / 8;
            var bitOffset = (int)(value % 8);
            
            _stream.Position = bytePos;
            
            // If we need to start at a sub-byte offset, we must read the byte into our buffer
            if (bitOffset > 0)
            {
                var b = _stream.ReadByte();
                if (b != -1)
                {
                    _readByte = b;
                    _readBitsConsumed = bitOffset;
                }
            }
            else
            {
                _readBitsConsumed = 8;
            }
            
            _bitPosition = value;
        }
    }

    public override void Flush()
    {
        FlushWriteBuffer();
        _stream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        // Fast path: if perfectly byte-aligned, read directly from the underlying stream
        if (_readBitsConsumed == 0)
        {
            var read = _stream.Read(buffer, offset, count);
            _bitPosition += read * 8;
            return read;
        }
        
        // Slow path: unaligned, read bit-by-bit to build bytes
        var bytesRead = 0;
        for (var i = 0; i < count; i++)
        {
            var b = ReadByte();
            if (b == -1) break;
            buffer[offset + i] = (byte)b;
            bytesRead++;
        }
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!_stream.CanSeek) throw new NotSupportedException("Stream does not support seeking.");
        
        FlushWriteBuffer();
        _readBitsConsumed = 8; // Discard read buffer
        
        var currentBytePos = _bitPosition / 8;
        var newBytePos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => currentBytePos + offset,
            SeekOrigin.End => _stream.Length + offset,
            _ => throw new ArgumentException("Invalid SeekOrigin")
        };
        
        _stream.Position = newBytePos;
        _bitPosition = newBytePos * 8;
        return newBytePos;
    }

    public override void SetLength(long value)
    {
        if (!_stream.CanSeek) throw new NotSupportedException("Stream does not support seeking.");
        
        FlushWriteBuffer();
        _readBitsConsumed = 8;
        _stream.SetLength(value);
        
        if (_bitPosition / 8 > value)
        {
            _bitPosition = value * 8;
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        // Fast path: if perfectly byte-aligned, write directly to the underlying stream
        if (_writeBitsConsumed == 0)
        {
            _stream.Write(buffer, offset, count);
            _bitPosition += count * 8;
        }
        else
        {
            // Slow path: unaligned, write bit-by-bit
            for (var i = 0; i < count; i++)
            {
                WriteByte(buffer[offset + i]);
            }
        }
    }

    public override int ReadByte()
    {
        if (_readBitsConsumed == 0)
        {
            var b = _stream.ReadByte();
            if (b != -1) _bitPosition += 8;
            return b;
        }
        
        var val = 0;
        for (var i = 0; i < 8; i++)
        {
            if (_readBitsConsumed == 8)
            {
                var nextByte = _stream.ReadByte();
                if (nextByte == -1)
                {
                    if (i == 0) return -1; // Standard EOF behavior
                    throw new EndOfStreamException("Unexpected end of stream while reading unaligned byte.");
                }
                _readByte = nextByte;
                _readBitsConsumed = 0;
            }
            
            if (((_readByte >> _readBitsConsumed) & 1) == 1)
            {
                val |= (1 << i);
            }
            _readBitsConsumed++;
            _bitPosition++;
        }
        return val;
    }

    public override void WriteByte(byte value)
    {
        if (_writeBitsConsumed == 0)
        {
            _stream.WriteByte(value);
            _bitPosition += 8;
        }
        else
        {
            for (var i = 0; i < 8; i++)
            {
                WriteBit(((value >> i) & 1) == 1);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            FlushWriteBuffer();
            _stream.Flush();
            if (!_leaveOpen)
            {
                _stream.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    #endregion

    #region Bit-Level Methods

    private void FlushWriteBuffer()
    {
        if (_writeBitsConsumed > 0)
        {
            _stream.WriteByte((byte)_writeByte);
            _writeByte = 0;
            _writeBitsConsumed = 0;
        }
    }

    public void WriteBit(bool value)
    {
        if (value)
        {
            _writeByte |= (1 << _writeBitsConsumed);
        }
        _writeBitsConsumed++;
        _bitPosition++;
        
        if (_writeBitsConsumed == 8)
        {
            _stream.WriteByte((byte)_writeByte);
            _writeByte = 0;
            _writeBitsConsumed = 0;
        }
    }

    public bool ReadBit()
    {
        if (_readBitsConsumed == 8)
        {
            var b = _stream.ReadByte();
            if (b == -1) throw new EndOfStreamException("End of stream reached.");
            _readByte = b;
            _readBitsConsumed = 0;
        }
        
        var val = ((_readByte >> _readBitsConsumed) & 1) == 1;
        _readBitsConsumed++;
        _bitPosition++;
        return val;
    }

    public void WriteInt32(int value, int numberOfBits = 32)
    {
        for (var i = 0; i < numberOfBits; i++)
        {
            WriteBit(((value >> i) & 1) == 1);
        }
    }

    public int ReadInt32(int numberOfBits = 32)
    {
        var result = 0;
        for (var i = 0; i < numberOfBits; i++)
        {
            if (ReadBit()) result |= (1 << i);
        }
        return result;
    }

    public void WriteFloat(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        var bits = BitConverter.ToInt32(bytes, 0);
        WriteInt32(bits, 32);
    }

    public float ReadFloat()
    {
        var bits = ReadInt32(32);
        var bytes = BitConverter.GetBytes(bits);
        return BitConverter.ToSingle(bytes, 0);
    }

    public void WriteString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        WriteInt32(bytes.Length, 32);
        foreach (var b in bytes) WriteByte(b);
    }

    public string ReadString()
    {
        var byteCount = ReadInt32(32); 
        if (byteCount == 0) return string.Empty;

        var bytes = new byte[byteCount];
        for (var i = 0; i < byteCount; i++) 
        {
            var b = ReadByte();
            if (b == -1) throw new EndOfStreamException("End of packet reached while reading string.");
            bytes[i] = (byte)b;
        }
        
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Returns the underlying byte array. Only supported if the underlying stream is a MemoryStream.
    /// </summary>
    public byte[] ToArray()
    {
        Flush();
        if (_stream is MemoryStream ms)
        {
            return ms.ToArray();
        }
        throw new InvalidOperationException("ToArray is only supported when the underlying stream is a MemoryStream.");
    }

    #endregion
}