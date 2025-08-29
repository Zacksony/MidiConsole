using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Midi;

internal class ChannelState : IReadOnlyChannelState
{
  public const int KeyCount = 128;

  public MidiKeyStatus[] Keys { get; } = new MidiKeyStatus[KeyCount];

  public Dictionary<byte, byte> ControlChanges { get; } = [];

  public byte ProgramChange { get; set; }

  public short PitchChange { get; set; }

  public static ChannelState[] CreateArray(int length)
  {
    ChannelState[] result = new ChannelState[length];
    for (int i = 0; i < length; i++)
    {
      result[i] = new ChannelState();
    }
    return result;
  }

  public void SetNoteOn(byte key, byte velocity)
  {
    Keys[key] = new(true, velocity);
  }

  public void SetNoteOff(byte key)
  {
    Keys[key] = new(false, 0);
  }

  IReadOnlyList<MidiKeyStatus> IReadOnlyChannelState.Keys => Keys;
  IReadOnlyDictionary<byte, byte> IReadOnlyChannelState.ControlChanges => ControlChanges;
  byte IReadOnlyChannelState.ProgramChange => ProgramChange;
  short IReadOnlyChannelState.PitchChange => PitchChange;
}
