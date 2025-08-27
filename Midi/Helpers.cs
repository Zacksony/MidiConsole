using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Midi;

internal static class Helpers
{
  public static double MicrosecondsPerQuarterNoteToBPM(int microsecondsPerQuarterNote)
  {
    return 1d / microsecondsPerQuarterNote * 60 * 1_000_000;
  }
}
