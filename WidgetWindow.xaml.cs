using System.Globalization;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
namespace TimeTracker;
public partial class WidgetWindow : Window
{
    readonly TimeRepository _repo; readonly MainWindow _main; readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromSeconds(1)}; bool _returning; bool _refreshing; SessionRow? _active;
    public WidgetWindow(TimeRepository repo,MainWindow main)
    {
        _repo=repo;_main=main;InitializeComponent();Topmost=_repo.BoolSetting("always_on_top",true);RestorePosition();_timer.Tick+=(_,_)=>RefreshClock();_timer.Start();RefreshWidget();
    }
    void RestorePosition()
    {
        if(!double.TryParse(_repo.Setting("widget_left"),NumberStyles.Float,CultureInfo.InvariantCulture,out var left)||!double.TryParse(_repo.Setting("widget_top"),NumberStyles.Float,CultureInfo.InvariantCulture,out var top))return;
        if(left>=SystemParameters.VirtualScreenLeft-Width+80&&left<=SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-80&&top>=SystemParameters.VirtualScreenTop&&top<=SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-80){WindowStartupLocation=WindowStartupLocation.Manual;Left=left;Top=top;}
    }
    void RefreshWidget()
    {
        _active=_repo.Active();RefreshClock();_refreshing=true;
        try
        {
            WidgetRecentList.ItemsSource=_repo.RecentFeed();WidgetRecentList.SelectedItem=null;
        }
        finally{_refreshing=false;}
    }    void RefreshClock()
    {
        _active=_repo.Active();if(_active is null){WidgetStateText.Text="NO ACTIVE TASK";WidgetStateText.Foreground=(System.Windows.Media.Brush)FindResource("TextSecondary");WidgetTitle.Text="No active task";WidgetDetail.Text="Select a recent task";WidgetClock.Text="00:00:00";return;}
        WidgetStateText.Text="TRACKING";WidgetStateText.Foreground=(System.Windows.Media.Brush)FindResource("Success");WidgetTitle.Text=$"{_active.Project} · {_active.Epic}";WidgetDetail.Text=_active.Activity+(string.IsNullOrWhiteSpace(_active.Comment)?"":$" — {_active.Comment}");var elapsed=DateTime.Now-_active.Start;WidgetClock.Text=$"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }
    void RecentSelected(object s,System.Windows.Controls.SelectionChangedEventArgs e){if(_refreshing)return;}
    ActivitySuggestion? SelectedTask()=>WidgetRecentList.SelectedItem as ActivitySuggestion;
    void StartClick(object s,RoutedEventArgs e){if(SelectedTask() is not { } item)return;_repo.Start(item.Project,item.Epic,item.Activity,item.Comment);RefreshWidget();}
    void FavoriteClick(object s,RoutedEventArgs e){if(s is not System.Windows.Controls.Button { Tag: ActivitySuggestion item })return;_repo.SetFavorite(item,!item.IsFavorite);RefreshWidget();e.Handled=true;}    void StopClick(object s,RoutedEventArgs e){_repo.Stop();RefreshWidget();}
    void HeaderMouseDown(object s,MouseButtonEventArgs e){if(e.LeftButton==MouseButtonState.Pressed)DragMove();}
    void OpenFullClick(object s,RoutedEventArgs e)=>ReturnToMain();
    void WidgetKeyDown(object s,KeyEventArgs e){if(e.Key==Key.Escape){e.Handled=true;ReturnToMain();}}
    void ReturnToMain(){_returning=true;_main.Show();_main.RefreshFromWidget();_main.Activate();Close();}
    void WidgetClosing(object? s,CancelEventArgs e)
    {
        _timer.Stop();_repo.SaveSettings(new Dictionary<string,string>{{"widget_left",Left.ToString(CultureInfo.InvariantCulture)},{"widget_top",Top.ToString(CultureInfo.InvariantCulture)}});if(!_returning){_main.Show();_main.RefreshFromWidget();_main.Activate();}
    }
}
