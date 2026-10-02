using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TimeTracker;

public sealed class HistoryView : UserControl
{
    readonly TimeRepository _repo;
    readonly Action _onSaved;
    readonly TextBox _search=new(){MinWidth=230,Margin=new Thickness(8,0,12,0)};
    readonly DataGrid _grid=new(){AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,SelectionMode=DataGridSelectionMode.Extended,SelectionUnit=DataGridSelectionUnit.CellOrRowHeader,ClipboardCopyMode=DataGridClipboardCopyMode.None};
    readonly TextBlock _status=new(){Margin=new Thickness(0,10,0,0)};
    readonly Stack<List<SessionRow>> _undo=new();
    SessionRow? _editBefore;
    internal DataGrid Grid=>_grid;

    public HistoryView(TimeRepository repo,Action onSaved)
    {
        _repo=repo;_onSaved=onSaved;
        Margin=new Thickness(4,18,4,0);
        var panel=new DockPanel();
        var toolbar=new WrapPanel{Margin=new Thickness(0,0,0,12)};
        toolbar.Children.Add(new TextBlock{Text="Search",VerticalAlignment=VerticalAlignment.Center});
        toolbar.Children.Add(_search);
        var refresh=new Button{Content="Refresh"};
        toolbar.Children.Add(refresh);
        DockPanel.SetDock(toolbar,Dock.Top);panel.Children.Add(toolbar);
        DockPanel.SetDock(_status,Dock.Bottom);panel.Children.Add(_status);
        AddColumn("Day","Start",110,"dd/MM/yyyy");
        AddColumn("Start","Start",80,"HH:mm");
        AddColumn("End day","End",110,"dd/MM/yyyy");
        AddColumn("End","End",80,"HH:mm");
        AddColumn("Project","Project",130);
        AddColumn("Epic","Epic",130);
        AddColumn("Activity","Activity",180);
        AddColumn("Comment","Comment",240);
        AddColumn("Time","Duration",85,readOnly:true);
        panel.Children.Add(_grid);Content=panel;
        _search.ToolTip="Search project, epic, activity, or comment in the complete history. Leave empty to show all.";
        _search.TextChanged+=(_,_)=>Reload();
        refresh.Click+=(_,_)=>Reload();
        _grid.BeginningEdit+=(_,e)=>_editBefore=Copy((SessionRow)e.Row.Item);
        _grid.PreparingCellForEdit+=PreparingCellForEdit;
        _grid.CellEditEnding+=CellEditEnding;
        _grid.PreviewKeyDown+=GridKeyDown;
        Loaded+=(_,_)=>Reload();
    }

    void AddColumn(string title,string property,double width,string? format=null,bool readOnly=false)
    {
        // Commit validated editor text ourselves so invalid input never reaches the model.
        _grid.Columns.Add(new DataGridTextColumn{Header=title,Binding=new Binding(property){Mode=readOnly?BindingMode.OneWay:BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.Explicit,StringFormat=format,TargetNullValue="…",ConverterCulture=CultureInfo.InvariantCulture},Width=width,IsReadOnly=readOnly});
    }

    public void Reload()
    {
        if(!CommitPendingEdit())return;
        var rows=_repo.SearchHistory(_search.Text);
        _grid.ItemsSource=rows;
        UpdateStatus();
    }

    void UpdateStatus()
    {
        var rows=_grid.Items.OfType<SessionRow>().ToList();
        var total=rows.Sum(x=>Math.Max(0,((x.End??DateTime.Now)-x.Start).TotalHours));
        _status.Text=rows.Count==0?"No records match this search.":$"{rows.Count} records · Total {SessionRow.Format(TimeSpan.FromHours(total))} · Enter: edit · Ctrl+C / Ctrl+V: copy / paste · Ctrl+Z: undo";
    }

    void PreparingCellForEdit(object? sender,DataGridPreparingCellForEditEventArgs e)
    {
        if(e.EditingElement is not TextBox text)return;
        // Typing-to-edit has already inserted the first character; leave its caret alone.
        if(e.EditingEventArgs is not TextCompositionEventArgs)text.SelectAll();
        if(e.Column==_grid.Columns[1]||e.Column==_grid.Columns[3])
        {
            text.PreviewMouseLeftButtonDown+=(_,args)=>{text.SelectAll();args.Handled=true;};
            text.PreviewKeyDown+=(_,args)=>{
                if(args.Key is not (Key.Up or Key.Down)||!ReadTime(text.Text,out var time))return;
                time+=TimeSpan.FromMinutes(args.Key==Key.Up?5:-5);
                if(time<TimeSpan.Zero)time=TimeSpan.FromHours(23)+TimeSpan.FromMinutes(55);
                if(time>=TimeSpan.FromDays(1))time=TimeSpan.Zero;
                text.Text=$"{(int)time.TotalHours:00}:{time.Minutes:00}";text.SelectAll();args.Handled=true;
            };
        }
    }

    void CellEditEnding(object? sender,DataGridCellEditEndingEventArgs e)
    {
        if(e.EditAction!=DataGridEditAction.Commit){_editBefore=null;return;}
        if(e.Row.Item is not SessionRow row||e.EditingElement is not TextBox editor)return;
        var before=_editBefore??Copy(row);
        var candidate=Copy(row);
        if(!TrySetCell(candidate,_grid.Columns.IndexOf(e.Column),editor.Text,out var error)||!Valid(candidate,out error))
        {
            e.Cancel=true;
            _status.Text=error;
            editor.SelectAll();
            return;
        }
        if(!Same(before,candidate))
        {
            try{_repo.SaveHistoryBatch(new[]{candidate});}
            catch(Exception ex){e.Cancel=true;_status.Text=$"Could not save: {ex.Message}";return;}
            CopyValues(candidate,row);
            PushUndo(new(){before});
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>{
                UpdateStatus();_onSaved();
            }));
        }
        _editBefore=null;
    }

    bool CommitPendingEdit()=>_grid.CommitEdit(DataGridEditingUnit.Cell,true)&&_grid.CommitEdit(DataGridEditingUnit.Row,true);

    void GridKeyDown(object sender,KeyEventArgs e)
    {
        var ctrl=(Keyboard.Modifiers&ModifierKeys.Control)!=0;
        if(ctrl&&e.Key==Key.Z){e.Handled=true;UndoEdit();return;}
        // Preserve normal text selection/copy/paste while inside the editor.
        if(e.OriginalSource is TextBox)return;
        if(e.Key==Key.Enter&&Keyboard.Modifiers==ModifierKeys.None)
        {
            if(_grid.CurrentCell.Column is {IsReadOnly:false})_grid.BeginEdit();
            e.Handled=true;return;
        }
        if(ctrl&&e.Key==Key.C)
        {
            e.Handled=true;
            if(!CommitPendingEdit())return;
            var focus=_grid.CurrentCell;
            try{Clipboard.SetText(SelectedText());}
            catch(Exception ex){_status.Text=$"Could not copy: {ex.Message}";}
            finally{RestoreCellFocus(focus);}
        }
        else if(ctrl&&e.Key==Key.V)
        {
            e.Handled=true;
            if(!CommitPendingEdit())return;
            try{if(Clipboard.ContainsText())PasteText(Clipboard.GetText());}
            catch(Exception ex){_status.Text=$"Could not paste: {ex.Message}";}
        }
        else if(e.Key==Key.Delete&&Keyboard.Modifiers==ModifierKeys.None)
        {
            e.Handled=true;
            if(CommitPendingEdit())PasteText("");
        }
    }

    internal string SelectedText()
    {
        var cells=_grid.SelectedCells.ToList();
        if(cells.Count==0&&_grid.CurrentCell.IsValid)cells.Add(_grid.CurrentCell);
        var selected=cells.Where(x=>x.Item is SessionRow).ToList();
        var columns=selected.Select(x=>x.Column.DisplayIndex).Distinct().Order().ToList();
        return string.Join("\n",selected.GroupBy(x=>(SessionRow)x.Item).OrderBy(g=>_grid.Items.IndexOf(g.Key)).Select(g=>
            string.Join("\t",columns.Select(index=>{
                var cell=g.FirstOrDefault(x=>x.Column.DisplayIndex==index);
                return cell.IsValid?CellText(g.Key,_grid.Columns.IndexOf(cell.Column)).Replace('\t',' ').Replace('\r',' ').Replace('\n',' '):"";
            }))));
    }

    internal bool PasteText(string text)
    {
        var hadFocus=_grid.IsKeyboardFocusWithin;
        var focus=_grid.CurrentCell;
        if(!CommitPendingEdit())return false;
        var lines=text.Replace("\r\n","\n").Replace('\r','\n').Split('\n').ToList();
        if(lines.Count>1&&lines[^1]=="")lines.RemoveAt(lines.Count-1);
        var values=lines.Select(x=>x.Split('\t')).ToArray();
        var targets=new List<(SessionRow Row,int Column,string Value)>();
        if(values.Length==1&&values[0].Length==1)
        {
            var cells=_grid.SelectedCells.ToList();
            if(cells.Count==0&&_grid.CurrentCell.IsValid)cells.Add(_grid.CurrentCell);
            foreach(var cell in cells)
                if(cell.Item is SessionRow row&&!cell.Column.IsReadOnly)targets.Add((row,_grid.Columns.IndexOf(cell.Column),values[0][0]));
        }
        else
        {
            var current=_grid.CurrentCell;
            if(current.Item is not SessionRow)return false;
            var firstRow=_grid.Items.IndexOf(current.Item);
            var firstColumn=current.Column.DisplayIndex;
            if(firstRow+values.Length>_grid.Items.Count||values.Any(x=>firstColumn+x.Length>_grid.Columns.Count))
            {_status.Text="The pasted range does not fit in the table.";return false;}
            for(var r=0;r<values.Length;r++)
                for(var c=0;c<values[r].Length;c++)
                {
                    var column=_grid.Columns.Single(x=>x.DisplayIndex==firstColumn+c);
                    if(!column.IsReadOnly)targets.Add(((SessionRow)_grid.Items[firstRow+r],_grid.Columns.IndexOf(column),values[r][c]));
                }
        }
        var changes=new Dictionary<SessionRow,SessionRow>();
        foreach(var target in targets)
        {
            if(!changes.TryGetValue(target.Row,out var candidate))changes[target.Row]=candidate=Copy(target.Row);
            if(!TrySetCell(candidate,target.Column,target.Value,out var error)){_status.Text=error;return false;}
        }
        foreach(var candidate in changes.Values)
            if(!Valid(candidate,out var error)){_status.Text=error;return false;}
        var modified=changes.Where(x=>!Same(x.Key,x.Value)).ToList();
        if(modified.Count==0)return true;
        try{_repo.SaveHistoryBatch(modified.Select(x=>x.Value));}
        catch(Exception ex){_status.Text=$"Could not paste: {ex.Message}";return false;}
        PushUndo(modified.Select(x=>Copy(x.Key)).ToList());
        foreach(var pair in modified)CopyValues(pair.Value,pair.Key);
        UpdateStatus();_onSaved();
        if(hadFocus)RestoreCellFocus(focus);
        return true;
    }

    void PushUndo(List<SessionRow> rows)=>_undo.Push(rows);

    internal void UndoEdit()
    {
        var hadFocus=_grid.IsKeyboardFocusWithin;
        var focus=_grid.CurrentCell;
        if(!CommitPendingEdit()||!_undo.TryPeek(out var previous))return;
        try
        {
            _repo.SaveHistoryBatch(previous);_undo.Pop();
            foreach(var row in _grid.Items.OfType<SessionRow>())
                if(previous.FirstOrDefault(x=>x.Id==row.Id) is { } saved)CopyValues(saved,row);
            UpdateStatus();_onSaved();
            if(hadFocus)RestoreCellFocus(focus);
        }
        catch(Exception ex){_status.Text=$"Could not undo: {ex.Message}";}
    }

    void RestoreCellFocus(DataGridCellInfo focus)
    {
        if(!focus.IsValid||!_grid.Items.Contains(focus.Item))return;
        _grid.CurrentCell=focus;
        _grid.ScrollIntoView(focus.Item,focus.Column);
        _grid.UpdateLayout();
        if(_grid.ItemContainerGenerator.ContainerFromItem(focus.Item) is DataGridRow container
            &&MainWindow.FindCell(container,focus.Column) is DataGridCell cell)
        {
            cell.Focus();Keyboard.Focus(cell);
        }
    }
    static string CellText(SessionRow row,int column)=>column switch
    {
        0=>row.Start.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture),
        1=>row.StartText,
        2=>row.End?.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture)??"…",
        3=>row.EndText,4=>row.Project,5=>row.Epic,6=>row.Activity,7=>row.Comment,_=>row.Duration
    };

    internal static bool TrySetCell(SessionRow row,int column,string value,out string error)
    {
        error="";
        if(column is 0 or 2)
        {
            if(!DateTime.TryParseExact(value.Trim(),"dd/MM/yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))
            {
                if(column==2&&row.End is null&&value.Trim() is "" or "…")return true;
                error="Enter a date as dd/MM/yyyy.";return false;
            }
            if(column==0)
            {
                var offset=date-row.Start.Date;
                row.Start=date+row.Start.TimeOfDay;
                if(row.End is not null)row.End=row.End.Value.Add(offset);
            }
            else
            {
                if(row.End is null){error="Set an end time before changing the end day.";return false;}
                row.End=date+row.End.Value.TimeOfDay;
            }
        }
        else if(column is 1 or 3)
        {
            if(column==3&&row.End is null&&value.Trim() is "" or "…")return true;
            if(!ReadTime(value,out var time)){error="Enter a time such as 1020, 10:20, or 10.20.";return false;}
            if(column==1)row.Start=row.Start.Date+time;
            else row.End=(row.End?.Date??row.Start.Date)+time;
        }
        else switch(column)
        {
            case 4:row.Project=value;break;
            case 5:row.Epic=value;break;
            case 6:row.Activity=value;break;
            case 7:row.Comment=value;break;
        }
        return true;
    }

    static bool ReadTime(string value,out TimeSpan time)
    {
        var text=value.Trim().Replace('.',':');
        if(text.Length is 3 or 4&&text.All(char.IsDigit))text=text.Insert(text.Length-2,":");
        return TimeSpan.TryParse(text,CultureInfo.InvariantCulture,out time)&&time>=TimeSpan.Zero&&time<TimeSpan.FromDays(1);
    }

    static bool Valid(SessionRow row,out string error)
    {
        error="";
        if(row.End is not null&&row.End<=row.Start)error="The start must be earlier than the end. No changes were saved.";
        else if(row.End is null&&row.Start>DateTime.Now)error="A running record cannot start in the future.";
        return error.Length==0;
    }
    static SessionRow Copy(SessionRow s)=>new(){Id=s.Id,Start=s.Start,End=s.End,Project=s.Project,Epic=s.Epic,Activity=s.Activity,Comment=s.Comment};
    static void CopyValues(SessionRow source,SessionRow target){target.Start=source.Start;target.End=source.End;target.Project=source.Project;target.Epic=source.Epic;target.Activity=source.Activity;target.Comment=source.Comment;}
    static bool Same(SessionRow a,SessionRow b)=>a.Start==b.Start&&a.End==b.End&&a.Project==b.Project&&a.Epic==b.Epic&&a.Activity==b.Activity&&a.Comment==b.Comment;
}