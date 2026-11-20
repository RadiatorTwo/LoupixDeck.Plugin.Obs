namespace LoupixDeck.Plugin.Obs;

/// <summary>
/// The curve of OBS's audio mixer fader (libobs <c>OBS_FADER_LOG</c>), so one dial tick moves the
/// volume by the same amount the mixer slider would: fine steps near 0 dB, coarse ones near silence.
/// Fader position 0 is silence (-inf dB), 1 is 0 dB.
/// </summary>
internal static class ObsFader
{
    private const double LogOffsetDb = 6.0;
    private const double LogRangeDb = 96.0;
    private const double LogOffsetVal = -0.77815125038364363; // -log10(LogOffsetDb)
    private const double LogRangeVal = -2.00860017176191756;  // -log10(LogRangeDb + LogOffsetDb)

    /// <summary>dB → fader position (0..1).</summary>
    public static double DbToPosition(float db)
    {
        if (db >= 0f) return 1d;
        if (db <= -LogRangeDb || float.IsNegativeInfinity(db)) return 0d;
        return (-Math.Log10(-db + LogOffsetDb) - LogRangeVal) / (LogOffsetVal - LogRangeVal);
    }

    /// <summary>Fader position (0..1) → dB; 0 gives negative infinity (silence).</summary>
    public static float PositionToDb(double position)
    {
        if (position >= 1d) return 0f;
        if (position <= 0d) return float.NegativeInfinity;
        return (float)(-(LogRangeDb + LogOffsetDb)
                       * Math.Pow((LogRangeDb + LogOffsetDb) / LogOffsetDb, -position)
                       + LogOffsetDb);
    }

    /// <summary>Linear amplitude (OBS "mul") → dB; 0 or less gives negative infinity.</summary>
    public static float MulToDb(double mul) =>
        mul <= 0d ? float.NegativeInfinity : (float)(20d * Math.Log10(mul));
}
