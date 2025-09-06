using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MidiConsole;

public enum QBlock : byte
{
  Empty = 0,
  Q1 = 0b0001,
  Q2 = 0b0010,
  Q3 = 0b0100,
  Q4 = 0b1000,
}

public enum ShadeBlock : byte
{
  FullBlock, Shade3, Shade2, Shade1
}

public static class BlockCharExtensions
{
  public static char ToChar(this QBlock blockChar)
  {
    return blockChar switch
    {
      QBlock.Empty => ' ',
      QBlock.Q1 => '▝',
      QBlock.Q2 => '▘',
      QBlock.Q3 => '▖',
      QBlock.Q4 => '▗',
      QBlock.Q1 | QBlock.Q2 => '▀',
      QBlock.Q1 | QBlock.Q3 => '▞',
      QBlock.Q1 | QBlock.Q4 => '▐',
      QBlock.Q2 | QBlock.Q3 => '▌',
      QBlock.Q2 | QBlock.Q4 => '▚',
      QBlock.Q3 | QBlock.Q4 => '▄',
      QBlock.Q1 | QBlock.Q2 | QBlock.Q3 => '▛',
      QBlock.Q1 | QBlock.Q2 | QBlock.Q4 => '▜',
      QBlock.Q1 | QBlock.Q3 | QBlock.Q4 => '▟',
      QBlock.Q2 | QBlock.Q3 | QBlock.Q4 => '▙',
      QBlock.Q1 | QBlock.Q2 | QBlock.Q3 | QBlock.Q4 => '█',
      _ => ' '
    };
  }

  public static char ToChar(this ShadeBlock blockChar)
  {
    return blockChar switch
    {
      ShadeBlock.FullBlock => '█',
      ShadeBlock.Shade3 => '▓',
      ShadeBlock.Shade2 => '▒',
      ShadeBlock.Shade1 => '░',
      _ => ' '
    };
  }
}
