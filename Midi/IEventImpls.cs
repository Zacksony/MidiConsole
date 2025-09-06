using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Midi;

public readonly record struct UnhandledEvent                (int DeltaTime, short TrackIndex) : IEvent;
public readonly record struct NoteOffMidiEvent              (int DeltaTime, short TrackIndex, byte ChannelNo, byte Key, byte Velocity) : IEvent;
public readonly record struct NoteOnMidiEvent               (int DeltaTime, short TrackIndex, byte ChannelNo, byte Key, byte Velocity) : IEvent;
public readonly record struct PolyphonicAftertouchMidiEvent (int DeltaTime, short TrackIndex, byte ChannelNo, byte Key, byte Pressure) : IEvent;
public readonly record struct ControlChangeMidiEvent        (int DeltaTime, short TrackIndex, byte ChannelNo, byte CCNo, byte Value) : IEvent;
public readonly record struct ProgramChangeMidiEvent        (int DeltaTime, short TrackIndex, byte ChannelNo, byte PCNo) : IEvent;
public readonly record struct AftertouchMidiEvent           (int DeltaTime, short TrackIndex, byte ChannelNo, byte Pressure) : IEvent;
public readonly record struct PitchChangeMidiEvent          (int DeltaTime, short TrackIndex, byte ChannelNo, short Value, byte Low7, byte High7) : IEvent;
public readonly record struct TextMetaEvent                 (int DeltaTime, short TrackIndex, string Text) : IEvent;
public readonly record struct SeqNameMetaEvent              (int DeltaTime, short TrackIndex, string Text) : IEvent;
public readonly record struct CopyrightMetaEvent            (int DeltaTime, short TrackIndex, string Text) : IEvent;
public readonly record struct TempoMetaEvent                (int DeltaTime, short TrackIndex, int MicrosecondsPerQuarterNote) : IEvent;
public readonly record struct TimeSignatureMetaEvent        (int DeltaTime, short TrackIndex, byte Numerator, byte Denominator) : IEvent;
