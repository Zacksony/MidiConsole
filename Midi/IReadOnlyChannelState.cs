using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Midi;

public interface IReadOnlyChannelState
{
  public sealed static IReadOnlyChannelState Default => DefaultImpl.Instance;

  public IReadOnlyList<MidiKeyStatus> KeysStatus { get; }

  public UInt128 KeysPressed { get; }

  public IReadOnlyDictionary<byte, byte> ControlChanges { get; }

  public byte ProgramChange { get; }

  public short PitchChange { get; }

  public short PitchChangeSigned => (short)(PitchChange - 8192);  

  public IReadOnlyChannelState DeepClone();

  public sealed byte GetUnsignedCCValue(byte ccNo)
  {
    return ControlChanges.GetValueOrDefault(ccNo, (byte)0);
  }

  public sealed sbyte GetSignedCCValue(byte ccNo)
  {
    return (sbyte)GetUnsignedCCValue(ccNo);
  }

  private class DefaultImpl : IReadOnlyChannelState
  {
    public const int KeyCount = 128;

    private static readonly Lazy<DefaultImpl> _instanceHolder = new(() => new DefaultImpl());

    public static DefaultImpl Instance => _instanceHolder.Value;

    public IReadOnlyList<MidiKeyStatus> KeysStatus { get; } = new MidiKeyStatus[KeyCount];

    public UInt128 KeysPressed { get; } = 0;

    public IReadOnlyDictionary<byte, byte> ControlChanges { get; } = (Dictionary<byte, byte>)[];

    public byte ProgramChange { get; } = 0;

    public short PitchChange { get; } = 0;

    public IReadOnlyChannelState DeepClone() => Instance;
  }
}
