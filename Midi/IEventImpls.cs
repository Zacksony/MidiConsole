using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Midi;

public readonly record struct UnhandledEvent               (int DeltaTime) : IEvent;
public readonly record struct NoteOffMidiEvent             (int DeltaTime, byte ChannelNo, byte Key, byte Velocity) : IEvent;
public readonly record struct NoteOnMidiEvent              (int DeltaTime, byte ChannelNo, byte Key, byte Velocity) : IEvent;
public readonly record struct PolyphonicAftertouchMidiEvent(int DeltaTime, byte ChannelNo, byte Key, byte Pressure) : IEvent;
public readonly record struct ControlChangeMidiEvent       (int DeltaTime, byte ChannelNo, byte CCNo, byte Value) : IEvent;
public readonly record struct ProgramChangeMidiEvent       (int DeltaTime, byte ChannelNo, byte PCNo) : IEvent;
public readonly record struct AftertouchMidiEvent          (int DeltaTime, byte ChannelNo, byte Pressure) : IEvent;
public readonly record struct PitchChangeMidiEvent         (int DeltaTime, byte ChannelNo, short Value) : IEvent;
public readonly record struct TextMetaEvent                (int DeltaTime, string Text) : IEvent;
public readonly record struct CopyrightMetaEvent           (int DeltaTime, string Text) : IEvent;
public readonly record struct TempoMetaEvent               (int DeltaTime, int MicrosecondsPerQuarterNote) : IEvent;
