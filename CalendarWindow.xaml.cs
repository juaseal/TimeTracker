using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
namespace TimeTracker;
public partial class CalendarWindow : Window
{
    readonly TimeRepository _repo; CalendarPeriodRow? _selectedPeriod; ObservableCollection<RecentFieldSetting> _recentFields=new(); int _weekStartDay=1;
    static readonly WeekStartOption[] WeekDays={new(){Value=1,Name="Monday"},new(){Value=2,Name="Tuesday"},new(){Value=3,Name="Wednesday"},new(){Value=4,Name="Thursday"},new(){Value=5,Name="Friday"},new(){Value=6,Name="Saturday"},new(){Value=7,Name="Sunday"}};
    public CalendarWindow(TimeRepository repo){_repo=repo;InitializeComponent();LoadPreferences();LoadCalendar();}
    void LoadPreferences()
    {
        _weekStartDay=_repo.WeekStartDay();WeekStartBox.ItemsSource=WeekDays;WeekStartBox.SelectedValue=_weekStartDay;RecentLimitBox.Text=_repo.RecentLimit().ToString(CultureInfo.InvariantCulture);_recentFields=new ObservableCollection<RecentFieldSetting>(_repo.RecentFieldSettings());RecentFieldsGrid.ItemsSource=_recentFields;
        var appearance=AppearanceManager.Load(_repo);SelectTag(ThemeBox,appearance.Theme);SelectTag(PaletteBox,appearance.Palette);SelectTag(FontScaleBox,appearance.FontScale.ToString("0.##",CultureInfo.InvariantCulture));HighContrastBox.IsChecked=appearance.HighContrast;ReducedMotionBox.IsChecked=appearance.ReducedMotion;StartInWidgetBox.IsChecked=_repo.BoolSetting("start_in_widget");AlwaysOnTopBox.IsChecked=_repo.BoolSetting("always_on_top",true);RoundSapBox.IsChecked=_repo.BoolSetting("sap_rounding_enabled");SelectTag(RoundingMinutesBox,_repo.Setting("sap_rounding_minutes","30"));
    }
    static void SelectTag(ComboBox box,string tag){box.SelectedItem=box.Items.OfType<ComboBoxItem>().FirstOrDefault(x=>string.Equals(x.Tag?.ToString(),tag,StringComparison.OrdinalIgnoreCase))??box.Items[0];}
    static string SelectedTag(ComboBox box,string fallback)=>(box.SelectedItem as ComboBoxItem)?.Tag?.ToString()??fallback;
    IEnumerable<ScheduleDay> InWeekOrder(IEnumerable<ScheduleDay> days)=>days.OrderBy(x=>(7+x.DayOfWeek-_weekStartDay)%7);
    void LoadCalendar(long? selectTemplate=null,long? selectPeriod=null){var currentTemplate=selectTemplate??(TemplateBox.SelectedItem as ScheduleTemplate)?.Id;var currentPeriod=selectPeriod??_selectedPeriod?.Id;var templates=_repo.Templates();var periods=_repo.Periods();TemplateBox.ItemsSource=templates;PeriodTemplateBox.ItemsSource=templates;PeriodsGrid.ItemsSource=periods;TemplateBox.SelectedItem=templates.FirstOrDefault(x=>x.Id==currentTemplate)??templates.FirstOrDefault();if(currentPeriod is not null)PeriodsGrid.SelectedItem=periods.FirstOrDefault(x=>x.Id==currentPeriod);if(PeriodsGrid.SelectedItem is null)PeriodModeText.Text="Select a period or create a new one";}
    void TemplateSelected(object s,SelectionChangedEventArgs e){if(TemplateBox.SelectedItem is not ScheduleTemplate t)return;TemplateModeText.Text=$"Editing: {t.Name}";TemplateNameBox.Text=t.Name;ScheduleDaysGrid.ItemsSource=new ObservableCollection<ScheduleDay>(InWeekOrder(_repo.TemplateDays(t.Id)));}
    ObservableCollection<ScheduleDay> EmptyWeek()=>new(InWeekOrder(WeekDays.Select(x=>new ScheduleDay{DayOfWeek=x.Value,Day=x.Name,Hours=0})));
    void NewTemplateClick(object s,RoutedEventArgs e){TemplateBox.SelectedItem=null;TemplateModeText.Text="Creating a new template";TemplateNameBox.Text="New template";ScheduleDaysGrid.ItemsSource=EmptyWeek();TemplateNameBox.Focus();TemplateNameBox.SelectAll();}
    void SaveTemplateClick(object s,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(TemplateNameBox.Text)||ScheduleDaysGrid.ItemsSource is not IEnumerable<ScheduleDay> days)return;var t=TemplateBox.SelectedItem as ScheduleTemplate??new();t.Name=TemplateNameBox.Text.Trim();try{_repo.SaveTemplate(t,days);LoadCalendar(t.Id);TemplateModeText.Text=$"Saved: {t.Name}";}catch(Exception ex){MessageBox.Show(ex.Message,"Could not save the template");}}
    void DeleteTemplateClick(object s,RoutedEventArgs e){if(TemplateBox.SelectedItem is not ScheduleTemplate t)return;_repo.DeleteTemplate(t.Id);LoadCalendar();}
    void PeriodSelected(object s,SelectionChangedEventArgs e){if(PeriodsGrid.SelectedItem is not CalendarPeriodRow p)return;_selectedPeriod=p;PeriodModeText.Text=$"Editing: {p.Name}";PeriodNameBox.Text=p.Name;PeriodStartBox.Text=p.StartText;PeriodEndBox.Text=p.EndText;PeriodTemplateBox.SelectedItem=(PeriodTemplateBox.ItemsSource as IEnumerable<ScheduleTemplate>)?.FirstOrDefault(x=>x.Id==p.TemplateId);}
    void NewPeriodClick(object s,RoutedEventArgs e){_selectedPeriod=null;PeriodsGrid.SelectedItem=null;PeriodModeText.Text="Creating a new period";PeriodNameBox.Text="New period";PeriodStartBox.Text="01/01";PeriodEndBox.Text="31/12";PeriodTemplateBox.SelectedIndex=0;PeriodNameBox.Focus();PeriodNameBox.SelectAll();}
    void SavePeriodClick(object s,RoutedEventArgs e){if(PeriodTemplateBox.SelectedItem is not ScheduleTemplate t)return;var p=_selectedPeriod??new();p.Name=PeriodNameBox.Text.Trim();p.StartText=PeriodStartBox.Text.Trim();p.EndText=PeriodEndBox.Text.Trim();p.TemplateId=t.Id;try{_repo.SavePeriod(p);_selectedPeriod=p;LoadCalendar(null,p.Id);PeriodModeText.Text=$"Saved: {p.Name}";}catch(Exception ex){MessageBox.Show(ex.Message,"Could not save the period");}}
    void DeletePeriodClick(object s,RoutedEventArgs e){if(_selectedPeriod is null)return;_repo.DeletePeriod(_selectedPeriod.Id);_selectedPeriod=null;LoadCalendar();NewPeriodClick(s,e);}
    void MoveRecentField(int offset){if(RecentFieldsGrid.SelectedItem is not RecentFieldSetting item)return;var index=_recentFields.IndexOf(item);var target=index+offset;if(target<0||target>=_recentFields.Count)return;_recentFields.Move(index,target);RecentFieldsGrid.SelectedItem=item;RecentFieldsGrid.ScrollIntoView(item);}
    void MoveFieldUpClick(object s,RoutedEventArgs e)=>MoveRecentField(-1);
    void MoveFieldDownClick(object s,RoutedEventArgs e)=>MoveRecentField(1);
    void ExportDataClick(object s,RoutedEventArgs e)
    {
        var dialog=new SaveFileDialog{Title="Export history",Filter="CSV file (*.csv)|*.csv",FileName=$"timetracker-{DateTime.Today:yyyyMMdd}.csv",DefaultExt=".csv",AddExtension=true};
        if(dialog.ShowDialog(this)!=true)return;try{_repo.ExportSessionsCsv(dialog.FileName);PreferencesStatusText.Text="History exported";}catch(Exception ex){MessageBox.Show(ex.Message,"Could not export");}
    }
    void BackupDataClick(object s,RoutedEventArgs e)
    {
        var dialog=new SaveFileDialog{Title="Create backup",Filter="SQLite database (*.db)|*.db",FileName=$"timetracker-backup-{DateTime.Today:yyyyMMdd}.db",DefaultExt=".db",AddExtension=true};
        if(dialog.ShowDialog(this)!=true)return;try{_repo.BackupTo(dialog.FileName);PreferencesStatusText.Text="Backup created";}catch(Exception ex){MessageBox.Show(ex.Message,"Could not create backup");}
    }
    void OpenDataFolderClick(object s,RoutedEventArgs e){AppPaths.EnsureDataDirectory();Process.Start(new ProcessStartInfo(AppPaths.DataDirectory){UseShellExecute=true});}
    void AboutClick(object s,RoutedEventArgs e)
    {
        var version=Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)??"1.0.0";
        MessageBox.Show($"TaskUp {version}\n\nYour records are stored locally on this device. The application does not transmit data or use telemetry.\n\nData and diagnostics:\n{AppPaths.DataDirectory}","About TaskUp",MessageBoxButton.OK,MessageBoxImage.Information);
    }
    void SavePreferencesClick(object s,RoutedEventArgs e)
    {
        RecentFieldsGrid.CommitEdit(DataGridEditingUnit.Cell,true);RecentFieldsGrid.CommitEdit(DataGridEditingUnit.Row,true);
        var weekStart=WeekStartBox.SelectedValue is int value?value:1;if(!_recentFields.Any(x=>x.IsVisible)){MessageBox.Show("Keep at least one field visible in Recent.","Settings");return;}
        var recentLimit=int.TryParse(RecentLimitBox.Text,out var parsedLimit)?Math.Clamp(parsedLimit,1,100):20;RecentLimitBox.Text=recentLimit.ToString(CultureInfo.InvariantCulture);_repo.SavePreferences(weekStart,recentLimit,_recentFields);
        var appearance=new AppearancePreferences{Theme=SelectedTag(ThemeBox,"light"),Palette=SelectedTag(PaletteBox,"violet"),FontScale=double.TryParse(SelectedTag(FontScaleBox,"1"),NumberStyles.Float,CultureInfo.InvariantCulture,out var scale)?scale:1,HighContrast=HighContrastBox.IsChecked==true,ReducedMotion=ReducedMotionBox.IsChecked==true};
        _repo.SaveSettings(new Dictionary<string,string>{{"appearance_theme",appearance.Theme},{"appearance_palette",appearance.Palette},{"font_scale",appearance.FontScale.ToString(CultureInfo.InvariantCulture)},{"high_contrast",appearance.HighContrast.ToString()},{"reduced_motion",appearance.ReducedMotion.ToString()},{"start_in_widget",(StartInWidgetBox.IsChecked==true).ToString()},{"always_on_top",(AlwaysOnTopBox.IsChecked==true).ToString()},{"sap_rounding_enabled",(RoundSapBox.IsChecked==true).ToString()},{"sap_rounding_minutes",SelectedTag(RoundingMinutesBox,"30")}});AppearanceManager.Apply(appearance);
        _weekStartDay=weekStart;var selected=(TemplateBox.SelectedItem as ScheduleTemplate)?.Id;LoadCalendar(selected);PreferencesStatusText.Text="Preferences saved";
    }
}
