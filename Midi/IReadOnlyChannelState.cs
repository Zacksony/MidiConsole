using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Midi;

public interface IReadOnlyChannelState
{
  public IReadOnlyList<MidiKeyStatus> Keys { get; }

  public IReadOnlyDictionary<byte, byte> ControlChanges { get; }

  public byte ProgramChange { get; }

  public short PitchChange { get; }

  public sealed byte GetUnsignedCCValue(byte ccNo)
  {
    return ControlChanges.GetValueOrDefault(ccNo, (byte)0);
  }

  public sealed sbyte GetSignedCCValue(byte ccNo)
  {
    return (sbyte)GetUnsignedCCValue(ccNo);
  }
}
