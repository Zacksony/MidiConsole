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

  private record struct ControlInfoHighLightFadeTimer
  (
    int Pc        ,
    int Vol       ,
    int Exp       ,
    int Pitch     ,
    int PitchRange,
    int Mod       ,
    int Panpot    ,
    int Cutoff    ,
    int Reso      ,
    int Att       ,
    int Dec       ,
    int Rel       
  );

  private const int MaxControlHighLightFadeTime = 40; // 40
  private const int MaxKeyboardFadeTime = 6; // 6

  private readonly static IReadOnlyList<int> KeyBlackWhites =
  [1, 0, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1];

  private CoolConsole _console;
  private MidiReader _midi;
  private IReadOnlyChannelState[] _previousChannelStates = [.. Enumerable.Repeat(IReadOnlyChannelState.Default, MidiReader.ChannelCount)];
  private ControlInfoHighLightFadeTimer[] _controlFadeTimers = new ControlInfoHighLightFadeTimer[MidiReader.ChannelCount];
  private int[,] _keyboardFadeTimers = new int[MidiReader.ChannelCount + 1, 128];

  public MidiConsoleVisualizer(MidiReader midiReader)
  {
    _console = new(width: 148, height: 40, targetFps: 60, title: "MidiConsole");
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
      for (int key = 0; key < 128; key++)
      {
        if (channelState.KeysStatus[key].Pressed)
        {
          _keyboardFadeTimers[channelIndex + 1, key] = MaxKeyboardFadeTime;
        }
        else
        {
          _keyboardFadeTimers[channelIndex + 1, key] = int.Max(0, _keyboardFadeTimers[channelIndex + 1, key] - 1);
        }
      }
      Console.Write(GetKeysString(channelState.KeysStatus, channelIndex + 1));

      Console.CursorLeft = 71;
      ControlInfo previous = ControlInfo.FromChannelState(_previousChannelStates[channelIndex]);
      ControlInfo current = ControlInfo.FromChannelState(channelState);
      _controlFadeTimers[channelIndex].Pc = previous.Pc == current.Pc ? int.Max(0, _controlFadeTimers[channelIndex].Pc - 1) : MaxControlHighLightFadeTime;
      _controlFadeTimers[channelIndex].Vol = previous.Vol == current.Vol ? int.Max(0, _controlFadeTimers[channelIndex].Vol - 1) : MaxControlHighLightFadeTime;
      _controlFadeTimers[channelIndex].Exp = previous.Exp == current.Exp ? int.Max(0, _controlFadeTimers[channelIndex].Exp - 1) : MaxControlHighLightFadeTime;
      _controlFadeTimers[channelIndex].Pitch = previous.Pitch == current.Pitch ? int.Max(0, _controlFadeTimers[channelIndex].Pitch - 1) : MaxControlHighLightFadeTime;
      _controlFadeTimers[channelIndex].PitchRange = previous.PitchRange == current.PitchRange ? int.Max(0, _controlFadeTimers[channelIndex].PitchRange - 1) : MaxControlHighLightFadeTime;
      _controlFadeTimers[channelIndex].Mod = previous.Mod == current.Mod ? int.Max(0, _controlFadeTimers[channelIndex].Mod - 1) : MaxControlHighLightFadeTime;
      _controlFadeTimers[channelIndex].Panpot = previous.Panpot == current.Panpot ? int.Max(0, _controlFadeTimers[channelIndex].Panpot - 1) : MaxControlHighLightFadeTime;
      _controlFadeTimers[channelIndex].Cutoff = previous.Cutoff == current.Cutoff ? int.Max(0, _controlFadeTimers[channelIndex].Cutoff - 1) : MaxControlHighLightFadeTime;
      _controlFadeTimers[channelIndex].Reso = previous.Reso == current.Reso ? int.Max(0, _controlFadeTimers[channelIndex].Reso - 1) : MaxControlHighLightFadeTime;
      _controlFadeTimers[channelIndex].Att = previous.Att == current.Att ? int.Max(0, _controlFadeTimers[channelIndex].Att - 1) : MaxControlHighLightFadeTime;
      _controlFadeTimers[channelIndex].Dec = previous.Dec == current.Dec ? int.Max(0, _controlFadeTimers[channelIndex].Dec - 1) : MaxControlHighLightFadeTime;
      _controlFadeTimers[channelIndex].Rel = previous.Rel == current.Rel ? int.Max(0, _controlFadeTimers[channelIndex].Rel - 1) : MaxControlHighLightFadeTime;
      Console.Write($"{colorCCDefault}" +
                    $"{colorCCDefault}{ControlTimerToColor(_controlFadeTimers[channelIndex].Pc)} {current.Pc:000} " +
                    $"{colorCCDefault}{ControlTimerToColor(_controlFadeTimers[channelIndex].Vol)} {current.Vol,3} " +
                    $"{colorCCDefault}{ControlTimerToColor(_controlFadeTimers[channelIndex].Exp)} {current.Exp,3} " +
                    $"{colorCCDefault}{ControlTimerToColor(_controlFadeTimers[channelIndex].Panpot)} {(current.Panpot < 0 ? "-" : (current.Panpot == 0 ? " " : "+"))}{sbyte.Abs(current.Panpot),2} " +
                    $"{colorCCDefault}{ControlTimerToColor(_controlFadeTimers[channelIndex].Pitch)}  {(current.Pitch < 0 ? "-" : (current.Pitch == 0 ? " " : "+"))}{short.Abs(current.Pitch),4}  " +
                    $"{colorCCDefault}{ControlTimerToColor(_controlFadeTimers[channelIndex].PitchRange)}    {current.PitchRange,3}   " +
                    $"{colorCCDefault}{ControlTimerToColor(_controlFadeTimers[channelIndex].Mod)} {current.Mod,3} " +
                    $"{colorCCDefault} {(current.Hold ? $"\u001b[38;2;240;240;200m{new string(FullBlock.ToChar(), 4)}" : $"{colorForeSemiDark}{new string(Shade1.ToChar(), 4)}")} " +
                    $"{colorCCDefault}{ControlTimerToColor(_controlFadeTimers[channelIndex].Cutoff)} {(current.Cutoff < 0 ? "-" : (current.Cutoff == 0 ? " " : "+"))}{sbyte.Abs(current.Cutoff),2} " +
                    $"{colorCCDefault}{ControlTimerToColor(_controlFadeTimers[channelIndex].Reso)}  {(current.Reso < 0 ? "-" : (current.Reso == 0 ? " " : "+"))}{sbyte.Abs(current.Reso),2} " +
                    $"{colorCCDefault}{ControlTimerToColor(_controlFadeTimers[channelIndex].Att)} {(current.Att < 0 ? "-" : (current.Att == 0 ? " " : "+"))}{sbyte.Abs(current.Att),2} " +
                    $"{colorCCDefault}{ControlTimerToColor(_controlFadeTimers[channelIndex].Dec)} {(current.Dec < 0 ? "-" : (current.Dec == 0 ? " " : "+"))}{sbyte.Abs(current.Dec),2} " +
                    $"{colorCCDefault}{ControlTimerToColor(_controlFadeTimers[channelIndex].Rel)} {(current.Rel < 0 ? "-" : (current.Rel == 0 ? " " : "+"))}{sbyte.Abs(current.Rel),2} " +
                    $"{colorRestore}");

      Console.CursorLeft = 1;
      Console.CursorTop += 2;

      mergedKeysPressed |= channelState.KeysPressed;
      _previousChannelStates[channelIndex] = channelState.DeepClone();
    }

    Console.CursorTop = mergedKeysPosTop;    
    Console.CursorLeft = 1;
    Console.Write($"{colorForeGray} ALL");

    Console.CursorLeft = 6;
    MidiKeyStatus[] mergedKeysStatus = KeysPressedToKeysStatus(mergedKeysPressed);
    for (int key = 0; key < 128; key++)
    {
      if (mergedKeysStatus[key].Pressed)
      {
        _keyboardFadeTimers[0, key] = MaxKeyboardFadeTime;
      }
      else
      {
        _keyboardFadeTimers[0, key] = int.Max(0, _keyboardFadeTimers[0, key] - 1);
      }
    }
    Console.Write(GetKeysString(mergedKeysStatus, 0));
  }

  private string GetKeysString(IReadOnlyList<MidiKeyStatus> keys, int channelNumber, byte maxR = 234, byte maxG = 234, byte maxB = 208)
  {
    byte blackMinR = 12;
    byte blackMinG = 12;
    byte blackMinB = 12;
    byte whiteMinR = 28;
    byte whiteMinG = 28;
    byte whiteMinB = 28;
    
    string keyWhiteUpColor = $"{whiteMinR};{whiteMinG};{whiteMinB}";
    string keyBlackUpColor = $"{blackMinR};{blackMinG};{blackMinB}";

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
      int key0FadeTimer = _keyboardFadeTimers[channelNumber, iKey0];
      int key1FadeTimer = _keyboardFadeTimers[channelNumber, iKey1];
      double key0TimerRatio = (double)key0FadeTimer / MaxKeyboardFadeTime;
      double key1TimerRatio = (double)key1FadeTimer / MaxKeyboardFadeTime;
      byte key0ColorR = key0FadeTimer == 0 ? (isKey0Black ? blackMinR : whiteMinR) : (byte)double.Clamp(whiteMinR + (maxR - whiteMinR) * key0TimerRatio, 0, 255);
      byte key0ColorG = key0FadeTimer == 0 ? (isKey0Black ? blackMinG : whiteMinG) : (byte)double.Clamp(whiteMinG + (maxG - whiteMinG) * key0TimerRatio, 0, 255);
      byte key0ColorB = key0FadeTimer == 0 ? (isKey0Black ? blackMinB : whiteMinB) : (byte)double.Clamp(whiteMinB + (maxB - whiteMinB) * key0TimerRatio, 0, 255);
      byte key1ColorR = key1FadeTimer == 0 ? (isKey1Black ? blackMinR : whiteMinR) : (byte)double.Clamp(whiteMinR + (maxR - whiteMinR) * key1TimerRatio, 0, 255);
      byte key1ColorG = key1FadeTimer == 0 ? (isKey1Black ? blackMinG : whiteMinG) : (byte)double.Clamp(whiteMinG + (maxG - whiteMinG) * key1TimerRatio, 0, 255);
      byte key1ColorB = key1FadeTimer == 0 ? (isKey1Black ? blackMinB : whiteMinB) : (byte)double.Clamp(whiteMinB + (maxB - whiteMinB) * key1TimerRatio, 0, 255);
      string key0Color = $"{key0ColorR};{key0ColorG};{key0ColorB}";
      string key1Color = $"{key1ColorR};{key1ColorG};{key1ColorB}";

      sb.Append($"\x1b[38;2;{key0Color}m\x1b[48;2;{key1Color}m{(Q2 | Q3).ToChar()}\x1b[0m");
    }

    return sb.ToString();
  }

  private static string ControlTimerToColor(int timer)
  {
    double timerRatio = (double)timer / MaxControlHighLightFadeTime;

    byte backMaxR = 128;
    byte backMaxG = 128;
    byte backMaxB = 110;
    byte foreMaxR = 255;
    byte foreMaxG = 255;
    byte foreMaxB = 255;

    byte backMinR = 12;
    byte backMinG = 12;
    byte backMinB = 12;
    byte foreMinR = 164;
    byte foreMinG = 164;
    byte foreMinB = 164;

    return $"\u001b[48;2;" +
           $"{(byte)double.Clamp(backMinR + (backMaxR - backMinR) * timerRatio, 0, 255)};" +
           $"{(byte)double.Clamp(backMinG + (backMaxG - backMinG) * timerRatio, 0, 255)};" +
           $"{(byte)double.Clamp(backMinB + (backMaxB - backMinB) * timerRatio, 0, 255)}m" +
           $"\u001b[38;2;" +
           $"{(byte)double.Clamp(foreMinR + (foreMaxR - foreMinR) * timerRatio, 0, 255)};" +
           $"{(byte)double.Clamp(foreMinG + (foreMaxG - foreMinG) * timerRatio, 0, 255)};" +
           $"{(byte)double.Clamp(foreMinB + (foreMaxB - foreMinB) * timerRatio, 0, 255)}m";
  }

  private static MidiKeyStatus[] KeysPressedToKeysStatus(UInt128 keysPressed)
  {
    MidiKeyStatus[] result = new MidiKeyStatus[128];
    for (int i = 0; i < 128; i++)
    {
      result[i] = new(((keysPressed >> i) & 1) == 1, 1);
    }
    return result;
  }  
}
