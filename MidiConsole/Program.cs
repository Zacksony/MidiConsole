using Midi;
using System.Collections.Concurrent;
using System.Diagnostics;

using static MidiConsole.QBlock;
using static MidiConsole.ShadeBlock;

namespace MidiConsole;

internal static class Program
{
  private readonly static bool EnableAudio = true;
  static void Main(string[] args)
  {
    //string midiFilePath = @"D:\MIDI\PROJ\Cyber Night\cyber-night.mid";
    //string midiFilePath = @"D:\MIDI\MIDIs\Piano Songs\TB\THE ULTIMATE 200 ANIME SONGS PIANO MEDLEY [with l.mid";
    //string midiFilePath = @"D:\MIDI\Black MIDIs\EVANS_ZUMN_finished_AS.mid";
    //string midiFilePath = @"D:\MIDI\MIDIs\Abstract64-CarveYourOwnPath.msgs.mid";
    //string midiFilePath = @"D:\MIDI\MIDIs\passport.mid";
    //string midiFilePath = @"D:\MIDI\MIDIs\Piano Songs\Slow Moon.mid";
    //string midiFilePath = @"D:\MIDI\MIDIs\Piano Songs\TB\zyg\One Last Kiss Animenz & Pianeet Duet.mid";
    //string midiFilePath = @"D:\MIDI\Black MIDIs\[Black Score] - I'm So Happy.mid";
    //string midiFilePath = @"D:\MIDI\Huge MIDIs\9KX2 18 Million Notes.mid";
    //string midiFilePath = @"D:\MIDI\Huge MIDIs\Erosion.mid";
    //string midiFilePath = @"D:\MIDI\Huge MIDIs\kentite.mid";
    //string midiFilePath = @"D:\MIDI\Huge MIDIs\反氯化苯2.mid";
    //string midiFilePath = @"D:\MIDI\Black MIDIs\真っ黒フランドール・S 修正版.mid";
    //string midiFilePath = @"D:\MIDI\PROJ\Chain\Chain.mid";
    string midiFilePath = @"D:\MIDI\MIDIs\メドレーに命をかけて.mid";

    byte[] midiBytes = File.ReadAllBytes(midiFilePath);
    using MemoryStream midiStream = new(midiBytes);
    using MidiReader midi = new(midiStream);
    //using MidiReader midi = new(midiFilePath);

    // initialize Synth
    ConcurrentQueue<IReadOnlyList<IEvent>>? eventQueue = null;
    if (EnableAudio)
    {
      KDMAPI.InitializeKDMAPIStream();
      KDMAPI.ResetKDMAPIStream();
      KDMAPI.SendDirectData(0x0);
      eventQueue = [];
      Task audioTask = Task.Run(() =>
      {
        while (true)
        {
          if (eventQueue.TryDequeue(out IReadOnlyList<IEvent>? events))
          {
            foreach (var @event in events)
            {
              SendToSynth(@event);
            }
          }
        }
      });
    }

    bool isPlaying = false;

    Task inputTask = Task.Run(() =>
    {
      while (Console.ReadKey(true) is ConsoleKeyInfo consoleKeyInfo)
      {
        switch (consoleKeyInfo.Key)
        {
          case ConsoleKey.Spacebar:
          {
            isPlaying = !isPlaying;
          }
          break;
        }
      }
    });

    Task drawingTask = Task.Run(() =>
    {
      new MidiConsoleVisualizer(midi).Start();
    });

    Task midiTask = Task.Run(() =>
    {
      Stopwatch watch = new();
      int ticksForNextJump = 0;

      IReadOnlyList<IEvent>? events = null;
      do
      {
        while (!isPlaying) { /* Spin */ }
        watch.Restart();
        double millisecondsForNextTick = ticksForNextJump * (double)midi.CurrentMillisecondsPerTick;
        if (EnableAudio && events != null) eventQueue?.Enqueue(events);
        while (watch.Elapsed.TotalMilliseconds < millisecondsForNextTick) { /* Spin */ }
      }
      while ((ticksForNextJump = midi.ReadNextEvents(out events)) > 0);
    });

    Task.WaitAll(drawingTask, midiTask, inputTask);
    if (EnableAudio) KDMAPI.TerminateKDMAPIStream();
  }

  private static void SendToSynth(IEvent @event)
  {
    switch (@event)
    {
      case UnhandledEvent e:
      {

      }
      break;
      case NoteOffMidiEvent e:
      {
        _ = KDMAPI.SendDirectData((uint)((0b1000_0000 | e.ChannelNo) | (e.Key << 8) | (e.Velocity << 16)));
      }
      break;
      case NoteOnMidiEvent e:
      {
        _ = KDMAPI.SendDirectData((uint)((0b1001_0000 | e.ChannelNo) | (e.Key << 8) | (e.Velocity << 16)));
      }
      break;
      case PolyphonicAftertouchMidiEvent e:
      {
        _ = KDMAPI.SendDirectData((uint)((0b1010_0000 | e.ChannelNo) | (e.Key << 8) | (e.Pressure << 16)));
      }
      break;
      case ControlChangeMidiEvent e:
      {
        _ = KDMAPI.SendDirectData((uint)((0b1011_0000 | e.ChannelNo) | (e.CCNo << 8) | (e.Value << 16)));
      }
      break;
      case ProgramChangeMidiEvent e:
      {
        _ = KDMAPI.SendDirectData((uint)((0b1100_0000 | e.ChannelNo) | (e.PCNo << 8)));
      }
      break;
      case AftertouchMidiEvent e:
      {
        _ = KDMAPI.SendDirectData((uint)((0b1010_0000 | e.ChannelNo) | (e.Pressure << 8)));
      }
      break;
      case PitchChangeMidiEvent e:
      {
        _ = KDMAPI.SendDirectData((uint)((0b1110_0000 | e.ChannelNo) | (e.Low7 << 8) | (e.High7 << 16)));
      }
      break;
      case TextMetaEvent e:
      {

      }
      break;
      case CopyrightMetaEvent e:
      {

      }
      break;
      case TempoMetaEvent e:
      {

      }
      break;
    }
  }
}
