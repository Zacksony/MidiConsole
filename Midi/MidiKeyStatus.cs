using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Midi;

public readonly record struct MidiKeyStatus(bool Pressed, byte Velocity);
