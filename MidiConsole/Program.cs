using Midi;

namespace MidiConsole
{
  internal class Program
  {
    static void Main(string[] args)
    {
      using MidiReader midi = new(@"D:\MIDI\PROJ\Cyber Night\cyber-night.mid");
    }
  }
}
