using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MidiConsole;

public class CoolConsole
{
  const int GWL_STYLE = -16;
  const int WS_SIZEBOX = 0x00040000;
  const int WS_MAXIMIZEBOX = 0x00010000;
 
  [DllImport("kernel32.dll", SetLastError = true)]
  static extern IntPtr GetConsoleWindow();
 
  [DllImport("user32.dll", SetLastError = true)]
  static extern int GetWindowLong(IntPtr hWnd, int nIndex);
 
  [DllImport("user32.dll", SetLastError = true)]
  static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

  private readonly int _width;
  private readonly int _height;
  private readonly int _targetFps;
  private readonly double _frameTime;
  private readonly Stopwatch _stopwatch;
  private bool _isRunning;

  public int Width => _width;

  public int Height => _height;

  public CoolConsole(int width = 40, int height = 20, int targetFps = 60, string title = "AnimatedConsole")
  {
    _width = width;
    _height = height;
    _targetFps = targetFps;
    _frameTime = 1000.0 / targetFps;
    _stopwatch = new Stopwatch();    

    IntPtr consoleHandle = GetConsoleWindow();
    int style = GetWindowLong(consoleHandle, GWL_STYLE);
    style &= ~WS_SIZEBOX;
    style &= ~WS_MAXIMIZEBOX;
    _ = SetWindowLong(consoleHandle, GWL_STYLE, style);

    Console.SetWindowSize(width, height);
    Console.SetBufferSize(width, height);
    Console.CursorVisible = false;
    Console.OutputEncoding = Encoding.Unicode;
    Console.Title = title;
  }

  public void Start(Action tick)
  {
    _isRunning = true;

    while (_isRunning)
    {
      _stopwatch.Restart();      

      tick.Invoke();

      while (_stopwatch.Elapsed.TotalMilliseconds < _frameTime) { /* Spin */ } 
    }
  }

  public void Stop()
  {
    _isRunning = false;
  }
}