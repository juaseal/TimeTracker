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
    readonly TimeRepository _repo=new(); readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromSeconds(1)}; readonly Stack<Action> _undo=new(); SessionRow? _active; SessionRow? _editBefore; bool _undoing; string? _reportProject; string? _reportEpic; string? _reportActivity; int _lastMainTabIndex; bool _openingSettings;
    static readonly Brush[] ReportColors={new SolidColorBrush(Color.FromRgb(109,93,251)),new SolidColorBrush(Color.FromRgb(39,131,106)),new SolidColorBrush(Color.FromRgb(235,150,48)),new SolidColorBrush(Color.FromRgb(211,78,94)),new SolidColorBrush(Color.FromRgb(55,136,216)),new SolidColorBrush(Color.FromRgb(151,91,178)),new SolidColorBrush(Color.FromRgb(76,164,84)),new SolidColorBrush(Color.FromRgb(210,112,45))};
    public MainWindow(){InitializeComponent();DayPicker.SelectedDate=DateTime.Today;WeekPicker.SelectedDate=MondayOf(DateTime.Today);ReportFromPicker.SelectedDate=DateTime.Today.AddMonths(-1);ReportToPicker.SelectedDate=DateTime.Today;_timer.Tick+=(_,_)=>RefreshClock();_timer.Start();RefreshAll();RefreshReport();}
    void RefreshAll(){_active=_repo.Active();RefreshClock();var recent=_repo.Recent();ProjectBox.ItemsSource=recent.Select(x=>x.Project).Distinct().ToList();RecentList.ItemsSource=recent;LoadDay();LoadWeek();}
    void RefreshClock()
    {
        _active=_repo.Active();
        if(_active is null)
        {
            ActiveTitle.Text="Sin tarea activa";ActiveDetail.Text="Elige proyecto, épica y actividad";Clock.Text="00:00:00";
            ActiveStateBadge.Visibility=Visibility.Collapsed;HeaderStatusText.Text="Sin tarea activa";HeaderStatusDot.Fill=Brushes.Gray;return;
        }
        ActiveTitle.Text=$"{_active.Project} · {_active.Epic}";ActiveDetail.Text=_active.Activity+(string.IsNullOrWhiteSpace(_active.Comment)?"":$"  —  {_active.Comment}");
        Clock.Text=((DateTime.Now-_active.Start) is var d)?$"{(int)d.TotalHours:00}:{d.Minutes:00}:{d.Seconds:00}":"";
        ActiveStateBadge.Visibility=Visibility.Visible;HeaderStatusText.Text="Registrando";HeaderStatusDot.Fill=(Brush)FindResource("Success");
    }
    void StartClick(object s,RoutedEventArgs e){var p=ProjectBox.Text.Trim();var ep=EpicBox.Text.Trim();var a=ActivityBox.Text.Trim();if(ActivityBox.SelectedItem is ActivitySuggestion x){p=x.Project;ep=x.Epic;a=x.Activity;}if(string.IsNullOrWhiteSpace(p)||string.IsNullOrWhiteSpace(ep)||string.IsNullOrWhiteSpace(a)){MessageBox.Show("Indica proyecto, épica y actividad.","TimeTracker");return;}_repo.Start(p,ep,a,CommentBox.Text.Trim());CommentBox.Clear();RefreshAll();}
    void StopClick(object s,RoutedEventArgs e){_repo.Stop();RefreshAll();}
    void ProjectChanged(object s,SelectionChangedEventArgs e)
    {
        var p=ProjectBox.SelectedItem?.ToString()??ProjectBox.Text;var recent=_repo.Recent(p);
        RecentList.ItemsSource=recent;ActivityBox.ItemsSource=recent;EpicBox.ItemsSource=recent.Select(x=>x.Epic).Distinct().ToList();
        if(recent.FirstOrDefault() is { } last){EpicBox.Text=last.Epic;ActivityBox.SelectedItem=last;ActivityBox.Text=last.Activity;}
    }
    void RecentSelected(object s,SelectionChangedEventArgs e)
    {
        if(RecentList.SelectedItem is not ActivitySuggestion x)return;
        ProjectBox.Text=x.Project;EpicBox.Text=x.Epic;ActivityBox.SelectedItem=x;ActivityBox.Text=x.Activity;CommentBox.Clear();
        _active=_repo.Active();if(_active is not null&&string.Equals(_active.Project,x.Project,StringComparison.OrdinalIgnoreCase)&&string.Equals(_active.Epic,x.Epic,StringComparison.OrdinalIgnoreCase)&&string.Equals(_active.Activity,x.Activity,StringComparison.OrdinalIgnoreCase))return;
        StartClick(s,new RoutedEventArgs());
    }
    void LoadDay()
    {
        var day=(DayPicker.SelectedDate??DateTime.Today).Date;var rows=_repo.Day(day);var expected=_repo.ExpectedHours(day);
        var actual=rows.Sum(x=>Math.Max(0,((x.End??DateTime.Now)-x.Start).TotalHours));var difference=expected-actual;
        if(expected>0&&difference>1d/3600)
        {
            var start=rows.Count==0?day.AddHours(9):(rows.Max(x=>x.End??DateTime.Now));
            rows.Add(new SessionRow{Id=-1,Start=start,End=start.AddHours(difference),Activity="Sin especificar",Comment="Tiempo pendiente de asignar"});
            DayStatusText.Text=$"Jornada {FormatHours(expected)} · Faltan {FormatHours(difference)}";DayStatusText.Foreground=System.Windows.Media.Brushes.DarkOrange;
        }
        else if(expected>0&&difference< -1d/3600){DayStatusText.Text=$"Jornada {FormatHours(expected)} · Exceso {FormatHours(-difference)}";DayStatusText.Foreground=System.Windows.Media.Brushes.Red;}
        else if(expected>0){DayStatusText.Text=$"Jornada completa · {FormatHours(expected)}";DayStatusText.Foreground=System.Windows.Media.Brushes.ForestGreen;}
        else{DayStatusText.Text="Día sin jornada configurada";DayStatusText.Foreground=System.Windows.Media.Brushes.Gray;}
        DayGrid.ItemsSource=new ObservableCollection<SessionRow>(rows);
    }
    static string FormatHours(double hours)=>SessionRow.Format(TimeSpan.FromHours(hours));
    static DateTime MondayOf(DateTime date)=>date.Date.AddDays(-((7+(int)date.DayOfWeek-1)%7));
    void PreviousDayClick(object s,RoutedEventArgs e)=>DayPicker.SelectedDate=(DayPicker.SelectedDate??DateTime.Today).Date.AddDays(-1);
    void TodayClick(object s,RoutedEventArgs e)=>DayPicker.SelectedDate=DateTime.Today;
    void NextDayClick(object s,RoutedEventArgs e)=>DayPicker.SelectedDate=(DayPicker.SelectedDate??DateTime.Today).Date.AddDays(1);
    void LoadWeek(){var monday=MondayOf(WeekPicker.SelectedDate??DateTime.Today);WeekNumberText.Text=$"Semana {ISOWeek.GetWeekOfYear(monday)} · {monday:dd/MM}–{monday.AddDays(6):dd/MM}";var rows=_repo.Week(monday);ApplyDailySummaries(rows);var view=CollectionViewSource.GetDefaultView(rows);view.GroupDescriptions.Clear();view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SummaryRow.DayText)));WeekGrid.ItemsSource=view;}
    void ApplyDailySummaries(IEnumerable<SummaryRow> source)
    {
        var rows=source.ToList();foreach(var group in rows.GroupBy(x=>x.Day)){var total=group.Sum(x=>x.Registered.TotalHours+x.Added);var expected=_repo.ExpectedHours(group.Key);var difference=total-expected;var totalText=SessionRow.Format(TimeSpan.FromHours(total));var expectedText=SessionRow.Format(TimeSpan.FromHours(expected));var status=expected<=0?"Libre":difference>0.01?$"+{SessionRow.Format(TimeSpan.FromHours(difference))}":difference< -0.01?$"-{SessionRow.Format(TimeSpan.FromHours(-difference))}":"OK";var brush=expected<=0?Brushes.Gray:difference>0.01?Brushes.Firebrick:difference< -0.01?Brushes.DarkOrange:Brushes.ForestGreen;foreach(var row in group){row.DailyTotalText=totalText;row.DailyExpectedText=expectedText;row.DailyStatusText=status;row.DailyStatusBrush=brush;}}
    }
    void WeekCellEditEnding(object s,DataGridCellEditEndingEventArgs e){if(e.Row.Item is SummaryRow row)Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>{_repo.SaveAdjustment(row);ApplyDailySummaries(WeekGrid.ItemsSource.Cast<SummaryRow>());}));}
    void PreviousWeekClick(object s,RoutedEventArgs e)=>WeekPicker.SelectedDate=MondayOf(WeekPicker.SelectedDate??DateTime.Today).AddDays(-7);
    void NextWeekClick(object s,RoutedEventArgs e)=>WeekPicker.SelectedDate=MondayOf(WeekPicker.SelectedDate??DateTime.Today).AddDays(7);
    void WeekRowDoubleClick(object s,MouseButtonEventArgs e)
    {
        if(WeekGrid.SelectedItem is not SummaryRow row)return;var text=string.Join(Environment.NewLine,_repo.TasksForSummary(row));
        var editor=new TextBox{Text=text,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(12),FontSize=14};
        var copy=new Button{Content="Copiar",HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(8),MinWidth=100};copy.Click+=(_,_)=>{try{Clipboard.SetText(editor.Text);}catch(Exception ex){MessageBox.Show(ex.Message,"No se pudo copiar");}};
        var panel=new DockPanel();DockPanel.SetDock(copy,Dock.Bottom);panel.Children.Add(copy);panel.Children.Add(editor);
        var window=new Window{Title=$"{row.Day:dd/MM/yyyy} · {row.Project} · {row.Epic}",Owner=this,Width=620,Height=420,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel};
        window.ContentRendered+=(_,_)=>{editor.Focus();editor.SelectAll();};
        window.PreviewKeyDown+=(_,key)=>
        {
            if(key.Key==Key.C&&(Keyboard.Modifiers&ModifierKeys.Control)!=0)
            {
                key.Handled=true;try{Clipboard.SetText(editor.Text);window.Close();}catch(Exception ex){MessageBox.Show(ex.Message,"No se pudo copiar");}return;
            }
            if(key.Key==Key.Escape){key.Handled=true;window.Close();}
        };
        window.ShowDialog();
    }
    void DayChanged(object s,SelectionChangedEventArgs e)=>LoadDay(); void WeekChanged(object s,SelectionChangedEventArgs e){var monday=MondayOf(WeekPicker.SelectedDate??DateTime.Today);if(WeekPicker.SelectedDate?.Date!=monday){WeekPicker.SelectedDate=monday;return;}LoadWeek();}
    void BeginningEdit(object s,DataGridBeginningEditEventArgs e){if(e.Row.Item is SessionRow row)_editBefore=Clone(row);}
    void CellEditEnding(object s,DataGridCellEditEndingEventArgs e)
    {
        if(e.Row.Item is not SessionRow row)return;var before=_editBefore;_editBefore=null;
        if(e.Column.DisplayIndex is 0 or 1&&e.EditingElement is TextBox timeEditor)
        {
            if(!TryReadTime(timeEditor.Text,out var time)||before is null)
            {
                e.Cancel=true;if(before is not null){CopyValues(before,row);timeEditor.Text=e.Column.DisplayIndex==0?before.StartText:before.EndText;}Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,ShowTimeFormatError);return;
            }
            if(e.Column.DisplayIndex==0)row.Start=row.Start.Date+time;else row.End=row.Start.Date+time;
            if(!ValidInterval(row)){e.Cancel=true;CopyValues(before,row);timeEditor.Text=e.Column.DisplayIndex==0?before.StartText:before.EndText;Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,ShowTimeError);return;}
        }
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>
        {
            if(!_undoing&&before is not null&&!Same(before,row))PushRestore(row,before);_repo.Save(row);LoadWeek();
        }));
    }
    void PreparingCellForEdit(object s,DataGridPreparingCellForEditEventArgs e)
    {
        if(e.EditingElement is not TextBox text)return;
        var isTime=e.Column.DisplayIndex is 0 or 1;
        text.SelectAll();if(isTime){text.PreviewMouseLeftButtonDown+=TimeMouseDown;text.PreviewKeyDown+=TimeKeyDown;}
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
        if(e.Key==Key.Enter&&e.OriginalSource is not TextBox)
        {
            if(DayGrid.CurrentCell.Column is not null&&!DayGrid.CurrentCell.Column.IsReadOnly)DayGrid.BeginEdit();
            e.Handled=true;return;
        }
        if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;
        if(e.Key==Key.C){CopyCell();e.Handled=true;}else if(e.Key==Key.V){PasteCell();e.Handled=true;}
    }
    void WindowKeyDown(object s,KeyEventArgs e){if((Keyboard.Modifiers&ModifierKeys.Control)!=0&&e.Key==Key.Z){UndoLast();e.Handled=true;}}
    void CopyCell()
    {
        if(DayGrid.CurrentCell.Item is not SessionRow row||DayGrid.CurrentCell.Column is null)return;
        var columnIndex=DayGrid.CurrentCell.Column.DisplayIndex;var value=CellValue(row,columnIndex);
        try{Clipboard.SetText(value??"");}catch(Exception ex){MessageBox.Show($"No se pudo copiar: {ex.Message}","TimeTracker");}
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
            if(!ValidInterval(row)){CopyValues(before,row);DayGrid.Items.Refresh();ShowTimeError();return;}
            if(!Same(before,row))PushRestore(row,before);_repo.Save(row);DayGrid.Items.Refresh();LoadWeek();
        }
        catch(Exception ex){MessageBox.Show($"No se pudo pegar: {ex.Message}","TimeTracker");}
        finally{RestoreCellFocus(row,columnIndex);}
    }
    static string CellValue(SessionRow row,int column)=>column switch{0=>row.StartText,1=>row.EndText,2=>row.Project,3=>row.Epic,4=>row.Activity,5=>row.Comment,_=>row.Duration};
    static void SetCellValue(SessionRow row,int column,string value){switch(column){case 0:row.StartText=value;break;case 1:row.EndText=value;break;case 2:row.Project=value;break;case 3:row.Epic=value;break;case 4:row.Activity=value;break;case 5:row.Comment=value;break;}}
    static bool TryReadTime(string value,out TimeSpan time){var text=value.Trim().Replace('.',':');if(text.Length is 3 or 4&&text.All(char.IsDigit))text=text.Insert(text.Length-2,":");return TimeSpan.TryParse(text,out time);}
    static bool ValidInterval(SessionRow row)=>row.End is null||row.End>row.Start;
    static void ShowTimeError()=>MessageBox.Show("La hora de inicio debe ser anterior a la hora de fin. Se ha recuperado el valor anterior.","Hora no válida",MessageBoxButton.OK,MessageBoxImage.Warning);
    static void ShowTimeFormatError()=>MessageBox.Show("Escribe una hora como 1020, 10:20 o 10.20. Se ha recuperado el valor anterior.","Formato de hora no válido",MessageBoxButton.OK,MessageBoxImage.Warning);
    void PushRestore(SessionRow target,SessionRow before)
    {
        _undo.Push(()=>{CopyValues(before,target);_repo.Save(target);DayGrid.Items.Refresh();LoadWeek();SelectCell(target,Math.Max(0,DayGrid.CurrentCell.Column?.DisplayIndex??0));});
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
        _repo.Save(row);rows.Add(row);_undo.Push(()=>{_repo.Delete(row.Id);rows.Remove(row);DayGrid.Items.Refresh();LoadWeek();});DayGrid.ItemsSource=rows;DayGrid.SelectedItem=row;DayGrid.ScrollIntoView(row);LoadWeek();
    }
    void DeleteClick(object s,RoutedEventArgs e)
    {
        DayGrid.CommitEdit(DataGridEditingUnit.Cell,true);DayGrid.CommitEdit(DataGridEditingUnit.Row,true);
        var row=DayGrid.CurrentCell.Item as SessionRow??DayGrid.SelectedItem as SessionRow??DayGrid.SelectedCells.Select(x=>x.Item).OfType<SessionRow>().FirstOrDefault();
        if(row is null){MessageBox.Show("Selecciona una celda de la fila que quieres eliminar.","TimeTracker");return;}
        var copy=Clone(row);_repo.Delete(row.Id);_undo.Push(()=>{copy.Id=0;_repo.Save(copy);LoadDay();LoadWeek();});LoadDay();LoadWeek();
    }
    void CopyWeekClick(object s,RoutedEventArgs e)
    {
        var rows=_repo.Week(WeekPicker.SelectedDate??DateTime.Today);var b=new StringBuilder();foreach(var g in rows.GroupBy(x=>x.Day)){b.AppendLine(g.Key.ToString("dddd dd/MM"));foreach(var x in g)b.AppendLine($"{x.Project} - {x.Epic}\t{x.TotalText}");b.AppendLine();}
        try{Clipboard.SetText(b.ToString());}catch(Exception ex){MessageBox.Show($"No se pudo copiar: {ex.Message}","TimeTracker");}
    }
    void OpenSettingsClick(object s,RoutedEventArgs e){new CalendarWindow(_repo){Owner=this}.ShowDialog();LoadDay();LoadWeek();}
    void TabsSelectionChanged(object s,SelectionChangedEventArgs e)
    {
        if(SettingsTab is null||Tabs.SelectedItem!=SettingsTab){if(Tabs.SelectedIndex>=0)_lastMainTabIndex=Tabs.SelectedIndex;return;}if(_openingSettings)return;_openingSettings=true;Tabs.SelectedIndex=Math.Clamp(_lastMainTabIndex,0,3);new CalendarWindow(_repo){Owner=this}.ShowDialog();LoadDay();LoadWeek();_openingSettings=false;
    }
    void ReportDatesChanged(object s,SelectionChangedEventArgs e){if(IsLoaded)RefreshReport();}
    void RefreshReportClick(object s,RoutedEventArgs e)=>RefreshReport();
    void RefreshReport()
    {
        if(ReportFromPicker.SelectedDate is not DateTime from||ReportToPicker.SelectedDate is not DateTime to)return;if(from>to){(from,to)=(to,from);ReportFromPicker.SelectedDate=from;ReportToPicker.SelectedDate=to;}
        var data=_repo.Report(from,to,_reportProject,_reportEpic,_reportActivity);ReportGrid.ItemsSource=data;DrawPie(data);ReportBackButton.Visibility=_reportProject is null?Visibility.Collapsed:Visibility.Visible;
        ReportLevelText.Text=_reportProject is null?"Tiempo por proyecto":_reportEpic is null?$"{_reportProject} · por épica":_reportActivity is null?$"{_reportProject} / {_reportEpic} · por tarea":$"{_reportProject} / {_reportEpic} / {_reportActivity} · por comentario";
    }
    void DrawPie(IReadOnlyList<ReportSlice> data)
    {
        ReportPie.Children.Clear();var total=data.Sum(x=>x.Hours);if(total<=0){ReportPie.Children.Add(new TextBlock{Text="No hay tiempo registrado en este periodo",Foreground=Brushes.Gray,FontSize=16});return;}
        const double cx=310,cy=215,r=150;double angle=-90;var labels=new List<(ReportSlice Slice,double Mid,bool Right,double Y,Point Edge,Brush Color)>();
        for(var i=0;i<data.Count;i++)
        {
            var slice=data[i];var sweep=slice.Hours/total*360;var mid=angle+sweep/2;Shape shape;
            if(sweep>=359.999){shape=new Ellipse{Width=r*2,Height=r*2};Canvas.SetLeft(shape,cx-r);Canvas.SetTop(shape,cy-r);}
            else{var start=PointOnCircle(cx,cy,r,angle);var end=PointOnCircle(cx,cy,r,angle+sweep);var figure=new PathFigure{StartPoint=new Point(cx,cy),IsClosed=true};figure.Segments.Add(new LineSegment(start,true));figure.Segments.Add(new ArcSegment(end,new Size(r,r),0,sweep>180,SweepDirection.Clockwise,true));shape=new System.Windows.Shapes.Path{Data=new PathGeometry(new[]{figure})};}
            var color=ReportColors[i%ReportColors.Length];shape.Fill=color;shape.Stroke=Brushes.White;shape.StrokeThickness=2;shape.Tag=slice;shape.Cursor=Cursors.Hand;shape.ToolTip=$"{slice.Label}: {slice.HoursText} ({slice.PercentageText})";ReportPie.Children.Add(shape);if(i<14){var edge=PointOnCircle(cx,cy,r,mid);var radians=mid*Math.PI/180;labels.Add((slice,mid,Math.Cos(radians)>=0,cy+(r+24)*Math.Sin(radians),edge,color));}angle+=sweep;
        }
        DrawPieLabels(labels.Where(x=>!x.Right).OrderBy(x=>x.Y),false);DrawPieLabels(labels.Where(x=>x.Right).OrderBy(x=>x.Y),true);
        var hole=new Ellipse{Width=130,Height=130,Fill=Brushes.White,IsHitTestVisible=false};Canvas.SetLeft(hole,cx-65);Canvas.SetTop(hole,cy-65);ReportPie.Children.Add(hole);
        var totalText=new TextBlock{Text=$"Total\n{SessionRow.Format(TimeSpan.FromHours(total))}",TextAlignment=TextAlignment.Center,FontSize=18,FontWeight=FontWeights.SemiBold,Width=120,IsHitTestVisible=false};Canvas.SetLeft(totalText,cx-60);Canvas.SetTop(totalText,cy-28);ReportPie.Children.Add(totalText);
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
        var rows=(ReportGrid.ItemsSource as IEnumerable<ReportSlice>)?.ToList()??new();var builder=new StringBuilder("Concepto\tHoras\tPorcentaje");foreach(var row in rows)builder.AppendLine().Append(row.Label.Replace('\t',' ').Replace('\r',' ').Replace('\n',' ')).Append('\t').Append(row.Hours.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)).Append('\t').Append(row.Percentage.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)).Append('%');try{Clipboard.SetText(builder.ToString());}catch(Exception ex){MessageBox.Show(ex.Message,"No se pudo copiar");}
    }
}
