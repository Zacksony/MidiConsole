using Midi;
using System.Diagnostics;

namespace MidiConsole;

internal class Program
{
  static void Main(string[] args)
  {
    //string midiFilePath = @"D:\MIDI\PROJ\Cyber Night\cyber-night.mid";
    string midiFilePath = @"D:\MIDI\Black MIDIs\EVANS_ZUMN_finished_AS.mid";
    byte[] midiBytes = File.ReadAllBytes(midiFilePath);
    using MemoryStream midiStream = new(midiBytes);      
    using MidiReader midi = new(midiStream);

    AnimatedConsole animeConsole = new(128, 24);

    Task drawingTask = Task.Run(() =>
    {
      animeConsole.Start(sb =>
      {
        foreach (IReadOnlyChannelState channelState in midi.CurrentChannelStates)
        {
          sb.AppendLine(new string([.. channelState.Keys.Select(k => k.Pressed ? "@" : "-").SelectMany(s => s)]));
        }
      });
    });

    Task midiTask = Task.Run(() =>
    {
      Stopwatch watch = new();
      int ticksForNextTick = 0;
      while ((ticksForNextTick = midi.ReadNextEvents()) > 0)
      {
        double millisecondsForNextTick = ticksForNextTick * (double)midi.CurrentMillisecondsPerTick;
        watch.Restart();
        while (watch.Elapsed.TotalMilliseconds < millisecondsForNextTick) { /* Spin */ }
      }
    });

    Task.WaitAll(drawingTask, midiTask);
  }
}
