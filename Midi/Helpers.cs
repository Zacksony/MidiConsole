using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Midi;

internal static class Helpers
{
  public static decimal MicrosecondsPerQuarterNoteToBPM(int microsecondsPerQuarterNote)
  {
    return (decimal)1 / microsecondsPerQuarterNote * 60 * 1_000_000;
  }
}
