using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using Drawing=System.Drawing;

namespace CompactCounter {
public sealed class CounterSnapshot {
 public Dictionary<string,int> Counts=new Dictionary<string,int>();public HashSet<string> Complete=new HashSet<string>();
 public List<Entry> Events=new List<Entry>();public string Warning="";public bool Running=true;
}
public sealed class LiteWindow : SurfaceWindow {
 readonly Engine engine;readonly string placementPath;readonly bool preview;
 readonly TextBlock chat=Ui.Label("正在识别当前聊天…",13,Ui.Text),count=Ui.Label("—",36,Ui.Text),unit=Ui.Label("次",12,Ui.Muted),status=Ui.Label("等待会话",10,Ui.Muted);
 readonly Border badge=new Border();readonly ManualResetEvent stop=new ManualResetEvent(false);readonly AutoResetEvent refresh=new AutoResetEvent(false);
 readonly DispatcherTimer renderTimer=new DispatcherTimer(),followTimer=new DispatcherTimer();
 volatile ActiveResult active=new ActiveResult();volatile CounterSnapshot snapshot=new CounterSnapshot();volatile Dictionary<string,string> names=new Dictionary<string,string>();
 DockPlacement placement;HistoryWindow history;MenuWindow menu;bool dragging;IntPtr dragOwner,lastOwner;Drawing.Rectangle lastOwnerBounds;Drawing.Size lastSize;string rendered="";
 public LiteWindow(Engine e):this(e,false){}
 public LiteWindow(Engine e,bool isPreview):base(280,152){
  engine=e;preview=isPreview;placementPath=Path.Combine(Path.GetDirectoryName(e.StatePath),"window.json");placement=DockPlacement.Load(placementPath);Title="压缩计数 · 自动";
  ContentGrid.Margin=new Thickness(18,12,12,15);ContentGrid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(24)});ContentGrid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(32)});ContentGrid.RowDefinitions.Add(new RowDefinition());
  var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)});header.Children.Add(Ui.Label("压缩计数",11,Ui.Muted));
  var more=Ui.Button("···",ShowMenu);more.Padding=new Thickness(0);more.FontSize=17;more.ToolTip="历史记录与选项";System.Windows.Automation.AutomationProperties.SetName(more,"打开计数器菜单");Grid.SetColumn(more,1);header.Children.Add(more);
  var hide=Ui.Button("×",Close);hide.Padding=new Thickness(0);hide.FontSize=17;hide.ToolTip="本次隐藏";System.Windows.Automation.AutomationProperties.SetName(hide,"本次隐藏计数器");Grid.SetColumn(hide,2);header.Children.Add(hide);ContentGrid.Children.Add(header);
  chat.Margin=new Thickness(0,3,5,0);Grid.SetRow(chat,1);ContentGrid.Children.Add(chat);
  var bottom=new Grid();bottom.ColumnDefinitions.Add(new ColumnDefinition());bottom.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});Grid.SetRow(bottom,2);ContentGrid.Children.Add(bottom);
  var number=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};count.FontFamily=new FontFamily("Segoe UI");count.FontWeight=FontWeights.Light;unit.Margin=new Thickness(5,17,0,0);unit.Visibility=Visibility.Collapsed;number.Children.Add(count);number.Children.Add(unit);bottom.Children.Add(number);
  badge.CornerRadius=new CornerRadius(12);badge.Padding=new Thickness(10,5,10,5);badge.Margin=new Thickness(0,13,5,0);badge.VerticalAlignment=VerticalAlignment.Center;badge.Background=Ui.Hover;badge.Child=status;Grid.SetColumn(badge,1);bottom.Children.Add(badge);
  Surface.MouseLeftButtonDown+=(s,a)=>{if(menu!=null&&!menu.IsDisposed){menu.Close();a.Handled=true;return;}if(a.ClickCount==2){ShowHistory();a.Handled=true;return;}if(a.ButtonState==MouseButtonState.Pressed){try{DragMove();}catch(InvalidOperationException){}}};
  Surface.MouseRightButtonUp+=(s,a)=>{ShowMenu();a.Handled=true;};
  SourceInitialized+=(s,a)=>{
   HwndSource.FromHwnd(Handle).AddHook(WindowMessage);
   if(!preview){var windows=ActiveChat.FindWindows();var fg=NativeWindow.GetForegroundWindow();active.Window=windows.Contains(fg)?fg:windows.Count>0?windows[0]:IntPtr.Zero;Follow(true);}
  };
  renderTimer.Interval=TimeSpan.FromMilliseconds(150);renderTimer.Tick+=(s,a)=>Render();followTimer.Interval=TimeSpan.FromMilliseconds(50);followTimer.Tick+=(s,a)=>Follow(false);
  Loaded+=(s,a)=>{if(preview)return;renderTimer.Start();followTimer.Start();var worker=new Thread(WatchChat){IsBackground=true};worker.SetApartmentState(ApartmentState.MTA);worker.Start();new Thread(WatchLogs){IsBackground=true}.Start();};
  Closed+=(s,a)=>{renderTimer.Stop();followTimer.Stop();stop.Set();refresh.Set();if(menu!=null&&!menu.IsDisposed)menu.Close();if(history!=null&&!history.IsDisposed)history.Close();};
 }
 IntPtr WindowMessage(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled){
  if(message==0x231){dragging=true;dragOwner=active.Window;}
  if(message==0x232){Drawing.Rectangle owner,box;if(NativeWindow.Bounds(dragOwner,out owner)&&NativeWindow.Bounds(Handle,out box)){placement=DockPlacement.Capture(owner,box,24);SavePlacement();}dragging=false;Follow(true);}
  return IntPtr.Zero;
 }
 void WatchChat(){try{var index=new TitleIndex(Path.Combine(Directory.GetParent(engine.Data.Root).FullName,"session_index.jsonl"));using(var reader=new ActiveChat(index)){while(!stop.WaitOne(0)){var watch=Stopwatch.StartNew();try{var next=reader.Read();bool changed=next.Id!=active.Id;active=next;names=index.Snapshot();if(changed)refresh.Set();}catch{active=new ActiveResult{Status="正在重新连接 Codex…"};}stop.WaitOne((int)Math.Max(50,450-watch.ElapsedMilliseconds));}}}catch{active=new ActiveResult{Status="暂时无法识别当前聊天"};}}
 void WatchLogs(){while(!stop.WaitOne(0)){try{lock(engine){engine.PrioritySession=active.Id;engine.Poll();var s=new CounterSnapshot{Events=engine.Data.Events.ToList(),Warning=engine.Warning};foreach(var id in engine.Data.Sessions.Keys)s.Counts[id]=engine.Count(id);foreach(var group in engine.Data.Files.Values.Where(x=>x.Session!="").GroupBy(x=>x.Session))if(group.All(x=>x.Complete))s.Complete.Add(group.Key);snapshot=s;}}catch{snapshot=new CounterSnapshot{Running=false,Warning="索引写入失败，请检查计数器状态目录权限。"};return;}WaitHandle.WaitAny(new WaitHandle[]{stop,refresh},engine.Pending>0?100:2000);}}
 void Render(){var a=active;var s=snapshot;string title=a.Id==""?a.Status:a.Title,value="—";int n;bool numeric=false;
  if(!s.Running)value="监控已停止";else if(a.Id!=""&&s.Counts.TryGetValue(a.Id,out n)){numeric=s.Complete.Contains(a.Id);value=numeric?n.ToString():"统计中…";}else if(a.Id!="")value="暂无可读日志";
  string key=title+"|"+value+"|"+s.Warning;if(key!=rendered){chat.Text=title;chat.ToolTip=title;count.Text=value;count.FontSize=numeric?(value.Length>4?25:36):value=="—"?32:13;unit.Visibility=numeric?Visibility.Visible:Visibility.Collapsed;status.Text=numeric?"● 自动跟随":s.Running?"等待会话":"监控停止";status.Foreground=numeric?Ui.Accent:Ui.Muted;badge.Background=numeric?Ui.Tint:Ui.Hover;badge.ToolTip=s.Warning!=""?s.Warning:"拖动卡片调整位置";System.Windows.Automation.AutomationProperties.SetName(count,numeric?"压缩 "+value+" 次":value);rendered=key;}
  if(history!=null&&!history.IsDisposed)history.RefreshData(s,names,a.Id);
 }
 void SavePlacement(){try{placement.Save(placementPath);}catch{chat.ToolTip="位置暂未保存，请检查计数器状态目录权限。";}}
 void Follow(bool force){if(dragging||preview||Handle==IntPtr.Zero)return;var h=active.Window;Drawing.Rectangle owner,box;if(!NativeWindow.Bounds(h,out owner)||!NativeWindow.Bounds(Handle,out box))return;
  if(!force&&h==lastOwner&&owner==lastOwnerBounds&&box.Size==lastSize)return;
  if(h!=lastOwner)NativeWindow.Attach(this,h);
  var area=NativeWindow.WorkArea(h);if(placement==null)placement=DockPlacement.Default(owner,box.Size,area);var point=placement.Position(owner,box.Size,area);
  if(point.X!=box.X||point.Y!=box.Y)NativeWindow.SetWindowPos(Handle,IntPtr.Zero,point.X,point.Y,0,0,0x15);
  lastOwner=h;lastOwnerBounds=owner;lastSize=box.Size;
 }
 void Place(){Drawing.Rectangle owner,box;if(NativeWindow.Bounds(active.Window,out owner)&&NativeWindow.Bounds(Handle,out box)){placement=DockPlacement.Default(owner,box.Size,NativeWindow.WorkArea(active.Window));SavePlacement();Follow(true);}}
 protected override bool HandleSurfaceKey(KeyEventArgs e){if(menu!=null&&!menu.IsDisposed&&menu.HandleMenuKey(e.Key,Keyboard.Modifiers))return true;return base.HandleSurfaceKey(e);}
 void ShowMenu(){if(menu!=null&&!menu.IsDisposed){menu.Close();return;}Activate();menu=new MenuWindow(this,ShowHistory,Place,Close,()=>System.Windows.Forms.Application.Exit());menu.Left=Left+Width-menu.Width-8;menu.Top=Top+40;menu.Show();}
 void ShowHistory(){if(history==null||history.IsDisposed){history=new HistoryWindow();history.Owner=this;}history.RefreshData(snapshot,names,active.Id);history.Show();history.Activate();}
 public void SetPreview(string title,int value){active=new ActiveResult{Id="preview",Title=title};var data=new CounterSnapshot();data.Counts["preview"]=value;data.Complete.Add("preview");snapshot=data;Render();}
}
public sealed class MenuWindow : SurfaceWindow {
 readonly List<Button> items=new List<Button>();int selected=-1;
 public MenuWindow(Window owner,Action history,Action place,Action hide,Action exit):base(224,218){
  Title="计数器菜单";Owner=owner;ShowActivated=false;Surface.CornerRadius=new CornerRadius(13);
  var stack=new StackPanel{Margin=new Thickness(8)};ContentGrid.Children.Add(stack);var label=Ui.Label("计数器",10,Ui.Muted);label.Margin=new Thickness(10,3,0,7);stack.Children.Add(label);Add(stack,"全部会话与历史",history);Add(stack,"重新吸附到 Codex",place);Add(stack,"本次隐藏",hide);stack.Children.Add(Ui.Divider());Add(stack,"退出计数器",exit);
  SourceInitialized+=(s,a)=>{NativeWindow.NoActivate(Handle);HwndSource.FromHwnd(Handle).AddHook(MouseActivation);};
  // Keep focus on the card. Destroying an active cross-process owned popup
  // during WM_ACTIVATE can send the Codex group behind the previous app.
  EventHandler leave=(s,a)=>Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{if(!owner.IsActive&&!IsDisposed&&!IsClosing)Close();}));
  EventHandler moved=(s,a)=>{if(!IsDisposed&&!IsClosing)Close();};
  owner.Deactivated+=leave;owner.LocationChanged+=moved;
  Closed+=(s,a)=>{owner.Deactivated-=leave;owner.LocationChanged-=moved;};
 }
 IntPtr MouseActivation(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;}
 public bool HandleMenuKey(Key key,ModifierKeys modifiers){
  if(key==Key.Escape){Close();return true;}
  if(key==Key.Down||key==Key.Up||key==Key.Tab){int step=key==Key.Up||key==Key.Tab&&(modifiers&ModifierKeys.Shift)!=0?-1:1;selected=selected<0?(step>0?0:items.Count-1):(selected+step+items.Count)%items.Count;for(int i=0;i<items.Count;i++)items[i].Background=i==selected?Ui.Hover:Brushes.Transparent;return true;}
  if(key==Key.Enter||key==Key.Space){if(selected<0)selected=0;items[selected].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));return true;}return false;
 }
 protected override bool HandleSurfaceKey(KeyEventArgs e){return HandleMenuKey(e.Key,Keyboard.Modifiers);}
 void Add(Panel stack,string text,Action action){var b=Ui.Button(text,()=>{Close();action();});b.Height=32;b.HorizontalContentAlignment=HorizontalAlignment.Left;b.Padding=new Thickness(10,4,10,4);b.Focusable=false;items.Add(b);stack.Children.Add(b);}
}
public sealed class HistoryWindow : SurfaceWindow {
 readonly ListBox sessions=new ListBox(),events=new ListBox();readonly TextBox search=new TextBox();readonly TextBlock heading=Ui.Label("全部事件",17,Ui.Text),summary=Ui.Label("",11,Ui.Muted),status=Ui.Label("只读本地记录",11,Ui.Muted),empty=Ui.Label("暂无记录",13,Ui.Muted);
 CounterSnapshot data;Dictionary<string,string> names=new Dictionary<string,string>();string selected="",active="",signature="";bool rebuilding;
 public HistoryWindow():base(890,620){Title="压缩记录 · 全部会话";ShowActivated=true;WindowStartupLocation=WindowStartupLocation.CenterOwner;
  ContentGrid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});ContentGrid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});ContentGrid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(38)});ContentGrid.Children.Add(Header("会话与压缩记录",Close));
  var columns=new Grid{Margin=new Thickness(14,2,18,0)};columns.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(295)});columns.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1)});columns.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetRow(columns,1);ContentGrid.Children.Add(columns);
  var left=new Grid{Margin=new Thickness(0,0,12,0)};left.RowDefinitions.Add(new RowDefinition{Height=new GridLength(42)});left.RowDefinitions.Add(new RowDefinition{Height=new GridLength(36)});left.RowDefinitions.Add(new RowDefinition());columns.Children.Add(left);search.Margin=new Thickness(4,0,4,4);search.Tag="搜索会话或 ID";search.ToolTip="输入会话名称或 ID";System.Windows.Automation.AutomationProperties.SetName(search,"搜索会话名称或 ID");search.TextChanged+=(s,a)=>Rebuild();left.Children.Add(search);summary.Margin=new Thickness(12,0,4,0);Grid.SetRow(summary,1);left.Children.Add(summary);Grid.SetRow(sessions,2);left.Children.Add(sessions);
  sessions.SelectionChanged+=(s,a)=>{if(!rebuilding&&sessions.SelectedItem is ListBoxItem){selected=(string)((ListBoxItem)sessions.SelectedItem).Tag;RebuildEvents();}};
  var line=new Border{Background=Ui.Line};Grid.SetColumn(line,1);columns.Children.Add(line);
  var right=new Grid{Margin=new Thickness(18,0,0,0)};right.RowDefinitions.Add(new RowDefinition{Height=new GridLength(42)});right.RowDefinitions.Add(new RowDefinition{Height=new GridLength(28)});right.RowDefinitions.Add(new RowDefinition());Grid.SetColumn(right,2);columns.Children.Add(right);
  var bar=new Grid();bar.ColumnDefinitions.Add(new ColumnDefinition());bar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});bar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});bar.Children.Add(heading);var all=Ui.Button("全部",()=>{selected="";sessions.SelectedItem=null;RebuildEvents();});Grid.SetColumn(all,1);bar.Children.Add(all);var export=Ui.Button("导出 CSV",()=>{if(data!=null)new ExportWindow(this,VisibleEvents().ToList()).ShowDialog();});export.Background=Ui.Tint;export.Margin=new Thickness(6,0,0,0);Grid.SetColumn(export,2);bar.Children.Add(export);right.Children.Add(bar);
  var hint=Ui.Label("时间按本机时区显示",10,Ui.Muted);Grid.SetRow(hint,1);right.Children.Add(hint);Grid.SetRow(events,2);right.Children.Add(events);empty.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetRow(empty,2);right.Children.Add(empty);
  status.Margin=new Thickness(26,0,20,0);Grid.SetRow(status,2);ContentGrid.Children.Add(status);
 }
 string ChatName(string id){string n;return names.TryGetValue(id,out n)?n:id;}
 public void RefreshData(CounterSnapshot d,Dictionary<string,string> n,string a){data=d;names=n;active=a;int titleHash=17;foreach(var pair in n)titleHash=unchecked(titleHash*31+pair.Key.GetHashCode()+pair.Value.GetHashCode());string sig=d.Events.Count+"|"+d.Counts.Count+"|"+d.Complete.Count+"|"+titleHash+"|"+a+"|"+d.Warning;if(sig!=signature){signature=sig;Rebuild();}}
 string SessionSubtitle(string id){if(id==active)return "当前聊天";var last=data.Events.Where(x=>x.Session==id&&x.Kind=="compacted").OrderByDescending(x=>x.Time).FirstOrDefault();DateTimeOffset date;if(last!=null&&DateTimeOffset.TryParse(last.Time,out date))return "上次压缩 "+date.ToLocalTime().ToString("MM-dd HH:mm");return data.Complete.Contains(id)?"尚无压缩记录":"未索引";}
 void Rebuild(){if(data==null)return;rebuilding=true;sessions.Items.Clear();var ids=names.Keys.Concat(data.Counts.Keys).Distinct().Where(x=>ChatName(x).IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0||x.Contains(search.Text)).OrderByDescending(x=>x==active).ThenBy(ChatName).ToArray();foreach(var id in ids){int n;string value=data.Counts.TryGetValue(id,out n)?data.Complete.Contains(id)?n+" 次":"统计中":"未索引";var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var text=new StackPanel();var name=Ui.Label(ChatName(id),12,Ui.Text);name.ToolTip=ChatName(id);text.Children.Add(name);var sub=Ui.Label(SessionSubtitle(id),10,Ui.Muted);sub.Margin=new Thickness(0,5,8,0);sub.ToolTip=id;text.Children.Add(sub);row.Children.Add(text);var amount=Ui.Label(value,12,Ui.Accent);amount.Margin=new Thickness(8,0,0,0);Grid.SetColumn(amount,1);row.Children.Add(amount);var item=new ListBoxItem{Content=row,Tag=id};System.Windows.Automation.AutomationProperties.SetName(item,ChatName(id)+"，"+value);sessions.Items.Add(item);if(id==selected)item.IsSelected=true;}summary.Text=ids.Length+" 个会话";rebuilding=false;RebuildEvents();status.Text=data.Warning!=""?data.Warning:"本地只读 · 记录自动更新";}
 IEnumerable<Entry> VisibleEvents(){return data.Events.Where(x=>selected==""||x.Session==selected).OrderByDescending(x=>x.Time);}
 public static string Kind(string value){return value=="compacted"?"压缩":value=="context_record"?"上下文记录 · 不计数":"通知 · 不计数";}
 void RebuildEvents(){if(data==null)return;heading.Text=selected==""?"全部事件":ChatName(selected);heading.ToolTip=heading.Text;events.Items.Clear();foreach(var e in VisibleEvents()){var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var text=new StackPanel();DateTimeOffset t;string time=DateTimeOffset.TryParse(e.Time,out t)?t.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"):e.Time;text.Children.Add(Ui.Label(time,13,Ui.Text));var sub=Ui.Label(ChatName(e.Session),11,Ui.Muted);sub.Margin=new Thickness(0,5,8,0);sub.ToolTip=ChatName(e.Session);text.Children.Add(sub);row.Children.Add(text);var kind=Ui.Label(Kind(e.Kind),11,e.Kind=="compacted"?Ui.Accent:Ui.Muted);Grid.SetColumn(kind,1);row.Children.Add(kind);var item=new ListBoxItem{Content=row,ToolTip=e.Time,Focusable=false};events.Items.Add(item);}empty.Visibility=events.Items.Count==0?Visibility.Visible:Visibility.Collapsed;empty.Text=selected==""?"暂无事件记录":"这个会话暂无事件记录";}
}
public sealed class ExportWindow : SurfaceWindow {
 public ExportWindow(Window owner,List<Entry> entries):base(420,260){Title="导出记录";Owner=owner;ShowActivated=true;WindowStartupLocation=WindowStartupLocation.CenterOwner;ContentGrid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});ContentGrid.RowDefinitions.Add(new RowDefinition());ContentGrid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(60)});ContentGrid.Children.Add(Header("导出记录",Close));
  var info=new StackPanel{Margin=new Thickness(24,5,24,12)};info.Children.Add(Ui.Label(entries.Count+" 条事件 · CSV 文件",18,Ui.Text));var detail=Ui.Label("包含会话 ID、事件时间、类型和日志位置。",12,Ui.Muted);detail.TextWrapping=TextWrapping.Wrap;detail.Margin=new Thickness(0,12,0,0);info.Children.Add(detail);Grid.SetRow(info,1);ContentGrid.Children.Add(info);
  var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(18,4,22,18)};var cancel=Ui.Button("取消",Close);cancel.Margin=new Thickness(0,0,8,0);actions.Children.Add(cancel);Button save=null;save=Ui.Button("选择保存位置",()=>{var dialog=new Microsoft.Win32.SaveFileDialog{Filter="CSV 文件|*.csv",FileName="compact-history.csv",AddExtension=true,DefaultExt=".csv"};if(dialog.ShowDialog(this)!=true)return;try{File.WriteAllLines(dialog.FileName,new string[]{"session_id,timestamp_utc,kind,byte_offset"}.Concat(entries.Select(e=>e.Session+","+e.Time+","+e.Kind+","+e.ByteOffset)),new UTF8Encoding(true));detail.Text="已保存："+dialog.FileName;save.Content="已保存";save.IsEnabled=false;cancel.Content="完成";}catch{new NoticeWindow(this,"无法保存","请选择可写入的位置，再试一次。").ShowDialog();}});save.Background=Ui.Tint;actions.Children.Add(save);Grid.SetRow(actions,2);ContentGrid.Children.Add(actions);
 }
}
}
