using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Midi;

public unsafe class MidiReader : IDisposable
{
  private class MidiReaderException(string message) : Exception(message);

  private readonly record struct PositionLength(long Position, uint Length);

  // Base infos

  private bool _ownStream;
  private Stream _baseStream;
  private long _tracksStartPosition;
  private PositionLength[] _trackPositions = null!;

  // Current states

  private long _tick = -1;
  private long[] _currentTrackPositions = null!;

  public MidiReader(string filePath) : this(File.OpenRead(filePath), true) { }

  public MidiReader(Stream midiStream, bool ownStream = false)
  {
    _ownStream = ownStream;
    _baseStream = midiStream;
    ReadHeader();
    GetTrackPositions();    
  }

  public short MidiFormat { get; private set; }

  public short TrackCount { get; private set; }

  public short TicksPerQuarterNote { get; private set; }

  public int CurrentMicrosecondsPerQuarterNote { get; private set; } = 500_000; // = 120 bpm for default

  public double CurrentBPM => (1d / CurrentMicrosecondsPerQuarterNote) * 60 * 1_000_000;

  public void Tick()
  {
    _tick++;


  }

  public void Dispose()
  {
    if (_ownStream)
    {
      _baseStream.Dispose();
    }
    GC.SuppressFinalize(this);
  }

  private void ReadHeader()
  {
    _baseStream.Position = 0;

    // Check header chunk name ('MThd')

    Span<byte> headerMagic = stackalloc byte[4];
    _baseStream.ReadExactly(headerMagic);
    if (!headerMagic.SequenceEqual((ReadOnlySpan<byte>)[0x4D, 0x54, 0x68, 0x64]))
    {
      throw new MidiReaderException("Invalid header name.");
    }

    // Read header chunk length

    uint headerLength = ReadUInt32();
    long headerInfoPosition = _baseStream.Position;

    // Read header info

    short midiFormat = ReadInt16();
    if (midiFormat != 0 && midiFormat != 1)
    {
      throw new MidiReaderException($"Unsupported midi format '{midiFormat}'.");
    }
    MidiFormat = midiFormat;

    short trackCount = ReadInt16();
    if (trackCount == 0)
    {
      throw new MidiReaderException($"Midi contains no tracks.");
    }
    TrackCount = trackCount;

    short tpq = ReadInt16();
    if (tpq <= 0)
    {
      throw new MidiReaderException($"TPQ must be greater than 0 (SMPTE is not supported!).");
    }
    TicksPerQuarterNote = tpq;

    _tracksStartPosition = _baseStream.Position = headerInfoPosition + headerLength;
  }

  private void GetTrackPositions()
  {
    _trackPositions = new PositionLength[TrackCount];
    _currentTrackPositions = new long[TrackCount];

    _baseStream.Position = _tracksStartPosition;

    Span<byte> chunkMagic = stackalloc byte[4];
    for (int i = 0; i < TrackCount;)
    {
      _baseStream.ReadExactly(chunkMagic);
      if (chunkMagic.SequenceEqual((ReadOnlySpan<byte>)[0x4D, 0x54, 0x72, 0x6B])) // 'MTrk'
      {
        uint chunkLength = ReadUInt32();
        _trackPositions[i] = new(_baseStream.Position, chunkLength);
        _baseStream.Position += chunkLength;

        _currentTrackPositions[i] = _baseStream.Position;

        i++;
      }
    }
  }

  private int ReadInt32()
  {
    Span<byte> buffer = stackalloc byte[4];
    _baseStream.ReadExactly(buffer);
    return BinaryPrimitives.ReadInt32BigEndian(buffer);
  }

  private uint ReadUInt32()
  {
    Span<byte> buffer = stackalloc byte[4];
    _baseStream.ReadExactly(buffer);
    return BinaryPrimitives.ReadUInt32BigEndian(buffer);
  }

  private short ReadInt16()
  {
    Span<byte> buffer = stackalloc byte[2];    
    _baseStream.ReadExactly(buffer);
    return BinaryPrimitives.ReadInt16BigEndian(buffer);
  }

  // variable-length quantity
  private int ReadVarInt()
  {
    int value = 0;
    int shift = 0;

    while (true)
    {
      int b = _baseStream.ReadByte();
      if (b == -1)
        throw new EndOfStreamException();

      value = (value << 7) | (b & 0x7F);

      if ((b & 0x80) == 0)
        break;

      shift += 7;

      if (shift > 28)
        throw new MidiReaderException("VarInt too long.");
    }

    return value;
  }  
}
