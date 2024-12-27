#region

using System;
using System.Collections.Generic;

#endregion

namespace IZ.Core.Api.Fragments;

[Flags]
public enum FragmentFormat {
  None = 0,
  Xxs = 1 << 0x01,
  Xs = 1 << 0x02,
  Sm = 1 << 0x03,
  Md = 1 << 0x04,
  Lg = 1 << 0x05,
  Xl = 1 << 0x06,
  Xxl = 1 << 0x07,
  Full = 1 << 0x1F
}

public static class FragmentFormats {
  public static List<FragmentFormat> Sizes => new List<FragmentFormat> {
    FragmentFormat.Xxs,
    FragmentFormat.Xs,
    FragmentFormat.Sm,
    FragmentFormat.Md,
    FragmentFormat.Lg,
    FragmentFormat.Lg,
    FragmentFormat.Xl,
    FragmentFormat.Xxl
  };

  public static FragmentFormat ExpandSizes(this FragmentFormat fmt) {
    List<FragmentFormat> sizes = Sizes;
    int idx = sizes.FindIndex(s => fmt.HasFlag(s));
    if (idx < 0) return fmt;
    FragmentFormat ret = 0;
    for (int i = idx; i < sizes.Count; i++) {
      ret |= sizes[i];
    }
    return ret;
  }
}
