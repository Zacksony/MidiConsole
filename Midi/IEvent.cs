namespace Midi;

public interface IEvent
{
  public short TrackIndex { get; }
  public int DeltaTime { get; }
}
