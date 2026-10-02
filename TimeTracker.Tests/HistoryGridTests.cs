using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TimeTracker;

static class HistoryGridTests
{
    public static void Verify()
    {
        Exception? failure=null;
        var thread=new Thread(()=>{
            var directory=Path.Combine(Path.GetTempPath(),"TaskUp.HistoryTests",Guid.NewGuid().ToString("N"));
            try
            {
                var repo=new TimeRepository(Path.Combine(directory,"test.db"));
                var day=new DateTime(2024,1,2);
                var first=new SessionRow{Start=day.AddHours(23),End=day.AddDays(1).AddHours(1),Project="P",Epic="E",Activity="A",Comment="One"};
                var second=new SessionRow{Start=day.AddDays(-1).AddHours(9),End=day.AddDays(-1).AddHours(10),Project="P",Epic="E",Activity="B",Comment="Two"};
                repo.Save(first);repo.Save(second);
                var view=new HistoryView(repo,()=>{});view.Reload();
                var grid=view.Grid;
                grid.Measure(new Size(1000,400));grid.Arrange(new Rect(0,0,1000,400));grid.UpdateLayout();
                var rows=grid.Items.OfType<SessionRow>().ToList();
                void Select(params (int Row,int Col)[] cells)
                {
                    grid.CurrentCell=new DataGridCellInfo(rows[cells[0].Row],grid.Columns[cells[0].Col]);
                    grid.SelectedCells.Clear();
                    foreach(var cell in cells)grid.SelectedCells.Add(new DataGridCellInfo(rows[cell.Row],grid.Columns[cell.Col]));
                    grid.UpdateLayout();
                }
                void Check(bool condition,string message){if(!condition)throw new Exception(message);}
                Select((0,7),(1,7));
                Check(grid.SelectedCells.Count==2,"Selected count "+grid.SelectedCells.Count);
                Check(view.PasteText("Shared"),"Scalar paste failed");
                Check(repo.SearchHistory().All(x=>x.Comment=="Shared"),"Paste did not update all selected cells: "+string.Join(",",repo.SearchHistory().Select(x=>x.Comment)));
                view.UndoEdit();
                Check(repo.SearchHistory()[0].Comment=="One"&&repo.SearchHistory()[1].Comment=="Two","Group undo failed");
                Select((0,6));
                Check(view.PasteText("Task 1\tComment 1\r\nTask 2\tComment 2\r\n"),"Range paste failed");
                Check(repo.SearchHistory()[0].Activity=="Task 1"&&repo.SearchHistory()[1].Comment=="Comment 2","Range paste did not persist");
                view.UndoEdit();
                Select((0,1),(1,1));
                Check(!view.PasteText("12:00"),"Invalid interval accepted");
                Check(repo.SearchHistory()[0].Start.Hour==23&&repo.SearchHistory()[1].Start.Hour==9,"Invalid paste saved partial changes");
                Select((0,3));Check(view.PasteText("0130"),"End time paste failed");
                Check(repo.SearchHistory()[0].End==day.AddDays(1).AddHours(1.5),"Overnight end day changed");
                view.UndoEdit();
                Select((0,0));Check(view.PasteText("05/01/2024"),"Date paste failed");
                Check(repo.SearchHistory()[0].Start.Date==new DateTime(2024,1,5)&&repo.SearchHistory()[0].End!.Value.Date==new DateTime(2024,1,6),"Changing day lost overnight interval");
                view.UndoEdit();
                Select((0,6),(0,7),(1,6),(1,7));
                Check(view.SelectedText()=="A\tOne\nB\tTwo","Copy range is not TSV");
                Select((0,7));
                Check(view.PasteText(""),"Clearing a comment failed");
                Check(repo.SearchHistory()[0].Comment=="","Empty clipboard value lost");
                view.UndoEdit();
                var missing=new SessionRow{Id=long.MaxValue,Project="Missing",Epic="E",Activity="A",Start=day};
                var changed=repo.SearchHistory()[0];changed.Comment="Should roll back";
                try{repo.SaveHistoryBatch(new[]{changed,missing});throw new Exception("Missing record accepted");}
                catch(InvalidOperationException){}
                Check(repo.SearchHistory()[0].Comment=="One","Failed batch did not roll back");

                // Exercise actual WPF editor events without opening a visible window.
                grid.Measure(new Size(1000,400));grid.Arrange(new Rect(0,0,1000,400));grid.UpdateLayout();
                Select((0,7));
                Check(grid.BeginEdit(),"Could not begin inline editing");
                Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
                var editor=grid.Columns[7].GetCellContent(rows[0]) as TextBox;
                Check(editor is not null&&editor.SelectedText=="One","Editor did not select all text");
                editor!.Text="Inline";
                Check(grid.CommitEdit(DataGridEditingUnit.Cell,true),"Inline commit failed");
                Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
                Check(repo.SearchHistory()[0].Comment=="Inline","Inline edit did not persist");
                view.UndoEdit();
                Check(repo.SearchHistory()[0].Comment=="One","Inline undo failed");
                Select((1,1));Check(grid.BeginEdit(),"Could not edit start time");
                var timeEditor=grid.Columns[1].GetCellContent(rows[1]) as TextBox;
                Check(timeEditor is not null,"Missing time editor");
                timeEditor!.Text="99:99";
                Check(!grid.CommitEdit(DataGridEditingUnit.Cell,true),"Invalid inline time committed");
                Check(repo.SearchHistory()[1].Start.Hour==9,"Invalid inline time persisted");
                grid.CancelEdit(DataGridEditingUnit.Cell);grid.CancelEdit(DataGridEditingUnit.Row);
                Select((0,7));Check(grid.BeginEdit(),"Could not begin cancellable edit");
                var cancelled=grid.Columns[7].GetCellContent(rows[0]) as TextBox;
                cancelled!.Text="Cancelled";
                grid.CancelEdit(DataGridEditingUnit.Cell);grid.CancelEdit(DataGridEditingUnit.Row);
                Check(repo.SearchHistory()[0].Comment=="One"&&rows[0].Comment=="One","Cancelled edit changed a record");
                // Typing-to-edit must preserve the first character after queued work runs.
                Select((0,7));
                var composition=new TextCompositionEventArgs(Keyboard.PrimaryDevice,new TextComposition(InputManager.Current,grid,"x")){RoutedEvent=TextCompositionManager.TextInputEvent};
                Check(grid.BeginEdit(composition),"Could not start typing-to-edit");
                Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
                var typing=grid.Columns[7].GetCellContent(rows[0]) as TextBox;
                Check(typing is not null&&typing.Text=="x"&&typing.SelectionLength==0&&typing.CaretIndex==1,"First typed character was selected");
                typing!.SelectedText="y";
                Check(typing.Text=="xy","Second character overwrote the first");
                grid.CancelEdit(DataGridEditingUnit.Cell);grid.CancelEdit(DataGridEditingUnit.Row);

                // A delayed save callback must not commit or replace the next cell's editor.
                Select((0,7));Check(grid.BeginEdit(),"Could not edit first cell");
                var previousEditor=(TextBox)grid.Columns[7].GetCellContent(rows[0]);
                previousEditor.Text="Changed";
                Check(grid.CommitEdit(DataGridEditingUnit.Cell,true),"Could not save first cell");
                grid.CommitEdit(DataGridEditingUnit.Row,true);
                Select((0,6));Check(grid.BeginEdit(),"Could not edit next cell");
                var nextEditor=(TextBox)grid.Columns[6].GetCellContent(rows[0]);
                nextEditor.Text="Still typing";
                Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
                Check(ReferenceEquals(nextEditor,grid.Columns[6].GetCellContent(rows[0]))&&nextEditor.Text=="Still typing","Saving previous cell interrupted next edit");
                grid.CancelEdit(DataGridEditingUnit.Cell);grid.CancelEdit(DataGridEditingUnit.Row);
                view.UndoEdit();

                // Copying between columns and pasting must keep the current cell and containers.
                Select((0,6));var copied=view.SelectedText();
                Select((1,7));
                var targetCell=grid.CurrentCell;
                var targetRow=grid.ItemContainerGenerator.ContainerFromItem(rows[1]);
                Check(view.PasteText(copied),"Cross-column paste failed");
                Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
                Check(grid.CurrentCell==targetCell&&grid.SelectedCells.Contains(targetCell),"Paste lost the target cell selection");
                Check(ReferenceEquals(targetRow,grid.ItemContainerGenerator.ContainerFromItem(rows[1])),"Paste rebuilt the focused row");
                Check(repo.SearchHistory()[1].Comment=="A","Cross-column paste changed the wrong field");
                view.UndoEdit();
            }
            catch(Exception ex){failure=ex;}
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if(Directory.Exists(directory))Directory.Delete(directory,true);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(failure is not null)throw new Exception(failure.ToString(),failure);
    }
}
