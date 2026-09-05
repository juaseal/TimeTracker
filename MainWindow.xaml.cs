using System.Collections.ObjectModel;
using System.Text;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
namespace TimeTracker;
public partial class MainWindow : Window
{
    readonly TimeRepository _repo; readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromSeconds(1)}; readonly Stack<Action> _undo=new(); SessionRow? _active; SessionRow? _editBefore; bool _undoing; string? _reportProject; string? _reportEpic; string? _reportActivity; int _lastMainTabIndex; bool _openingSettings; int _weekStartDay=1; bool _sapRoundingEnabled; int _sapRoundingMinutes=30; List<RecentFieldSetting> _recentFieldSettings=new(); WidgetWindow? _widget;
    static readonly Brush[] ReportColors={new SolidColorBrush(Color.FromRgb(109,93,251)),new SolidColorBrush(Color.FromRgb(39,131,106)),new SolidColorBrush(Color.FromRgb(235,150,48)),new SolidColorBrush(Color.FromRgb(211,78,94)),new SolidColorBrush(Color.FromRgb(55,136,216)),new SolidColorBrush(Color.FromRgb(151,91,178)),new SolidColorBrush(Color.FromRgb(76,164,84)),new SolidColorBrush(Color.FromRgb(210,112,45))};
    public MainWindow():this(new TimeRepository()){}
    internal MainWindow(TimeRepository repo){_repo=repo;var appearance=AppearanceManager.Load(_repo);AppearanceManager.Apply(appearance);InitializeComponent();ProjectBox.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler(NowFieldTextChanged));EpicBox.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler(NowFieldTextChanged));ActivityBox.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler(NowFieldTextChanged));Topmost=_repo.BoolSetting("always_on_top",true);_weekStartDay=_repo.WeekStartDay();LoadRoundingPolicy();_recentFieldSettings=_repo.RecentFieldSettings();DayPicker.SelectedDate=DateTime.Today;WeekPicker.SelectedDate=StartOfWeek(DateTime.Today);ReportFromPicker.SelectedDate=DateTime.Today.AddMonths(-1);ReportToPicker.SelectedDate=DateTime.Today;_timer.Tick+=(_,_)=>RefreshClock();_timer.Start();RefreshAll();RefreshReport();}
    List<ActivitySuggestion> CombinedRecent(string? project=null)=>_repo.RecentFeed(project);
    void RefreshRecent(){_recentFieldSettings=_repo.RecentFieldSettings();var recent=CombinedRecent();ProjectBox.ItemsSource=recent.Select(x=>x.Project).Distinct().ToList();RecentList.ItemsSource=recent;}    void RefreshAll(){_active=_repo.Active();RefreshClock();RefreshRecent();LoadDay();LoadWeek();}
    void RefreshClock()
    {
        _active=_repo.Active();
        if(_active is null)
        {
            ActiveTitle.Text="No active task";ActiveDetail.Text="Choose a project, epic, and activity";Clock.Text="00:00:00";
            ActiveStateBadge.Visibility=Visibility.Collapsed;HeaderStatusText.Text="No active task";HeaderStatusDot.Fill=Brushes.Gray;return;
        }
        ActiveTitle.Text=$"{_active.Project} · {_active.Epic}";ActiveDetail.Text=_active.Activity+(string.IsNullOrWhiteSpace(_active.Comment)?"":$"  —  {_active.Comment}");
        Clock.Text=((DateTime.Now-_active.Start) is var d)?$"{(int)d.TotalHours:00}:{d.Minutes:00}:{d.Seconds:00}":"";
        ActiveStateBadge.Visibility=Visibility.Visible;HeaderStatusText.Text="Tracking";HeaderStatusDot.Fill=(Brush)FindResource("Success");
    }
    void OpenWidgetClick(object s,RoutedEventArgs e)=>OpenWidget();
    void OpenWidget(){if(_widget?.IsVisible==true){_widget.Activate();return;}_widget=new WidgetWindow(_repo,this);_widget.Closed+=(_,_)=>_widget=null;_widget.Show();Hide();}
    internal void ShowWidgetAtStartup()=>OpenWidget();
    public void RefreshFromWidget(){RefreshAll();RefreshReport();}
    void StartClick(object s,RoutedEventArgs e){var p=ProjectBox.Text.Trim();var ep=EpicBox.Text.Trim();var a=ActivityBox.Text.Trim();if(ActivityBox.SelectedItem is ActivitySuggestion x){p=x.Project;ep=x.Epic;a=x.Activity;}if(string.IsNullOrWhiteSpace(p)||string.IsNullOrWhiteSpace(ep)||string.IsNullOrWhiteSpace(a)){MessageBox.Show("Enter a project, epic, and activity.","TaskUp");return;}_repo.Start(p,ep,a,CommentBox.Text.Trim());RefreshAll();}
    void StopClick(object s,RoutedEventArgs e){_repo.Stop();RefreshAll();}
    void ProjectChanged(object s,SelectionChangedEventArgs e)
    {
        var p=ProjectBox.SelectedItem?.ToString()??ProjectBox.Text;var recent=CombinedRecent(p);
        RecentList.ItemsSource=recent;ActivityBox.ItemsSource=recent;EpicBox.ItemsSource=recent.Select(x=>x.Epic).Distinct().ToList();
        if(recent.FirstOrDefault() is { } last){EpicBox.Text=last.Epic;ActivityBox.SelectedItem=last;ActivityBox.Text=last.Activity;}
    }
    void NowFieldTextChanged(object s,TextChangedEventArgs e)
    {
        CommentBox.Clear();
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>
        {
            var project=ProjectBox.Text.Trim();var epic=EpicBox.Text.Trim();var activity=ActivityBox.Text.Trim();
            if(ActivityBox.SelectedItem is ActivitySuggestion selected&&string.Equals(ActivityBox.Text,selected.Display,StringComparison.OrdinalIgnoreCase))activity=selected.Activity;
            if(string.IsNullOrWhiteSpace(project)||string.IsNullOrWhiteSpace(epic)||string.IsNullOrWhiteSpace(activity))return;
            CommentBox.Text=_repo.LastComment(project,epic,activity)??"";
        }));
    }
    void RecentSelected(object s,SelectionChangedEventArgs e)
    {
        if(s is not ListBox list||!list.IsKeyboardFocusWithin||list.SelectedItem is not ActivitySuggestion x)return;
        ProjectBox.Text=x.Project;EpicBox.Text=x.Epic;ActivityBox.SelectedItem=x;ActivityBox.Text=x.Activity;CommentBox.Text=x.Comment;
        _active=_repo.Active();if(_active is not null&&string.Equals(_active.Project,x.Project,StringComparison.OrdinalIgnoreCase)&&string.Equals(_active.Epic,x.Epic,StringComparison.OrdinalIgnoreCase)&&string.Equals(_active.Activity,x.Activity,StringComparison.OrdinalIgnoreCase))return;
        StartClick(s,new RoutedEventArgs());
    }
    void FavoriteClick(object s,RoutedEventArgs e)
    {
        if(s is not Button { Tag: ActivitySuggestion item })return;
        _repo.SetFavorite(item,!item.IsFavorite);RefreshRecent();e.Handled=true;
    }
    void LoadDay()
    {
        var day=(DayPicker.SelectedDate??DateTime.Today).Date;var rows=_repo.Day(day);var expected=_repo.ExpectedHours(day);
        var actual=rows.Sum(x=>Math.Max(0,((x.End??DateTime.Now)-x.Start).TotalHours));var difference=expected-actual;
        if(expected>0&&difference>1d/3600)
        {
            var start=rows.Count==0?day.AddHours(9):(rows.Max(x=>x.End??DateTime.Now));
            rows.Add(new SessionRow{Id=-1,Start=start,End=start.AddHours(difference),Activity="Unspecified",Comment="Unallocated time"});
            DayStatusText.Text=$"Workday {FormatHours(expected)} · Remaining {FormatHours(difference)}";DayStatusText.Foreground=System.Windows.Media.Brushes.DarkOrange;
        }
        else if(expected>0&&difference< -1d/3600){DayStatusText.Text=$"Workday {FormatHours(expected)} · Overtime {FormatHours(-difference)}";DayStatusText.Foreground=System.Windows.Media.Brushes.Red;}
        else if(expected>0){DayStatusText.Text=$"Workday complete · {FormatHours(expected)}";DayStatusText.Foreground=System.Windows.Media.Brushes.ForestGreen;}
        else{DayStatusText.Text="No work schedule configured for this day";DayStatusText.Foreground=System.Windows.Media.Brushes.Gray;}
        DayGrid.ItemsSource=new ObservableCollection<SessionRow>(rows);
    }
    void RefreshDayStatus(SessionRow? editedRow=null,DateTime? editedStart=null,DateTime? editedEnd=null)
    {
        var day=(DayPicker.SelectedDate??DateTime.Today).Date;var expected=_repo.ExpectedHours(day);
        var rows=(DayGrid.ItemsSource as IEnumerable<SessionRow>)??Enumerable.Empty<SessionRow>();
        var actual=rows.Where(x=>x.Id!=-1).Sum(x=>
        {
            var start=ReferenceEquals(x,editedRow)?editedStart??x.Start:x.Start;
            var end=ReferenceEquals(x,editedRow)?editedEnd:x.End;
            return Math.Max(0,((end??DateTime.Now)-start).TotalHours);
        });
        SetDayStatus(expected,expected-actual);
    }
    void SetDayStatus(double expected,double difference)
    {
        var separator=$" {(char)0x00B7} ";
        if(expected>0&&difference>1d/3600){DayStatusText.Text=$"Workday {FormatHours(expected)}{separator}Remaining {FormatHours(difference)}";DayStatusText.Foreground=Brushes.DarkOrange;}
        else if(expected>0&&difference< -1d/3600){DayStatusText.Text=$"Workday {FormatHours(expected)}{separator}Overtime {FormatHours(-difference)}";DayStatusText.Foreground=Brushes.Red;}
        else if(expected>0){DayStatusText.Text=$"Workday complete{separator}{FormatHours(expected)}";DayStatusText.Foreground=Brushes.ForestGreen;}
        else{DayStatusText.Text=$"D{(char)0x00ED}a sin jornada configurada";DayStatusText.Foreground=Brushes.Gray;}
    }
    static string FormatHours(double hours)=>SessionRow.Format(TimeSpan.FromHours(hours));
    void LoadRoundingPolicy(){_sapRoundingEnabled=_repo.BoolSetting("sap_rounding_enabled");_sapRoundingMinutes=int.TryParse(_repo.Setting("sap_rounding_minutes","30"),out var minutes)&&minutes is >=5 and <=30&&minutes%5==0?minutes:30;}
    double RoundForSap(double hours)=>SapRounding.Apply(hours,_sapRoundingEnabled,_sapRoundingMinutes);
    string SapPolicyText()=>_sapRoundingEnabled?$"SAP · redondeo a {_sapRoundingMinutes} min":"SAP · sin redondeo";
    DateTime StartOfWeek(DateTime date){var day=date.DayOfWeek==DayOfWeek.Sunday?7:(int)date.DayOfWeek;return date.Date.AddDays(-((7+day-_weekStartDay)%7));}
    void PreviousDayClick(object s,RoutedEventArgs e)=>DayPicker.SelectedDate=(DayPicker.SelectedDate??DateTime.Today).Date.AddDays(-1);
    void TodayClick(object s,RoutedEventArgs e)=>DayPicker.SelectedDate=DateTime.Today;
    void NextDayClick(object s,RoutedEventArgs e)=>DayPicker.SelectedDate=(DayPicker.SelectedDate??DateTime.Today).Date.AddDays(1);
    void LoadWeek()
    {
        var start=StartOfWeek(WeekPicker.SelectedDate??DateTime.Today);WeekNumberText.Text=$"Period · {start:dd/MM}–{start.AddDays(6):dd/MM}";SapPolicyTextBlock.Text=SapPolicyText();var rows=_repo.Week(start);ApplyDailySummaries(rows);var view=CollectionViewSource.GetDefaultView(rows);view.GroupDescriptions.Clear();view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SummaryRow.DayText)));WeekGrid.ItemsSource=view;LoadWeekSummary(rows,start);
    }
    void LoadWeekSummary(IReadOnlyList<SummaryRow> source,DateTime start)
    {
        while(WeekSummaryGrid.Columns.Count>2)WeekSummaryGrid.Columns.RemoveAt(1);
        for(var i=0;i<7;i++){var day=start.AddDays(i);var name=CultureInfo.GetCultureInfo("en-US").TextInfo.ToTitleCase(day.ToString("dddd",CultureInfo.GetCultureInfo("en-US")));WeekSummaryGrid.Columns.Insert(1+i,new DataGridTextColumn{Header=$"{name}\n{day:dd/MM}",Binding=new Binding($"DayHours[{i}]"){Mode=BindingMode.OneWay},Width=new DataGridLength(88),IsReadOnly=true});}
        WeekSummaryGrid.ItemsSource=source.GroupBy(x=>new{x.Project,x.Epic}).OrderBy(x=>x.Key.Project).ThenBy(x=>x.Key.Epic).Select(group=>new WeekSummaryRow{Project=group.Key.Project,Epic=group.Key.Epic,DayHours=Enumerable.Range(0,7).Select(offset=>{var hours=RoundForSap(group.Where(x=>x.Day==start.AddDays(offset)).Sum(x=>x.Registered.TotalHours+x.Added));return Math.Abs(hours)<0.0000001?"":hours.ToString("0.##",CultureInfo.CurrentCulture);}).ToArray()}).ToList();
    }
    void ApplyDailySummaries(IEnumerable<SummaryRow> source)
    {
        var rows=source.ToList();foreach(var group in rows.GroupBy(x=>x.Day)){var total=group.Sum(x=>x.Registered.TotalHours+x.Added);var expected=_repo.ExpectedHours(group.Key);var difference=total-expected;var totalText=SessionRow.Format(TimeSpan.FromHours(total));var expectedText=SessionRow.Format(TimeSpan.FromHours(expected));var status=expected<=0?"Libre":difference>0.01?$"+{SessionRow.Format(TimeSpan.FromHours(difference))}":difference< -0.01?$"-{SessionRow.Format(TimeSpan.FromHours(-difference))}":"OK";var brush=expected<=0?Brushes.Gray:difference>0.01?Brushes.Firebrick:difference< -0.01?Brushes.DarkOrange:Brushes.ForestGreen;foreach(var row in group){row.DailyTotalText=totalText;row.DailyExpectedText=expectedText;row.DailyStatusText=status;row.DailyStatusBrush=brush;}}
    }
    void WeekCellEditEnding(object s,DataGridCellEditEndingEventArgs e){if(e.Row.Item is SummaryRow row)Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>{_repo.SaveAdjustment(row);LoadWeek();}));}
    void PreviousWeekClick(object s,RoutedEventArgs e)=>WeekPicker.SelectedDate=StartOfWeek(WeekPicker.SelectedDate??DateTime.Today).AddDays(-7);
    void CurrentWeekClick(object s,RoutedEventArgs e)=>WeekPicker.SelectedDate=StartOfWeek(DateTime.Today);
    void NextWeekClick(object s,RoutedEventArgs e)=>WeekPicker.SelectedDate=StartOfWeek(WeekPicker.SelectedDate??DateTime.Today).AddDays(7);
    void WeekRowDoubleClick(object s,MouseButtonEventArgs e)
    {
        if(WeekGrid.SelectedItem is not SummaryRow row)return;ShowTasksWindow(row.Day,row.Project,row.Epic);
    }
    void WeekSummaryCellDoubleClick(object s,MouseButtonEventArgs e)
    {
        if(WeekSummaryGrid.CurrentCell.Item is not WeekSummaryRow row||WeekSummaryGrid.CurrentCell.Column is null)return;
        var dayOffset=WeekSummaryGrid.CurrentCell.Column.DisplayIndex-1;if(dayOffset is <0 or >6||string.IsNullOrWhiteSpace(row.DayHours[dayOffset]))return;
        ShowTasksWindow(StartOfWeek(WeekPicker.SelectedDate??DateTime.Today).AddDays(dayOffset),row.Project,row.Epic);
    }
    void ShowTasksWindow(DateTime day,string project,string epic)
    {
        var summary=new SummaryRow{Day=day,Project=project,Epic=epic};var text=string.Join(Environment.NewLine,_repo.TasksForSummary(summary));
        var editor=new TextBox{Text=text,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(12),FontSize=14};
        var window=new Window{Title=$"{day:dd/MM/yyyy} · {project} · {epic}",Owner=this,Width=620,Height=420,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var copy=new Button{Content="Copy",HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(8),MinWidth=100};copy.Click+=(_,_)=>{try{Clipboard.SetText(editor.Text);window.Close();}catch(Exception ex){MessageBox.Show(ex.Message,"Could not copy");}};
        var panel=new DockPanel();DockPanel.SetDock(copy,Dock.Bottom);panel.Children.Add(copy);panel.Children.Add(editor);window.Content=panel;
        window.ContentRendered+=(_,_)=>{editor.Focus();editor.SelectAll();};
        window.PreviewKeyDown+=(_,key)=>
        {
            if(key.Key==Key.C&&(Keyboard.Modifiers&ModifierKeys.Control)!=0)
            {
                key.Handled=true;try{Clipboard.SetText(editor.Text);window.Close();}catch(Exception ex){MessageBox.Show(ex.Message,"Could not copy");}return;
            }
            if(key.Key==Key.Escape){key.Handled=true;window.Close();}
        };
        window.ShowDialog();
    }
    void DayChanged(object s,SelectionChangedEventArgs e)=>LoadDay(); void WeekChanged(object s,SelectionChangedEventArgs e){var start=StartOfWeek(WeekPicker.SelectedDate??DateTime.Today);if(WeekPicker.SelectedDate?.Date!=start){WeekPicker.SelectedDate=start;return;}LoadWeek();}
    void BeginningEdit(object s,DataGridBeginningEditEventArgs e){if(e.Row.Item is SessionRow row)_editBefore=Clone(row);}
    void CellEditEnding(object s,DataGridCellEditEndingEventArgs e)
    {
        if(e.Row.Item is not SessionRow row)return;var before=_editBefore;_editBefore=null;
        if(e.Column.DisplayIndex is 0 or 1&&e.EditingElement is TextBox timeEditor)
        {
            if(!TryReadTime(timeEditor.Text,out var time)||before is null)
            {
                e.Cancel=true;if(before is not null){CopyValues(before,row);timeEditor.Text=e.Column.DisplayIndex==0?before.StartText:before.EndText;}RefreshDayStatus();Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,ShowTimeFormatError);return;
            }
            if(e.Column.DisplayIndex==0)row.Start=row.Start.Date+time;else row.End=row.Start.Date+time;
            if(!ValidInterval(row)){e.Cancel=true;CopyValues(before,row);timeEditor.Text=e.Column.DisplayIndex==0?before.StartText:before.EndText;RefreshDayStatus();Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,ShowTimeError);return;}
        }
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>
        {
            if(!_undoing&&before is not null&&!Same(before,row))PushRestore(row,before);_repo.Save(row);RefreshDayStatus();LoadWeek();RefreshRecent();
        }));
    }
    void PreparingCellForEdit(object s,DataGridPreparingCellForEditEventArgs e)
    {
        if(e.EditingElement is not TextBox text)return;
        var isTime=e.Column.DisplayIndex is 0 or 1;
        if(isTime){text.Tag=e.Column.DisplayIndex;text.PreviewMouseLeftButtonDown+=TimeMouseDown;text.PreviewKeyDown+=TimeKeyDown;text.TextChanged-=TimeTextChanged;text.TextChanged+=TimeTextChanged;}
    }
    void TimeTextChanged(object s,TextChangedEventArgs e)
    {
        if(s is not TextBox { DataContext: SessionRow row,Tag: int column } text||column is not (0 or 1)||!TryReadTime(text.Text,out var time))return;
        var start=column==0?row.Start.Date+time:row.Start;var end=column==1?row.Start.Date+time:row.End;
        if(end is not null&&end<=start)return;
        RefreshDayStatus(row,start,end);
    }
    void TimeMouseDown(object s,MouseButtonEventArgs e){if(s is TextBox text){text.SelectAll();e.Handled=true;}}
    void TimeKeyDown(object s,KeyEventArgs e)
    {
        if(s is not TextBox text||e.Key is not (Key.Up or Key.Down))return;
        if(!TryReadTime(text.Text,out var time))return;
        time=time.Add(TimeSpan.FromMinutes(e.Key==Key.Up?5:-5));if(time<TimeSpan.Zero)time=TimeSpan.FromHours(23)+TimeSpan.FromMinutes(55);if(time>=TimeSpan.FromDays(1))time=TimeSpan.Zero;
        text.Text=$"{(int)time.TotalHours:00}:{time.Minutes:00}";text.SelectAll();e.Handled=true;
    }
    void DayGridKeyDown(object s,KeyEventArgs e)
    {
        if(e.Key==Key.Delete&&Keyboard.Modifiers==ModifierKeys.None)
        {
            ClearCurrentDayCell();e.Handled=true;return;
        }
        if(e.Key==Key.Enter&&e.OriginalSource is not TextBox)
        {
            if(DayGrid.CurrentCell.Column is not null&&!DayGrid.CurrentCell.Column.IsReadOnly)DayGrid.BeginEdit();
            e.Handled=true;return;
        }
        if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;
        if(e.Key==Key.C){CopyCell();e.Handled=true;}else if(e.Key==Key.V){PasteCell();e.Handled=true;}
    }
    void ClearCurrentDayCell()
    {
        DayGrid.CommitEdit(DataGridEditingUnit.Cell,true);DayGrid.CommitEdit(DataGridEditingUnit.Row,true);
        if(DayGrid.CurrentCell.Item is not SessionRow row||DayGrid.CurrentCell.Column is null||DayGrid.CurrentCell.Column.IsReadOnly)return;
        var columnIndex=DayGrid.CurrentCell.Column.DisplayIndex;if(columnIndex==0)return;
        var before=Clone(row);SetCellValue(row,columnIndex,"");
        if(Same(before,row))return;
        PushRestore(row,before);_repo.Save(row);DayGrid.Items.Refresh();RefreshDayStatus();LoadWeek();RefreshRecent();RestoreCellFocus(row,columnIndex);
    }
    void WindowKeyDown(object s,KeyEventArgs e){if((Keyboard.Modifiers&ModifierKeys.Control)!=0&&e.Key==Key.Z){UndoLast();e.Handled=true;}}
    void CopyCell()
    {
        if(DayGrid.CurrentCell.Item is not SessionRow row||DayGrid.CurrentCell.Column is null)return;
        var columnIndex=DayGrid.CurrentCell.Column.DisplayIndex;var value=CellValue(row,columnIndex);
        try{Clipboard.SetText(value??"");}catch(Exception ex){MessageBox.Show($"Could not copy: {ex.Message}","TaskUp");}
        finally{RestoreCellFocus(row,columnIndex);}
    }
    void PasteCell()
    {
        if(DayGrid.CurrentCell.Item is not SessionRow row||DayGrid.CurrentCell.Column is null||DayGrid.CurrentCell.Column.IsReadOnly)return;
        var column=DayGrid.CurrentCell.Column;var columnIndex=column.DisplayIndex;
        try
        {
            var value=Clipboard.ContainsText()?Clipboard.GetText().Split(new[]{'\r','\n','\t'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"":"";
            var before=Clone(row);SetCellValue(row,columnIndex,value);
            if(!ValidInterval(row)){CopyValues(before,row);DayGrid.Items.Refresh();RefreshDayStatus();ShowTimeError();return;}
            if(!Same(before,row))PushRestore(row,before);_repo.Save(row);DayGrid.Items.Refresh();RefreshDayStatus();LoadWeek();RefreshRecent();
        }
        catch(Exception ex){MessageBox.Show($"Could not paste: {ex.Message}","TaskUp");}
        finally{RestoreCellFocus(row,columnIndex);}
    }
    static string CellValue(SessionRow row,int column)=>column switch{0=>row.StartText,1=>row.EndText,2=>row.Project,3=>row.Epic,4=>row.Activity,5=>row.Comment,_=>row.Duration};
    static void SetCellValue(SessionRow row,int column,string value){switch(column){case 0:row.StartText=value;break;case 1:row.EndText=value;break;case 2:row.Project=value;break;case 3:row.Epic=value;break;case 4:row.Activity=value;break;case 5:row.Comment=value;break;}}
    static bool TryReadTime(string value,out TimeSpan time){var text=value.Trim().Replace('.',':');if(text.Length is 3 or 4&&text.All(char.IsDigit))text=text.Insert(text.Length-2,":");return TimeSpan.TryParse(text,out time);}
    static bool ValidInterval(SessionRow row)=>row.End is null||row.End>row.Start;
    static void ShowTimeError()=>MessageBox.Show("The start time must be earlier than the end time. The previous value has been restored.","Invalid time",MessageBoxButton.OK,MessageBoxImage.Warning);
    static void ShowTimeFormatError()=>MessageBox.Show("Enter a time such as 1020, 10:20, or 10.20. The previous value has been restored.","Invalid time format",MessageBoxButton.OK,MessageBoxImage.Warning);
    void PushRestore(SessionRow target,SessionRow before)
    {
        _undo.Push(()=>{CopyValues(before,target);_repo.Save(target);DayGrid.Items.Refresh();RefreshDayStatus();LoadWeek();RefreshRecent();SelectCell(target,Math.Max(0,DayGrid.CurrentCell.Column?.DisplayIndex??0));});
    }
    void UndoLast()
    {
        DayGrid.CommitEdit(DataGridEditingUnit.Cell,true);DayGrid.CommitEdit(DataGridEditingUnit.Row,true);
        Dispatcher.BeginInvoke(()=>{if(_undo.Count==0)return;_undoing=true;try{_undo.Pop()();}finally{_undoing=false;}});
    }
    void RestoreCellFocus(SessionRow row,int columnIndex)=>Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>SelectCell(row,columnIndex)));
    void SelectCell(SessionRow row,int columnIndex)
    {
        var column=DayGrid.Columns.FirstOrDefault(x=>x.DisplayIndex==columnIndex);if(column is null||!DayGrid.Items.Contains(row))return;
        var target=new DataGridCellInfo(row,column);DayGrid.SelectedCells.Clear();DayGrid.CurrentCell=target;DayGrid.SelectedCells.Add(target);
        DayGrid.ScrollIntoView(row,column);DayGrid.UpdateLayout();
        if(DayGrid.ItemContainerGenerator.ContainerFromItem(row) is DataGridRow rowContainer&&FindCell(rowContainer,column) is DataGridCell cell)
        {
            cell.Focus();Keyboard.Focus(cell);return;
        }
        DayGrid.Focus();
    }
    static DataGridCell? FindCell(DependencyObject parent,DataGridColumn column)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i);if(child is DataGridCell cell&&cell.Column==column)return cell;
            if(FindCell(child,column) is DataGridCell nested)return nested;
        }
        return null;
    }
    static SessionRow Clone(SessionRow s)=>new(){Id=s.Id,Start=s.Start,End=s.End,Project=s.Project,Epic=s.Epic,Activity=s.Activity,Comment=s.Comment};
    static void CopyValues(SessionRow source,SessionRow target){target.Start=source.Start;target.End=source.End;target.Project=source.Project;target.Epic=source.Epic;target.Activity=source.Activity;target.Comment=source.Comment;}
    static bool Same(SessionRow a,SessionRow b)=>a.Start==b.Start&&a.End==b.End&&a.Project==b.Project&&a.Epic==b.Epic&&a.Activity==b.Activity&&a.Comment==b.Comment;
    void AddRowClick(object s,RoutedEventArgs e)
    {
        var day=(DayPicker.SelectedDate??DateTime.Today).Date;var rows=(DayGrid.ItemsSource as ObservableCollection<SessionRow>)??new();var previous=rows.LastOrDefault();var recent=_repo.Recent().FirstOrDefault();
        var start=previous?.End??day.Add(DateTime.Now.TimeOfDay);if(start.Date!=day)start=day.AddHours(9);
        var row=new SessionRow{Start=start,End=start.AddMinutes(30),Project=recent?.Project??"",Epic=recent?.Epic??"",Activity=recent?.Activity??""};
        _repo.Save(row);rows.Add(row);_undo.Push(()=>{_repo.Delete(row.Id);rows.Remove(row);DayGrid.Items.Refresh();RefreshDayStatus();LoadWeek();RefreshRecent();});DayGrid.ItemsSource=rows;DayGrid.SelectedItem=row;DayGrid.ScrollIntoView(row);RefreshDayStatus();LoadWeek();RefreshRecent();
    }
    void DeleteClick(object s,RoutedEventArgs e)
    {
        DayGrid.CommitEdit(DataGridEditingUnit.Cell,true);DayGrid.CommitEdit(DataGridEditingUnit.Row,true);
        var row=DayGrid.CurrentCell.Item as SessionRow??DayGrid.SelectedItem as SessionRow??DayGrid.SelectedCells.Select(x=>x.Item).OfType<SessionRow>().FirstOrDefault();
        if(row is null){MessageBox.Show("Select a cell in the row you want to delete.","TaskUp");return;}
        var copy=Clone(row);_repo.Delete(row.Id);_undo.Push(()=>{copy.Id=0;_repo.Save(copy);LoadDay();LoadWeek();RefreshRecent();});LoadDay();LoadWeek();RefreshRecent();
    }
    void CopyWeekClick(object s,RoutedEventArgs e)
    {
        var start=StartOfWeek(WeekPicker.SelectedDate??DateTime.Today);var rows=_repo.Week(start);var b=new StringBuilder();foreach(var g in rows.GroupBy(x=>x.Day)){b.AppendLine(g.Key.ToString("dddd dd/MM",CultureInfo.GetCultureInfo("en-US")));foreach(var x in g)b.AppendLine($"{x.Project} - {x.Epic}\t{SessionRow.Format(TimeSpan.FromHours(RoundForSap(x.Registered.TotalHours+x.Added)))}");b.AppendLine();}CopyToClipboard(b.ToString());
    }
    string WeekSummaryHeader(DateTime start)=>"Project - Epic\t"+string.Join('\t',Enumerable.Range(0,7).Select(i=>start.AddDays(i).ToString("dddd dd/MM",CultureInfo.GetCultureInfo("en-US"))));
    static string WeekSummaryLine(WeekSummaryRow row)=>CleanClipboardText(row.ProjectEpic)+"\t"+string.Join('\t',row.DayHours);
    static string CleanClipboardText(string value)=>value.Replace('\t',' ').Replace('\r',' ').Replace('\n',' ');
    void CopyWeekSummaryRowClick(object s,RoutedEventArgs e){if(s is Button{DataContext:WeekSummaryRow row})CopyToClipboard(string.Join('\t',row.DayHours));}
    void CopyWeekSummaryClick(object s,RoutedEventArgs e)
    {
        var start=StartOfWeek(WeekPicker.SelectedDate??DateTime.Today);var rows=(WeekSummaryGrid.ItemsSource as IEnumerable<WeekSummaryRow>)?.ToList()??new();var builder=new StringBuilder(WeekSummaryHeader(start));foreach(var row in rows)builder.AppendLine().Append(WeekSummaryLine(row));CopyToClipboard(builder.ToString());
    }
    static void CopyToClipboard(string text){try{Clipboard.SetText(text);}catch(Exception ex){MessageBox.Show($"Could not copy: {ex.Message}","TaskUp");}}
    void ShowSettings()
    {
        new CalendarWindow(_repo){Owner=this}.ShowDialog();AppearanceManager.Apply(AppearanceManager.Load(_repo));Topmost=_repo.BoolSetting("always_on_top",true);_weekStartDay=_repo.WeekStartDay();LoadRoundingPolicy();_recentFieldSettings=_repo.RecentFieldSettings();RefreshRecent();LoadDay();var start=StartOfWeek(WeekPicker.SelectedDate??DateTime.Today);if(WeekPicker.SelectedDate?.Date!=start)WeekPicker.SelectedDate=start;else LoadWeek();RefreshReport();
    }
    void OpenSettingsClick(object s,RoutedEventArgs e)=>ShowSettings();
    void TabsSelectionChanged(object s,SelectionChangedEventArgs e)
    {
        if(SettingsTab is null||Tabs.SelectedItem!=SettingsTab){if(Tabs.SelectedIndex>=0)_lastMainTabIndex=Tabs.SelectedIndex;return;}if(_openingSettings)return;_openingSettings=true;Tabs.SelectedIndex=Math.Clamp(_lastMainTabIndex,0,3);ShowSettings();_openingSettings=false;
    }
    void ReportDatesChanged(object s,SelectionChangedEventArgs e){if(IsLoaded)RefreshReport();}
    void RefreshReportClick(object s,RoutedEventArgs e)=>RefreshReport();
    void RefreshReport()
    {
        if(ReportFromPicker.SelectedDate is not DateTime from||ReportToPicker.SelectedDate is not DateTime to)return;if(from>to){(from,to)=(to,from);ReportFromPicker.SelectedDate=from;ReportToPicker.SelectedDate=to;}
        var data=_repo.Report(from,to,_reportProject,_reportEpic,_reportActivity);ReportGrid.ItemsSource=data;DrawPie(data);ReportBackButton.Visibility=_reportProject is null?Visibility.Collapsed:Visibility.Visible;
        ReportLevelText.Text=_reportProject is null?"Time by project":_reportEpic is null?$"{_reportProject} · by epic":_reportActivity is null?$"{_reportProject} / {_reportEpic} · by task":$"{_reportProject} / {_reportEpic} / {_reportActivity} · by comment";
    }
    void DrawPie(IReadOnlyList<ReportSlice> data)
    {
        ReportPie.Children.Clear();var total=data.Sum(x=>x.Hours);if(total<=0){ReportPie.Children.Add(new TextBlock{Text="No time has been recorded in this period",Foreground=Brushes.Gray,FontSize=16});return;}
        const double cx=310,cy=215,r=150;double angle=-90;var labels=new List<(ReportSlice Slice,double Mid,bool Right,double Y,Point Edge,Brush Color)>();
        for(var i=0;i<data.Count;i++)
        {
            var slice=data[i];var sweep=slice.Hours/total*360;var mid=angle+sweep/2;Shape shape;
            if(sweep>=359.999){shape=new Ellipse{Width=r*2,Height=r*2};Canvas.SetLeft(shape,cx-r);Canvas.SetTop(shape,cy-r);}
            else{var start=PointOnCircle(cx,cy,r,angle);var end=PointOnCircle(cx,cy,r,angle+sweep);var figure=new PathFigure{StartPoint=new Point(cx,cy),IsClosed=true};figure.Segments.Add(new LineSegment(start,true));figure.Segments.Add(new ArcSegment(end,new Size(r,r),0,sweep>180,SweepDirection.Clockwise,true));shape=new System.Windows.Shapes.Path{Data=new PathGeometry(new[]{figure})};}
            var color=ReportColors[i%ReportColors.Length];shape.Fill=color;shape.Stroke=Brushes.White;shape.StrokeThickness=2;shape.Tag=slice;shape.Cursor=Cursors.Hand;shape.ToolTip=$"{slice.Label}: {slice.HoursText} ({slice.PercentageText})";ReportPie.Children.Add(shape);if(i<14){var edge=PointOnCircle(cx,cy,r,mid);var radians=mid*Math.PI/180;labels.Add((slice,mid,Math.Cos(radians)>=0,cy+(r+24)*Math.Sin(radians),edge,color));}angle+=sweep;
        }
        DrawPieLabels(labels.Where(x=>!x.Right).OrderBy(x=>x.Y),false);DrawPieLabels(labels.Where(x=>x.Right).OrderBy(x=>x.Y),true);
        var hole=new Ellipse{Width=130,Height=130,Fill=(Brush)FindResource("Surface"),IsHitTestVisible=false};Canvas.SetLeft(hole,cx-65);Canvas.SetTop(hole,cy-65);ReportPie.Children.Add(hole);
        var totalText=new TextBlock{Text=$"Total\n{SessionRow.Format(TimeSpan.FromHours(total))}",TextAlignment=TextAlignment.Center,FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=(Brush)FindResource("TextPrimary"),Width=120,IsHitTestVisible=false};Canvas.SetLeft(totalText,cx-60);Canvas.SetTop(totalText,cy-28);ReportPie.Children.Add(totalText);
    }
    void DrawPieLabels(IEnumerable<(ReportSlice Slice,double Mid,bool Right,double Y,Point Edge,Brush Color)> source,bool right)
    {
        double nextY=8;foreach(var item in source){var y=Math.Clamp(Math.Max(item.Y-9,nextY),8,407);nextY=y+24;var lineEnd=new Point(right?472:148,y+9);var line=new Polyline{Stroke=item.Color,StrokeThickness=1.5,IsHitTestVisible=false,Points=new PointCollection{item.Edge,lineEnd}};ReportPie.Children.Add(line);var label=new TextBlock{Text=$"{Truncate(item.Slice.Label,18)} · {item.Slice.PercentageText}",Width=140,TextAlignment=right?TextAlignment.Left:TextAlignment.Right,FontSize=11,ToolTip=$"{item.Slice.Label}: {item.Slice.HoursText} ({item.Slice.PercentageText})",Tag=item.Slice,Cursor=Cursors.Hand};Canvas.SetLeft(label,right?476:4);Canvas.SetTop(label,y);ReportPie.Children.Add(label);}
    }
    static string Truncate(string text,int max)=>text.Length<=max?text:text[..Math.Max(1,max-1)]+"…";
    static Point PointOnCircle(double cx,double cy,double radius,double degrees){var radians=degrees*Math.PI/180;return new Point(cx+radius*Math.Cos(radians),cy+radius*Math.Sin(radians));}
    void ReportPieClick(object s,MouseButtonEventArgs e){if(e.OriginalSource is FrameworkElement{Tag:ReportSlice slice})DrillDown(slice);}
    void ReportGridSelected(object s,SelectionChangedEventArgs e){if(ReportGrid.SelectedItem is ReportSlice slice&&e.AddedItems.Count>0)DrillDown(slice);}
    void DrillDown(ReportSlice slice){if(_reportProject is null)_reportProject=slice.Label;else if(_reportEpic is null)_reportEpic=slice.Label;else if(_reportActivity is null)_reportActivity=slice.Label;else return;RefreshReport();}
    void ReportBackClick(object s,RoutedEventArgs e){if(_reportActivity is not null)_reportActivity=null;else if(_reportEpic is not null)_reportEpic=null;else _reportProject=null;RefreshReport();}
    void CopyReportClick(object s,RoutedEventArgs e)
    {
        var rows=(ReportGrid.ItemsSource as IEnumerable<ReportSlice>)?.ToList()??new();var builder=new StringBuilder("Item\tHours\tPercentage");foreach(var row in rows)builder.AppendLine().Append(row.Label.Replace('\t',' ').Replace('\r',' ').Replace('\n',' ')).Append('\t').Append(row.Hours.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)).Append('\t').Append(row.Percentage.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)).Append('%');try{Clipboard.SetText(builder.ToString());}catch(Exception ex){MessageBox.Show(ex.Message,"Could not copy");}
    }
}
