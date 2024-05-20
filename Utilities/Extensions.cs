using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LogParserApp.Utilities;
public static class Extensions
{
    public static string FormatExceptionMessageForDb(this string str)
    {
        return  str.RemoveNullChars().Replace("'", "''").Replace(Environment.NewLine, "|");
    }

    public static string RemoveNullChars(this string str)
    {
        return str.Replace("\0", "");
    }
    public static string DoubleToSingleLinefeed(this string str)
    {
        return str.Replace(Environment.NewLine+Environment.NewLine, Environment.NewLine);
    }
}
