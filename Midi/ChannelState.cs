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

  public MidiKeyStatus[] KeysStatus { get; private set; } = new MidiKeyStatus[KeyCount];

  public UInt128 KeysPressed { get; private set; } = 0;

  public Dictionary<byte, byte> ControlChanges { get; private set; } = [];

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
    KeysStatus[key] = new(true, velocity);
    KeysPressed |= (UInt128)1 << key;
  }

  public void SetNoteOff(byte key)
  {
    KeysStatus[key] = new(false, 0);
    KeysPressed &= ~((UInt128)1 << key);
  }

  public IReadOnlyChannelState DeepClone()
  {
    ChannelState state = new()
    {
      KeysStatus = [.. KeysStatus],
      ControlChanges = ControlChanges.ToDictionary(),
      ProgramChange = ProgramChange,
      PitchChange = PitchChange
    };
    return state;
  }

  IReadOnlyList<MidiKeyStatus> IReadOnlyChannelState.KeysStatus => KeysStatus;
  IReadOnlyDictionary<byte, byte> IReadOnlyChannelState.ControlChanges => ControlChanges;
  UInt128 IReadOnlyChannelState.KeysPressed => KeysPressed;
  byte IReadOnlyChannelState.ProgramChange => ProgramChange;
  short IReadOnlyChannelState.PitchChange => PitchChange;
}
