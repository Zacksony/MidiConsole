using Midi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MidiConsole.QBlock;
using static MidiConsole.ShadeBlock;

namespace MidiConsole;

public class MidiConsoleVisualizer
{
  private readonly record struct ControlInfo
  (
    byte  Pc        ,
    byte  Vol       ,
    byte  Exp       ,
    short Pitch     ,
    short PitchRange,
    short Mod       ,
    sbyte Panpot    ,
    sbyte Cutoff    ,
    sbyte Reso      ,
    sbyte Att       ,
    sbyte Dec       ,
    sbyte Rel       ,
    bool  Hold
  )
  {
    public static ControlInfo FromChannelState(IReadOnlyChannelState channelState)
    {
      return new
      (
        Pc: (byte)(channelState.ProgramChange + 1),
        Vol: channelState.GetUnsignedCCValue(7),
        Exp: channelState.GetUnsignedCCValue(11),
        Pitch: channelState.PitchChangeSigned,
        PitchRange: channelState.GetUnsignedCCValue(6),
        Mod: channelState.GetUnsignedCCValue(1),
        Panpot: (sbyte)(channelState.GetUnsignedCCValue(10) - 64),
        Cutoff: (sbyte)(channelState.GetUnsignedCCValue(74) - 64),
        Reso: (sbyte)(channelState.GetUnsignedCCValue(71) - 64),
        Att: (sbyte)(channelState.GetUnsignedCCValue(73) - 64),
        Dec: (sbyte)(channelState.GetUnsignedCCValue(75) - 64),
        Rel: (sbyte)(channelState.GetUnsignedCCValue(72) - 64),
        Hold: channelState.GetUnsignedCCValue(64) != 0
      );
    }
  }

  private static byte[] KeyBlackWhites =
  [1, 0, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1];

  private CoolConsole _console;
  private MidiReader _midi;
  private IReadOnlyChannelState[] _previousChannelStates = [.. Enumerable.Repeat(IReadOnlyChannelState.Default, MidiReader.ChannelCount)];

  public MidiConsoleVisualizer(MidiReader midiReader)
  {
    _console = new(width: 148, height: 40, targetFps: 120, title: "MidiConsole");
    _midi = midiReader;
  }

  public void Start()
  {
    _console.Start(Tick);
  }

  private void Tick()
  {
    string colorRestore = "\u001b[0m";

    string colorColFore = "\u001b[38;2;192;192;192m";
    string colorCol0 = "\u001b[48;2;24;24;24m";
    string colorCol1 = "\u001b[48;2;60;60;60m";

    string colorCCDefault = "\u001b[48;2;12;12;12m\u001b[38;2;164;164;164m";
    string colorCCHighlight = "\u001b[48;2;144;144;128m\u001b[38;2;240;240;200m";
    string colorForeGray = "\u001b[38;2;180;180;180m";
    string colorForeLightGray = "\u001b[38;2;240;240;216m";
    string colorForeSemiDark = "\u001b[38;2;120;120;120m";

    Console.SetCursorPosition(1, 1);
    if (_midi.CurrentSongNameText.Length != 0) Console.Write($"{colorForeGray}{_midi.CurrentSongNameText}.MID  -  ");
    if (_midi.CurrentCopyrightText.Length != 0) Console.Write($"{colorForeGray}{_midi.CurrentCopyrightText}");
    string programName = " MidiConsole 1.0.1 by Zacksony ";
    Console.SetCursorPosition(_console.Width - programName.Length - 1, 1);
    Console.Write($"{colorForeGray}{programName}");

    Console.CursorLeft = 1;
    Console.CursorTop += 2;
    Console.Write($"{colorForeGray}TEMPO: {colorForeLightGray}{_midi.CurrentBPM:0.00} BPM  " +
                  $"{colorForeGray}BEAT: {colorForeLightGray}{_midi.CurrentSignatureNumerator} / {_midi.CurrentSignatureDenominator}  " +
                  $"{colorForeGray}TPQ: {colorForeLightGray}{_midi.TicksPerQuarterNote}  " +
                  $"{colorForeGray}TICK: {colorForeLightGray}{_midi.CurrentTick}  " +
                  $"{colorForeGray}NOTES: {colorForeLightGray}{_midi.CurrentNoteCount}  " +
                  $"{colorForeGray}EVENTS: {colorForeLightGray}{_midi.CurrentEventCount}");

    Console.CursorLeft = 71;
    Console.CursorTop += 2;
    Console.Write($"{colorColFore}" +
                  $"{colorCol0}  PC " +
                  $"{colorCol1} VOL " +
                  $"{colorCol0} EXP " +
                  $"{colorCol1} PAN " +
                  $"{colorCol0} P. BEND " +
                  $"{colorCol1} P. RANGE " +
                  $"{colorCol0} MOD " +
                  $"{colorCol1} HOLD " +
                  $"{colorCol0} CUT " +
                  $"{colorCol1} RESO " +
                  $"{colorCol0} ATT " +
                  $"{colorCol1} DEC " +
                  $"{colorCol0} REL " +
                  $"{colorRestore}");

    int mergedKeysPosTop = Console.CursorTop;

    Console.CursorLeft = 1;
    Console.CursorTop += 2;
    UInt128 mergedKeysPressed = 0;
    foreach ((int channelIndex, IReadOnlyChannelState channelState) in _midi.CurrentChannelStates.Index())
    {
      Console.CursorLeft = 1;
      Console.Write($"{colorForeGray}CH{channelIndex + 1:00}");

      Console.CursorLeft = 6;
      Console.Write(GetKeysString(channelState.KeysStatus));

      Console.CursorLeft = 71;
      ControlInfo previous = ControlInfo.FromChannelState(_previousChannelStates[channelIndex]);
      ControlInfo current = ControlInfo.FromChannelState(channelState);
      Console.Write($"{colorCCDefault}" +
                    $"{colorCCDefault}{(previous.Pc == current.Pc ? colorCCDefault : colorCCHighlight)} {current.Pc:000} " +
                    $"{colorCCDefault}{(previous.Vol == current.Vol ? colorCCDefault : colorCCHighlight)} {current.Vol,3} " +
                    $"{colorCCDefault}{(previous.Exp == current.Exp ? colorCCDefault : colorCCHighlight)} {current.Exp,3} " +
                    $"{colorCCDefault}{(previous.Panpot == current.Panpot ? colorCCDefault : colorCCHighlight)} {(current.Panpot < 0 ? "-" : (current.Panpot == 0 ? " " : "+"))}{sbyte.Abs(current.Panpot),2} " +
                    $"{colorCCDefault}{(previous.Pitch == current.Pitch ? colorCCDefault : colorCCHighlight)}  {(current.Pitch < 0 ? "-" : (current.Pitch == 0 ? " " : "+"))}{short.Abs(current.Pitch),4}  " +
                    $"{colorCCDefault}{(previous.PitchRange == current.PitchRange ? colorCCDefault : colorCCHighlight)}    {current.PitchRange,3}   " +
                    $"{colorCCDefault}{(previous.Mod == current.Mod ? colorCCDefault : colorCCHighlight)} {current.Mod,3} " +
                    $"{colorCCDefault} {(current.Hold ? $"\u001b[38;2;240;240;200m{new string(FullBlock.ToChar(), 4)}" : $"{colorForeSemiDark}{new string(Shade1.ToChar(), 4)}")} " +
                    $"{colorCCDefault}{(previous.Cutoff == current.Cutoff ? colorCCDefault : colorCCHighlight)} {(current.Cutoff < 0 ? "-" : (current.Cutoff == 0 ? " " : "+"))}{sbyte.Abs(current.Cutoff),2} " +
                    $"{colorCCDefault}{(previous.Reso == current.Reso ? colorCCDefault : colorCCHighlight)}  {(current.Reso < 0 ? "-" : (current.Reso == 0 ? " " : "+"))}{sbyte.Abs(current.Reso),2} " +
                    $"{colorCCDefault}{(previous.Att == current.Att ? colorCCDefault : colorCCHighlight)} {(current.Att < 0 ? "-" : (current.Att == 0 ? " " : "+"))}{sbyte.Abs(current.Att),2} " +
                    $"{colorCCDefault}{(previous.Dec == current.Dec ? colorCCDefault : colorCCHighlight)} {(current.Dec < 0 ? "-" : (current.Dec == 0 ? " " : "+"))}{sbyte.Abs(current.Dec),2} " +
                    $"{colorCCDefault}{(previous.Rel == current.Rel ? colorCCDefault : colorCCHighlight)} {(current.Rel < 0 ? "-" : (current.Rel == 0 ? " " : "+"))}{sbyte.Abs(current.Rel),2} " +
                    $"{colorRestore}");

      Console.CursorLeft = 1;
      Console.CursorTop += 2;

      mergedKeysPressed |= channelState.KeysPressed;
      _previousChannelStates[channelIndex] = channelState.DeepClone();
    }

    Console.CursorTop = mergedKeysPosTop;
    IReadOnlyList<MidiKeyStatus> mergedKeysStatus = KeysPressedToKeysStatus(mergedKeysPressed);
    Console.CursorLeft = 1;
    Console.Write($"{colorForeGray} ALL");

    Console.CursorLeft = 6;
    Console.Write(GetKeysString(mergedKeysStatus, "200;200;200", "200;200;200"));
  }

  private static IReadOnlyList<MidiKeyStatus> KeysPressedToKeysStatus(UInt128 keysPressed)
  {
    MidiKeyStatus[] result = new MidiKeyStatus[128];
    for (int i = 0; i < 128; i++)
    {
      result[i] = new(((keysPressed >> i) & 1) == 1, 1);
    }
    return result;
  }

  private static string GetKeysString(IReadOnlyList<MidiKeyStatus> keys, string keyBlackDownColor = "240;240;216", string keyWhiteDownColor = "240;240;216")
  {
    string keyBlackUpColor = "12;12;12";
    string keyWhiteUpColor = "28;28;28";

    StringBuilder sb = new(5120);
    int halfKeyCount = keys.Count / 2;
    for (int i = 0; i < halfKeyCount; i++)
    {
      int iKey0 = i * 2;
      int iKey1 = i * 2 + 1;
      bool isKey0Black = KeyBlackWhites[iKey0 % 12] == 0;
      bool isKey1Black = KeyBlackWhites[iKey1 % 12] == 0;
      MidiKeyStatus key0 = keys[i * 2];
      MidiKeyStatus key1 = keys[i * 2 + 1];
      if (!key0.Pressed && !key1.Pressed)
      {
        sb.Append($"\x1b[38;2;{(isKey0Black ? keyBlackUpColor : keyWhiteUpColor)}m\x1b[48;2;{(isKey1Black ? keyBlackUpColor : keyWhiteUpColor)}m{(Q2 | Q3).ToChar()}\x1b[0m");
      }
      else if (!key0.Pressed && key1.Pressed)
      {
        sb.Append($"\x1b[38;2;{(isKey0Black ? keyBlackUpColor : keyWhiteUpColor)}m\x1b[48;2;{(isKey1Black ? keyBlackDownColor : keyWhiteDownColor)}m{(Q2 | Q3).ToChar()}\x1b[0m");
      }
      else if (key0.Pressed && !key1.Pressed)
      {
        sb.Append($"\x1b[38;2;{(isKey0Black ? keyBlackDownColor : keyWhiteDownColor)}m\x1b[48;2;{(isKey1Black ? keyBlackUpColor : keyWhiteUpColor)}m{(Q2 | Q3).ToChar()}\x1b[0m");
      }
      else if (key0.Pressed && key1.Pressed)
      {
        sb.Append($"\x1b[38;2;{(isKey0Black ? keyBlackDownColor : keyWhiteDownColor)}m\x1b[48;2;{(isKey1Black ? keyBlackDownColor : keyWhiteDownColor)}m{(Q2 | Q3).ToChar()}\x1b[0m");
      }
    }

    return sb.ToString();
  }
}
