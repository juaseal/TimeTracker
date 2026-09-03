using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Media;
namespace TimeTracker;
public sealed class ActivitySuggestion
{
    public long Id { get; set; }
    public string Project { get; set; } = "";
    public string Epic { get; set; } = "";
    public string Activity { get; set; } = "";
    public string Comment { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime? End { get; set; }
    public string Display => $"{Epic} · {Activity}";
    public string TimeText => $"{Start:dd/MM/yyyy HH:mm} – {(End is null ? "en curso" : End.Value.ToString("HH:mm"))}";
    public string CommentText => string.IsNullOrWhiteSpace(Comment) ? "Sin comentario" : Comment;
    public List<RecentDisplayField> DisplayFields { get; set; } = new();
    public bool IsFavorite { get; set; }
    public string FavoriteGlyph => IsFavorite ? "★" : "☆";
    public System.Windows.Thickness SeparatorThickness { get; set; }
}
public sealed class RecentDisplayField
{
    public string Text { get; set; } = "";
    public System.Windows.FontWeight FontWeight { get; set; } = System.Windows.FontWeights.Normal;
}
public sealed class RecentFieldSetting : INotifyPropertyChanged
{
    bool _isVisible; bool _isBold;
    public string FieldKey { get; set; } = "";
    public string Label { get; set; } = "";
    public int Order { get; set; }
    public bool IsVisible { get=>_isVisible; set{_isVisible=value;Changed();} }
    public bool IsBold { get=>_isBold; set{_isBold=value;Changed();} }
    public event PropertyChangedEventHandler? PropertyChanged;
    void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
public sealed class WeekStartOption { public int Value { get; set; } public string Name { get; set; } = ""; }
public sealed class WeekSummaryRow
{
    public string Project { get; set; } = "";
    public string Epic { get; set; } = "";
    public string ProjectEpic => $"{Project} - {Epic}";
    public string[] DayHours { get; set; } = new string[7];
}
public sealed class SessionRow
{
    public long Id { get; set; }
    public DateTime Start { get; set; }
    public DateTime? End { get; set; }
    public string Project { get; set; } = "";
    public string Epic { get; set; } = "";
    public string Activity { get; set; } = "";
    public string Comment { get; set; } = "";
    public string StartText { get => Start.ToString("HH:mm"); set { if (TryParseTime(value, out var t)) Start = Start.Date + t; } }
    public string EndText { get => End?.ToString("HH:mm") ?? "…"; set { if (string.IsNullOrWhiteSpace(value) || value == "…") End = null; else if (TryParseTime(value, out var t)) End = Start.Date + t; } }
    public string Duration => Format((End ?? DateTime.Now) - Start);
    public static string Format(TimeSpan value) => $"{(int)value.TotalHours:00}:{value.Minutes:00}";
    static bool TryParseTime(string? value, out TimeSpan time)
    {
        var text=(value??"").Trim().Replace('.',':');
        if (text.Length is 3 or 4 && text.All(char.IsDigit)) text=text.Insert(text.Length-2,":");
        return TimeSpan.TryParse(text,out time) && time>=TimeSpan.Zero && time<TimeSpan.FromDays(1);
    }
}
public sealed class SummaryRow : INotifyPropertyChanged
{
    double _added; string _dailyTotalText=""; string _dailyExpectedText=""; string _dailyStatusText=""; Brush _dailyStatusBrush=Brushes.Gray;
    public DateTime Day { get; set; }
    public string Project { get; set; } = "";
    public string Epic { get; set; } = "";
    public TimeSpan Registered { get; set; }
    public double Added { get=>_added; set{_added=value;Changed();Changed(nameof(AddedText));Changed(nameof(TotalText));} }
    public string DayText=>Day.ToString("ddd dd/MM");
    public string RegisteredText=>SessionRow.Format(Registered);
    public string AddedText { get=>Added.ToString("0.##",CultureInfo.InvariantCulture); set{if(double.TryParse((value??"").Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var hours))Added=hours;} }
    public string TotalText=>SessionRow.Format(Registered+TimeSpan.FromHours(Added));
    public string DailyTotalText { get=>_dailyTotalText; set{_dailyTotalText=value;Changed();} }
    public string DailyExpectedText { get=>_dailyExpectedText; set{_dailyExpectedText=value;Changed();} }
    public string DailyStatusText { get=>_dailyStatusText; set{_dailyStatusText=value;Changed();} }
    public Brush DailyStatusBrush { get=>_dailyStatusBrush; set{_dailyStatusBrush=value;Changed();} }
    public event PropertyChangedEventHandler? PropertyChanged;
    void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
public sealed class ScheduleTemplate { public long Id { get; set; } public string Name { get; set; } = ""; public override string ToString()=>Name; }
public sealed class ScheduleDay { public int DayOfWeek { get; set; } public string Day { get; set; } = ""; public double Hours { get; set; } }
public sealed class CalendarPeriodRow { public long Id { get; set; } public string Name { get; set; } = ""; public string StartText { get; set; } = "01/01"; public string EndText { get; set; } = "31/12"; public long TemplateId { get; set; } public string TemplateName { get; set; } = ""; }
public sealed class WeekDayTotal { public DateTime Day { get; set; } public TimeSpan Total { get; set; } public double ExpectedHours { get; set; } public string DayText=>Day.ToString("ddd dd/MM"); public string TotalText=>SessionRow.Format(Total); public string ExpectedText=>SessionRow.Format(TimeSpan.FromHours(ExpectedHours)); public double DifferenceHours=>Total.TotalHours-ExpectedHours; public string StatusText=>ExpectedHours<=0?"Libre":DifferenceHours switch { > 0.01=>$"+{SessionRow.Format(TimeSpan.FromHours(DifferenceHours))}", < -0.01=>$"-{SessionRow.Format(TimeSpan.FromHours(-DifferenceHours))}", _=>"OK"}; public Brush StatusBrush=>ExpectedHours<=0?Brushes.Gray:DifferenceHours>0.01?Brushes.Firebrick:DifferenceHours< -0.01?Brushes.DarkOrange:Brushes.ForestGreen; }
public sealed class ReportSlice { public string Label { get; set; } = ""; public double Hours { get; set; } public string HoursText=>SessionRow.Format(TimeSpan.FromHours(Hours)); public double Percentage { get; set; } public string PercentageText=>$"{Percentage:0.#}%"; }
