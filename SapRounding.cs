namespace TimeTracker;

public static class SapRounding
{
    public static double Apply(double hours,bool enabled,int resolutionMinutes)
    {
        if(!enabled)return hours;
        if(resolutionMinutes is <5 or >30||resolutionMinutes%5!=0)throw new ArgumentOutOfRangeException(nameof(resolutionMinutes));
        var units=hours*60/resolutionMinutes;
        return Math.Round(units,MidpointRounding.AwayFromZero)*resolutionMinutes/60d;
    }
}