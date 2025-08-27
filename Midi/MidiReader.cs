using System.Buffers.Binary;
using System.Text;

namespace Midi;

public unsafe class MidiReader : IDisposable
{
  private class MidiReaderException(string message) : Exception(message);

  private readonly record struct PositionLength(long Position, uint Length);

  private record class EventAndRemainingTicks(IEvent Event)
  {
    public long RemainingTicks { get; set; } = Event.DeltaTime;
  }

  public const int ChannelCount = 16;

  // Base infos

  private bool _ownStream;
  private Stream _baseStream;
  private long _tracksStartPosition;
  private PositionLength[] _trackStartPositionAndLengths = null!;

  // Current states

  private long _currentTick = -1;
  private long[] _currentTrackPositions = null!;
  private Dictionary<int, int>[] _currentCCValuesByChannel = [.. Enumerable.Repeat(new Dictionary<int, int>(), ChannelCount)];
  private UInt128[] _currentKetStates = new UInt128[ChannelCount];
  private List<EventAndRemainingTicks>[] _eventBuffer = [.. Enumerable.Repeat(new List<EventAndRemainingTicks>(), ChannelCount)];

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

  public double CurrentBPM => Helpers.MicrosecondsPerQuarterNoteToBPM(CurrentMicrosecondsPerQuarterNote);

  public void Tick()
  {
    _currentTick++;
  }

  public void Dispose()
  {
    if (_ownStream)
    {
      _baseStream.Dispose();
    }
    GC.SuppressFinalize(this);
  }

  public void ReadNextEventOfTrack(short trackIndex)
  {
    _baseStream.Position = _currentTrackPositions[trackIndex];

    if (_baseStream.Position < _trackStartPositionAndLengths[trackIndex].Position + _trackStartPositionAndLengths[trackIndex].Length)
    {
      if (ReadOneEvent() is IEvent @event)
      {
        InputEvent(@event);
      }
      _currentTrackPositions[trackIndex] = _baseStream.Position;
    }
  }

  private void InputEvent(IEvent @event)
  {

  }

  private IEvent? ReadOneEvent()
  {
    IEvent? result = null;

    int deltaTime = ReadVarInt();
    byte firstByte = ReadByte();

    // Meta Event
    if (firstByte == 0xFF)
    {
      byte metaEventType = ReadByte();
      int metaEventDataLength = ReadVarInt();      
      Span<byte> metaEventData = new byte[metaEventDataLength];
      if (metaEventDataLength != 0)
      {
        ReadBytes(metaEventData);
      }

      switch (metaEventType)
      {
        case 0x01:
        case 0x03:
        case 0x04:
        case 0x05:
        case 0x06:
        case 0x07:
          {
            result = new TextMetaEvent(deltaTime, Encoding.UTF8.GetString(metaEventData));
          }
          break;
        case 0x02:
          {
            result = new CopyrightMetaEvent(deltaTime, Encoding.UTF8.GetString(metaEventData));
          }
          break;
        case 0x51:
          {
            if (metaEventDataLength == 3)
            {
              result = new TempoMetaEvent(deltaTime, (metaEventData[0] << 16) | (metaEventData[1] << 8) | (metaEventData[2]));
            }            
          }
          break;
        default:
          break;
      }
    }

    // SysEx Event (暂不处理)
    else if (firstByte == 0xF0 || firstByte == 0xF7)
    {
      int sysExEventDataLength = ReadVarInt();      
      Span<byte> sysExEventData = new byte[sysExEventDataLength];
      if (sysExEventDataLength != 0)
      {
        ReadBytes(sysExEventData);
      }
    }

    else if (firstByte == 0xF2)
    {
      ReadInt16();
    }

    else if (firstByte == 0xF3)
    {
      ReadByte();
    }

    // Midi Event
    else
    {
      byte midiEventType = (byte)(firstByte & 0b1111_0000);
      byte channelNo = (byte)(firstByte & 0b0000_1111);

      switch (midiEventType)
      {
        // Note Off
        case 0b1000_0000:
          {
            byte key = ReadByte();
            byte vel = ReadByte();

            result = new NoteOffMidiEvent(deltaTime, channelNo, key, vel);
          }
          break;

        // Note On
        case 0b1001_0000:
          {
            byte key = ReadByte();
            byte vel = ReadByte();

            result = new NoteOnMidiEvent(deltaTime, channelNo, key, vel);
          }
          break;

        // Polyphonic After-touch
        case 0b1010_0000:
          {
            byte key = ReadByte();
            byte pressure = ReadByte();

            result = new PolyphonicAftertouchMidiEvent(deltaTime, channelNo, key, pressure);
          }
          break;

        // Control Change
        case 0b1011_0000:
          {
            byte ccNo = ReadByte(); 
            byte value = ReadByte();

            result = new ControlChangeMidiEvent(deltaTime, channelNo, ccNo, value);
          }
          break;

        // Program Change
        case 0b1100_0000:
          {
            byte pcNo = ReadByte();

            result = new ProgramChangeMidiEvent(deltaTime, channelNo, pcNo);
          }
          break;

        // After-touch
        case 0b1101_0000:
          {
            byte pressure = ReadByte();

            result = new AftertouchMidiEvent(deltaTime, channelNo, pressure);
          }
          break;

        // Pitch Wheel Change
        case 0b1110_0000:
          {
            byte low7 = ReadByte();
            byte high7 = ReadByte();
            short value = (short)((high7 << 7) | low7);

            result = new PitchChangeMidiEvent(deltaTime, channelNo, value);
          }
          break;

        default:
          break;
      }
    }

    return result;
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
    _trackStartPositionAndLengths = new PositionLength[TrackCount];
    _currentTrackPositions = new long[TrackCount];

    _baseStream.Position = _tracksStartPosition;

    Span<byte> chunkMagic = stackalloc byte[4];
    for (int i = 0; i < TrackCount;)
    {
      _baseStream.ReadExactly(chunkMagic);
      if (chunkMagic.SequenceEqual((ReadOnlySpan<byte>)[0x4D, 0x54, 0x72, 0x6B])) // 'MTrk'
      {
        uint chunkLength = ReadUInt32();
        _trackStartPositionAndLengths[i] = new(_baseStream.Position, chunkLength);
        _currentTrackPositions[i] = _baseStream.Position;
        _baseStream.Position += chunkLength;
        i++;
      }
    }
  }

  private void ReadBytes(Span<byte> buffer)
  {
    _baseStream.ReadExactly(buffer);
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

  private byte ReadByte()
  {
    return (byte)_baseStream.ReadByte();
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
