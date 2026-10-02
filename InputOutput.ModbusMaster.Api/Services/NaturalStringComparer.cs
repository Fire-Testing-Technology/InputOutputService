namespace InputOutput.ModbusMaster.Api.Services;

/// <summary>
/// Case-insensitive ordering where runs of digits compare by numeric value, so <c>COM2</c> sorts before
/// <c>COM10</c> and <c>/dev/ttyUSB2</c> before <c>/dev/ttyUSB10</c>. Culture-independent (no ICU/NLS needed).
/// </summary>
public sealed class NaturalStringComparer : IComparer<string>
{
    public static NaturalStringComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                var startX = i;
                var startY = j;
                while (i < x.Length && char.IsAsciiDigit(x[i]))
                {
                    i++;
                }

                while (j < y.Length && char.IsAsciiDigit(y[j]))
                {
                    j++;
                }

                // Compare by value without parsing (no overflow): ignore leading zeros, then longer run is larger.
                var runX = x.AsSpan(startX, i - startX).TrimStart('0');
                var runY = y.AsSpan(startY, j - startY).TrimStart('0');
                if (runX.Length != runY.Length)
                {
                    return runX.Length.CompareTo(runY.Length);
                }

                var byDigits = runX.CompareTo(runY, StringComparison.Ordinal);
                if (byDigits != 0)
                {
                    return byDigits;
                }
            }
            else
            {
                var byChar = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (byChar != 0)
                {
                    return byChar;
                }

                i++;
                j++;
            }
        }

        // One string is a prefix of the other (shorter first); otherwise tie-break for a stable total order.
        var remaining = (x.Length - i).CompareTo(y.Length - j);
        return remaining != 0 ? remaining : string.CompareOrdinal(x, y);
    }
}
