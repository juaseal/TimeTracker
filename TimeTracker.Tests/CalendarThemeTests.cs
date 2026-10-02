using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TimeTracker;

static class CalendarThemeTests
{
    public static void Verify()
    {
        Exception? failure=null;
        var thread=new Thread(()=>{
            try
            {
                var app=new App();app.InitializeComponent();
                var calendar=new Calendar { DisplayDate=new DateTime(2026,9,23),SelectedDate=new DateTime(2026,9,23) };
                var picker=new DatePicker();
                var host=new Window { Content=new StackPanel { Children={picker,calendar} } };
                foreach(var theme in new[]{"dark","light","dark"})
                {
                    AppearanceManager.Apply(new AppearancePreferences { Theme=theme });
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    picker.Style=(Style)app.FindResource(typeof(DatePicker));
                    if(picker.CalendarStyle!=app.FindResource("ThemedCalendar"))throw new Exception("DatePicker is not using the themed calendar.");
                    calendar.Style=picker.CalendarStyle;
                    foreach(var mode in new[]{CalendarMode.Month,CalendarMode.Year,CalendarMode.Decade})
                    {
                        calendar.DisplayMode=mode;
                        calendar.Measure(new Size(320,320));calendar.Arrange(new Rect(0,0,320,320));calendar.UpdateLayout();
                        var item=(CalendarItem)calendar.Template.FindName("PART_CalendarItem",calendar);
                        if(item is null)throw new Exception("Missing CalendarItem.");
                        var activeGrid=(Grid)item.Template.FindName(mode==CalendarMode.Month?"PART_MonthView":"PART_YearView",item);
                        if(activeGrid.Visibility!=Visibility.Visible)throw new Exception($"Calendar view {mode} is hidden.");
                        var expectedBackground=((SolidColorBrush)app.FindResource("Surface")).Color;
                        var expectedForeground=((SolidColorBrush)app.FindResource("TextPrimary")).Color;
                        if(((SolidColorBrush)item.Background).Color!=expectedBackground||((SolidColorBrush)item.Foreground).Color!=expectedForeground)throw new Exception($"Calendar theme mismatch {theme}/{mode}: {item.Background} / {item.Foreground}, expected {expectedBackground} / {expectedForeground}.");
                        var buttons=Descendants(item).OfType<Control>().Where(x=>mode==CalendarMode.Month?x is CalendarDayButton:x is CalendarButton).ToList();
                        if(buttons.Count==0)throw new Exception("No calendar buttons generated.");
                        foreach(var button in buttons)
                        {
                            if(button.Foreground is not SolidColorBrush fg||button.Background is not SolidColorBrush bg)throw new Exception("Missing button colors.");
                            if(Contrast(fg.Color,bg.Color)<4.5)throw new Exception($"Insufficient contrast in {theme}/{mode}: {button.ContentText()}");
                        }
                        var output=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../artifacts/calendar-preview"));
                        Directory.CreateDirectory(output);
                        var bitmap=new RenderTargetBitmap(320,320,96,96,PixelFormats.Pbgra32);bitmap.Render(calendar);
                        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var file=File.Create(Path.Combine(output,$"{theme}-{mode}.png"));encoder.Save(file);
                    }
                }
                host.Close();app.Shutdown();
            }
            catch(Exception ex){failure=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(failure is not null)throw new Exception("Calendar verification failed: "+failure,failure);
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i);yield return child;
            foreach(var descendant in Descendants(child))yield return descendant;
        }
    }
    static string ContentText(this Control control)=>(control as ContentControl)?.Content?.ToString()??"";
    static double Contrast(Color a,Color b)
    {
        static double L(Color c)
        {
            static double Linear(byte value){var v=value/255d;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
            return .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);
        }
        var x=L(a);var y=L(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);
    }
}

