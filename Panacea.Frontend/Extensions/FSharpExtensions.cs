using System;
using Microsoft.FSharp.Core;

namespace Panacea.Frontend.Extensions;

public static class FSharpExtensions
{
    public static T? AsNullable<T>(this FSharpOption<T> opt) where T : class
    {
        return opt.IsSome() ? opt.Value : null;
    }
    
    public static bool IsSome<T>(this FSharpOption<T> opt)
    {
        return FSharpOption<T>.get_IsSome(opt);
    }
    
    public static bool IsNone<T>(this FSharpOption<T> opt)
    {
        return FSharpOption<T>.get_IsNone(opt);
    }
}