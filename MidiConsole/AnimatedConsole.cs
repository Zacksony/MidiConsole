using System.Diagnostics;
using System.Text;

namespace MidiConsole;

public class AnimatedConsole
{
  private readonly int _width;
  private readonly int _height;
  private readonly int _targetFps;
  private readonly double _frameTime;
  private readonly Stopwatch _stopwatch;
  private bool _isRunning;

  public int Width => _width;

  public int Height => _height;

  public AnimatedConsole(int width = 40, int height = 20, int targetFps = 60)
  {
    _width = width;
    _height = height;
    _targetFps = targetFps;
    _frameTime = 1000.0 / targetFps;
    _stopwatch = new Stopwatch();
    Console.CursorVisible = false;
  }

  public void Start(Action<StringBuilder> drawAction)
  {
    _isRunning = true;

    while (_isRunning)
    {
      _stopwatch.Restart();

      StringBuilder buffer = new StringBuilder(_width * _height);
      drawAction.Invoke(buffer);

      Console.SetCursorPosition(0, 0);
      Console.Write(buffer.ToString());

      _stopwatch.Stop();
      double elapsed = _stopwatch.Elapsed.TotalMilliseconds;
      if (elapsed < _frameTime)
      {
        Thread.Sleep((int)(_frameTime - elapsed));
      }
    }
  }

  public void Stop()
  {
    _isRunning = false;
  }
}