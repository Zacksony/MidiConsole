using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Midi;

public interface IEvent
{
  public int DeltaTime { get; }
}

public static class IEventExtensions
{
  public static bool IfIsThen<T>(this IEvent? @this, Action<T> action) where T : IEvent
  {
    if (@this is T specific)
    {
      action?.Invoke(specific);
      return true;
    }
    else
    {
      return false;
    }
  }
}
