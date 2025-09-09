using System.Buffers.Binary;
using System.Text;

namespace Midi;

public unsafe class MidiReader : IDisposable
{
  private class MidiReaderException(string message) : Exception(message);

  private readonly record struct PositionLength(long Position, uint Length);

  // Consts

  public const int ChannelCount = 16;

  // Base infos

  private bool             _doOwnStream;
  private Stream           _baseStream;
  private short            _midiFormat;
  private short            _trackCount;
  private short            _ticksPerQuarterNote;    
  private long             _tracksStartPosition;
  private PositionLength[] _trackStartPositionAndLengths = null!;

  // Current states

  private long           _currentTick = 0;
  private int[]          _ticksForNextEvent;
  private long[]         _currentTrackPositions = null!;
  private string         _currentSongNameText = string.Empty;
  private string         _currentCopyrightText = string.Empty;
  private int            _currentMicrosecondsPerQuarterNote = 500_000; // = 120 bpm for default
  private byte           _currentSignatureNumerator = 4;
  private byte           _currentSignatureDenominator  = 4;
  private ChannelState[] _currentChannelStates = ChannelState.CreateArray(ChannelCount);
  private byte[]         _currentStatusBytesByTrack = null!;
  private long           _currentNoteCount = 0;
  private long           _currentEventCount = 0;

  public MidiReader(string filePath) : this(File.OpenRead(filePath), true) { }

  public MidiReader(Stream midiStream, bool ownStream = false)
  {
    _doOwnStream = ownStream;
    _baseStream = midiStream;
    ReadHeader();
    GetTrackPositions();
    _ticksForNextEvent = new int[_trackCount];
    _currentStatusBytesByTrack = new byte[_trackCount];
  }

  public short MidiFormat => _midiFormat;

  public short TrackCount => _trackCount;

  public short TicksPerQuarterNote => _ticksPerQuarterNote;

  public long CurrentTick => _currentTick;

  public string CurrentCopyrightText => _currentCopyrightText;

  public string CurrentSongNameText => _currentSongNameText;

  public int CurrentMicrosecondsPerQuarterNote => _currentMicrosecondsPerQuarterNote;

  public decimal CurrentMillisecondsPerTick => ((decimal)1 / _ticksPerQuarterNote) * CurrentMicrosecondsPerQuarterNote / 1000;

  public decimal CurrentBPM => Helpers.MicrosecondsPerQuarterNoteToBPM(_currentMicrosecondsPerQuarterNote);

  public byte CurrentSignatureNumerator => _currentSignatureNumerator;

  public byte CurrentSignatureDenominator => _currentSignatureDenominator;

  public long CurrentNoteCount => _currentNoteCount;

  public long CurrentEventCount => _currentEventCount;

  public IReadOnlyList<IReadOnlyChannelState> CurrentChannelStates => _currentChannelStates;

  public double CurrentRealtimeSpeed { get; set; }

  // TODO: current拍号

  public int ReadNextEvents(out IReadOnlyList<IEvent> events)
  {
    List<IEvent> eventList = [];
    events = eventList;

    if (_trackCount == 0)
    {
      return 0;
    }    

    int minTicksForNextEvent = int.MaxValue;

    for (short iTrack = 0; iTrack < _trackCount; iTrack++)
    {
      if (_ticksForNextEvent[iTrack] < 0)
      {
        continue;
      }

      while (_ticksForNextEvent[iTrack] == 0 && ReadEvent(iTrack) is IEvent @event)
      {        
        eventList.Add(@event);
        UpdateStateFromEvent(@event);
        _ticksForNextEvent[iTrack] = PeekNextEventDeltaTime(iTrack);
      }

      if (_ticksForNextEvent[iTrack] > 0)
      {
        minTicksForNextEvent = minTicksForNextEvent > _ticksForNextEvent[iTrack] ? _ticksForNextEvent[iTrack] : minTicksForNextEvent;
      }
    }

    if (minTicksForNextEvent == int.MaxValue)
    {
      return 0;
    }

    for (int i = 0; i < _ticksForNextEvent.Length; i++)
    {
      _ticksForNextEvent[i] -= minTicksForNextEvent;
    }
    _currentTick += minTicksForNextEvent;

    return minTicksForNextEvent;
  }

  public void Dispose()
  {
    if (_doOwnStream)
    {
      _baseStream.Dispose();
    }
    GC.SuppressFinalize(this);
  }

  private void UpdateStateFromEvent(IEvent @event)
  {
    _currentEventCount++;
    switch (@event)
    {
      case UnhandledEvent e:
      {

      }
      break;
      case NoteOffMidiEvent e:
      {
        _currentChannelStates[e.ChannelNo].SetNoteOff(e.Key);
      }
      break;
      case NoteOnMidiEvent e:
      {
        _currentNoteCount++;
        _currentChannelStates[e.ChannelNo].SetNoteOn(e.Key, e.Velocity);
      }
      break;
      case PolyphonicAftertouchMidiEvent e:
      {

      }
      break;
      case ControlChangeMidiEvent e:
      {
        _currentChannelStates[e.ChannelNo].ControlChanges[e.CCNo] = e.Value;
      }
      break;
      case ProgramChangeMidiEvent e:
      {
        _currentChannelStates[e.ChannelNo].ProgramChange = e.PCNo;
      }
      break;
      case AftertouchMidiEvent e:
      {

      }
      break;
      case PitchChangeMidiEvent e:
      {
        _currentChannelStates[e.ChannelNo].PitchChange = e.Value;
      }
      break;
      case TextMetaEvent e:
      {

      }
      break;
      case SeqNameMetaEvent e:
      {
        if (e.TrackIndex == 0)
        {
          _currentSongNameText = e.Text;
        }
      }
      break;
      case CopyrightMetaEvent e:
      {
        _currentCopyrightText = e.Text;
      }
      break;
      case TempoMetaEvent e:
      {
        _currentMicrosecondsPerQuarterNote = e.MicrosecondsPerQuarterNote;
      }
      break;
      case TimeSignatureMetaEvent e:
      {
        _currentSignatureNumerator = e.Numerator;
        _currentSignatureDenominator = e.Denominator;
      }
      break;
    }
  }

  private int PeekNextEventDeltaTime(short trackIndex)
  {
    _baseStream.Position = _currentTrackPositions[trackIndex];

    if (_baseStream.Position < _trackStartPositionAndLengths[trackIndex].Position + _trackStartPositionAndLengths[trackIndex].Length)
    {
      int result = ReadVarInt();
      _baseStream.Position = _currentTrackPositions[trackIndex];
      return result;
    }

    return -1;
  }

  private IEvent? ReadEvent(short trackIndex)
  {
    _baseStream.Position = _currentTrackPositions[trackIndex];

    if (_baseStream.Position >= _trackStartPositionAndLengths[trackIndex].Position + _trackStartPositionAndLengths[trackIndex].Length)
    {
      return null;
    }   

    try
    {
      int deltaTime = ReadVarInt();
      byte firstByte = ReadByte();
      byte firstBit = (byte)(firstByte & 0b1000_0000);

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
          case 0x04:
          case 0x05:
          case 0x06:
          case 0x07:
          {
            return new TextMetaEvent(deltaTime, trackIndex, Encoding.UTF8.GetString(metaEventData));
          }
          case 0x02:
          {
            return new CopyrightMetaEvent(deltaTime, trackIndex, Encoding.UTF8.GetString(metaEventData));
          }
          case 0x03:
          {
            return new SeqNameMetaEvent(deltaTime, trackIndex, Encoding.UTF8.GetString(metaEventData));
          }
          case 0x51:
          {
            if (metaEventDataLength == 3)
            {
              return new TempoMetaEvent(deltaTime, trackIndex, (metaEventData[0] << 16) | (metaEventData[1] << 8) | (metaEventData[2]));
            }
            else
            {
              return new UnhandledEvent(deltaTime, trackIndex);
            }
          }
          case 0x58:
          {
            if (metaEventDataLength == 4)
            {
              byte denominator = metaEventData[1] switch
              {
                1 => 2,
                2 => 4,
                3 => 8,
                4 => 16,
                5 => 32,
                6 => 64,
                7 => 128,
                _ => 4
              };
              return new TimeSignatureMetaEvent(deltaTime, trackIndex, metaEventData[0], denominator);
            }
            else
            {
              return new UnhandledEvent(deltaTime, trackIndex);
            }
          }
          default:
          return new UnhandledEvent(deltaTime, trackIndex);
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
        return new UnhandledEvent(deltaTime, trackIndex);
      }

      else if (firstByte == 0xF2)
      {
        ReadInt16();
        return new UnhandledEvent(deltaTime, trackIndex);
      }

      else if (firstByte == 0xF3)
      {
        ReadByte();
        return new UnhandledEvent(deltaTime, trackIndex);
      }

      // Midi Event
      else
      {
        byte midiStatusByte;
        byte midiEventType;
        byte channelNo;

        if (firstBit == 0b0000_0000)
        {
          midiStatusByte = _currentStatusBytesByTrack[trackIndex];
          _baseStream.Position--;
        }
        else
        {
          _currentStatusBytesByTrack[trackIndex] = midiStatusByte = firstByte;
        } 
          
        midiEventType = (byte)(midiStatusByte & 0b1111_0000);
        channelNo = (byte)(midiStatusByte & 0b0000_1111);

        switch (midiEventType)
        {
          // Note Off
          case 0b1000_0000:
          {
            byte key = ReadByte();
            byte vel = ReadByte();

            return new NoteOffMidiEvent(deltaTime, trackIndex, channelNo, key, vel);
          }

          // Note On
          case 0b1001_0000:
          {
            byte key = ReadByte();
            byte vel = ReadByte();

            return new NoteOnMidiEvent(deltaTime, trackIndex, channelNo, key, vel);
          }

          // Polyphonic After-touch
          case 0b1010_0000:
          {
            byte key = ReadByte();
            byte pressure = ReadByte();

            return new PolyphonicAftertouchMidiEvent(deltaTime, trackIndex, channelNo, key, pressure);
          }

          // Control Change
          case 0b1011_0000:
          {
            byte ccNo = ReadByte();
            byte value = ReadByte();

            return new ControlChangeMidiEvent(deltaTime, trackIndex, channelNo, ccNo, value);
          }

          // Program Change
          case 0b1100_0000:
          {
            byte pcNo = ReadByte();

            return new ProgramChangeMidiEvent(deltaTime, trackIndex, channelNo, pcNo);
          }

          // After-touch
          case 0b1101_0000:
          {
            byte pressure = ReadByte();

            return new AftertouchMidiEvent(deltaTime, trackIndex, channelNo, pressure);
          }

          // Pitch Wheel Change
          case 0b1110_0000:
          {
            byte low7 = ReadByte();
            byte high7 = ReadByte();
            short value = (short)((high7 << 7) | low7);

            return new PitchChangeMidiEvent(deltaTime, trackIndex, channelNo, value, low7, high7);
          }

          default:
          return new UnhandledEvent(deltaTime, trackIndex);
        }
      }
    }
    finally
    {
      _currentTrackPositions[trackIndex] = _baseStream.Position;
    }
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
    _midiFormat = midiFormat;

    short trackCount = ReadInt16();
    if (trackCount == 0)
    {
      throw new MidiReaderException($"Midi contains no tracks.");
    }
    _trackCount = trackCount;

    short tpq = ReadInt16();
    if (tpq <= 0)
    {
      throw new MidiReaderException($"TPQ must be greater than 0 (SMPTE is not supported!).");
    }
    _ticksPerQuarterNote = tpq;

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
