using System;
using System.Collections.Generic;

namespace ConstiBoost.Localization;

/// <summary>All German dictionaries (one file per app area).</summary>
internal static class Dictionaries
{
    public static readonly IReadOnlyList<Action<Action<string, string>>> All = new List<Action<Action<string, string>>>
    {
        DeCommon.Register,
        DeViews1.Register,
        DeViews2.Register,
        DeGamesVm.Register,
        DeViewModels.Register,
        DeOptimizations.Register,
        DeServices.Register,
    };
}
