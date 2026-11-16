using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;

namespace CompactCounter {
public sealed class CounterSnapshot {
 public Dictionary<string,int> Counts=new Dictionary<string,int>();public HashSet<string> Complete=new HashSet<string>();
 public List<Entry> Events=new List<Entry>();public string Warning="";public bool Running=true;
}
public sealed class LiteWindow : Form {
 protected override bool ShowWithoutActivation {get{return true;}}
 readonly Engine engine;readonly Label chat=new Label(),count=new Label();readonly ToolTip tooltip=new ToolTip();
 readonly ManualResetEvent stop=new ManualResetEvent(false);readonly AutoResetEvent refresh=new AutoResetEvent(false);
 readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
 volatile ActiveResult active=new ActiveResult();volatile CounterSnapshot snapshot=new CounterSnapshot();
 volatile Dictionary<string,string> names=new Dictionary<string,string>();HistoryWindow history;bool placed;string rendered="";
 public LiteWindow(Engine e){
  engine=e;Text="压缩计数 · 自动";ClientSize=new Size(224,69);FormBorderStyle=FormBorderStyle.FixedSingle;
  TopMost=true;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=true;StartPosition=FormStartPosition.Manual;
  BackColor=Color.FromArgb(248,250,249);Font=new Font("Microsoft YaHei UI",9);AutoScaleMode=AutoScaleMode.Dpi;
  chat.SetBounds(12,8,200,22);chat.AutoEllipsis=true;chat.Text="正在识别当前聊天…";
  count.SetBounds(12,31,200,31);count.Font=new Font("Microsoft YaHei UI",16,FontStyle.Bold);count.ForeColor=Color.FromArgb(25,82,65);count.Text="—";
  Controls.AddRange(new Control[]{chat,count});var menu=new ContextMenuStrip();menu.Items.Add("全部会话与历史",null,(s,a)=>ShowHistory());menu.Items.Add("靠近 Codex",null,(s,a)=>Place());menu.Items.Add("本次隐藏",null,(s,a)=>Close());menu.Items.Add("退出自动计数器",null,(s,a)=>Application.Exit());ContextMenuStrip=menu;chat.ContextMenuStrip=menu;count.ContextMenuStrip=menu;
  chat.DoubleClick+=(s,a)=>ShowHistory();count.DoubleClick+=(s,a)=>ShowHistory();
  var area=Screen.PrimaryScreen.WorkingArea;Location=new Point(area.Right-Width-16,area.Top+80);
  timer.Interval=150;timer.Tick+=(s,a)=>Render();
  Shown+=(s,a)=>{timer.Start();var watch=new Thread(WatchChat){IsBackground=true,Name="Current sidebar item"};watch.SetApartmentState(ApartmentState.MTA);watch.Start();new Thread(WatchLogs){IsBackground=true,Name="Read-only compaction index"}.Start();};
  FormClosed+=(s,a)=>{timer.Stop();stop.Set();refresh.Set();if(history!=null)history.Dispose();tooltip.Dispose();};
 }
 void WatchChat(){
  try{var index=new TitleIndex(Path.Combine(Directory.GetParent(engine.Data.Root).FullName,"session_index.jsonl"));using(var reader=new ActiveChat(index)){
   while(!stop.WaitOne(0)){
    var start=Stopwatch.StartNew();try {var next=reader.Read();bool changed=next.Id!=active.Id;active=next;names=index.Snapshot();if(changed)refresh.Set();}catch{active=new ActiveResult {Status="正在重新连接 Codex…"};}
    stop.WaitOne((int)Math.Max(50,450-start.ElapsedMilliseconds));
   }
  }}catch{active=new ActiveResult {Status="暂时无法识别当前聊天"};}
 }
 void WatchLogs(){
  while(!stop.WaitOne(0)){
   try{
    // A quick close/reopen can overlap worker shutdown. Share one engine and
    // serialize polling so state is never overwritten by an older instance.
    lock(engine){
    engine.PrioritySession=active.Id;engine.Poll();
    var s=new CounterSnapshot {Events=engine.Data.Events.ToList(),Warning=engine.Warning};
    foreach(var id in engine.Data.Sessions.Keys)s.Counts[id]=engine.Count(id);
    foreach(var group in engine.Data.Files.Values.Where(x=>x.Session!="").GroupBy(x=>x.Session))if(group.All(x=>x.Complete))s.Complete.Add(group.Key);
    snapshot=s;
    }
   }catch{snapshot=new CounterSnapshot {Running=false,Warning="索引写入失败，监控已停止。关闭后检查计数器状态目录权限。"};return;}
   WaitHandle.WaitAny(new WaitHandle[]{stop,refresh},engine.Pending>0?100:2000);
  }
 }
 void Render(){
  var a=active;var s=snapshot;string title=a.Id==""?a.Status:a.Title;string value="—";int n;
  if(!s.Running)value="监控已停止";
  else if(a.Id!=""&&s.Counts.TryGetValue(a.Id,out n))value=s.Complete.Contains(a.Id)?"压缩 "+n+" 次":"统计中…";
  else if(a.Id!="")value="暂无可读日志";
  string key=title+"|"+value+"|"+s.Warning;
  if(key!=rendered){chat.Text=title;count.Text=value;var old=count.Font;count.Font=new Font(Font.FontFamily,value.Length>8?11:16,FontStyle.Bold);old.Dispose();tooltip.SetToolTip(chat,a.Id);tooltip.SetToolTip(count,s.Warning!=""?s.Warning:"自动跟随当前聊天 · 右键查看历史");rendered=key;}
  if(!placed&&a.Window!=IntPtr.Zero){Place();placed=true;}
  if(history!=null&&!history.IsDisposed&&history.Visible)history.RefreshData(s,names,a.Id);
 }
 void Place(){var h=active.Window;ActiveChat.Rect r;if(h!=IntPtr.Zero&&ActiveChat.GetWindowRect(h,out r)){var area=Screen.FromHandle(h).WorkingArea;Location=new Point(Math.Max(area.Left,Math.Min(r.Right+8,area.Right-Width)),Math.Max(area.Top,Math.Min(r.Top+72,area.Bottom-Height)));}}
 void ShowHistory(){if(history==null||history.IsDisposed)history=new HistoryWindow();history.RefreshData(snapshot,names,active.Id);history.Show();history.Activate();}
}
public sealed class HistoryWindow : Form {
 readonly ListView sessions=new ListView(),events=new ListView();readonly TextBox search=new TextBox();readonly Label status=new Label();
 CounterSnapshot data;Dictionary<string,string> names;string active="",selected="";bool rendering;string signature="";
 public HistoryWindow(){
  Text="压缩记录 · 全部会话";Font=new Font("Microsoft YaHei UI",10);BackColor=Color.White;Size=new Size(960,700);MinimumSize=new Size(700,500);StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;
  var root=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=6};
  root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));root.RowStyles.Add(new RowStyle(SizeType.Percent,55));root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));root.RowStyles.Add(new RowStyle(SizeType.Percent,45));root.RowStyles.Add(new RowStyle(SizeType.Absolute,32));Controls.Add(root);
  root.Controls.Add(new Label {Text="全部会话",Font=new Font(Font.FontFamily,20,FontStyle.Bold),AutoSize=true},0,0);
  var bar=new FlowLayoutPanel{Dock=DockStyle.Fill};bar.Controls.Add(new Label {Text="查找聊天",AutoSize=true,Margin=new Padding(0,6,8,0)});search.Width=280;search.TextChanged+=(s,a)=>Rebuild();bar.Controls.Add(search);var all=new Button {Text="全部事件",AutoSize=true};all.Click+=(s,a)=>{selected="";RebuildEvents();};bar.Controls.Add(all);root.Controls.Add(bar,0,1);
  Setup(sessions,new string[]{"聊天","压缩次数","会话 ID"},new int[]{280,110,360});sessions.SelectedIndexChanged+=(s,a)=>{if(!rendering&&sessions.SelectedItems.Count>0){selected=(string)sessions.SelectedItems[0].Tag;RebuildEvents();}};root.Controls.Add(sessions,0,2);
  var eventBar=new FlowLayoutPanel{Dock=DockStyle.Fill};eventBar.Controls.Add(new Label {Text="事件历史",AutoSize=true,Margin=new Padding(0,8,16,0)});var export=new Button {Text="导出 CSV",AutoSize=true};export.Click+=(s,a)=>Export();eventBar.Controls.Add(export);eventBar.Controls.Add(new Label {Text="时间按本机时区显示",AutoSize=true,Margin=new Padding(12,8,0,0)});root.Controls.Add(eventBar,0,3);
  Setup(events,new string[]{"事件时间","记录类型","聊天"},new int[]{220,200,330});root.Controls.Add(events,0,4);status.Dock=DockStyle.Fill;root.Controls.Add(status,0,5);
 }
 static void Setup(ListView v,string[] labels,int[] widths){v.View=View.Details;v.Dock=DockStyle.Fill;v.FullRowSelect=true;v.HideSelection=false;v.MultiSelect=false;for(int i=0;i<labels.Length;i++)v.Columns.Add(labels[i],widths[i]);}
 string ChatName(string id){string name;return names.TryGetValue(id,out name)?name:id;}
 public void RefreshData(CounterSnapshot d,Dictionary<string,string> n,string a){data=d;names=n;active=a;string sig=d.Events.Count+"|"+d.Complete.Count+"|"+n.Count+"|"+a+"|"+d.Warning;if(sig!=signature){signature=sig;Rebuild();}}
 void Rebuild(){if(data==null)return;rendering=true;sessions.BeginUpdate();sessions.Items.Clear();foreach(var id in names.Keys.Concat(data.Counts.Keys).Distinct().Where(x=>ChatName(x).IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0||x.Contains(search.Text)).OrderByDescending(x=>x==active).ThenBy(ChatName)){
   int c;string v=data.Counts.TryGetValue(id,out c)?data.Complete.Contains(id)?c.ToString():"统计中":"未索引";var row=new ListViewItem(new string[]{ChatName(id),v,id}){Tag=id};sessions.Items.Add(row);if(id==selected)row.Selected=true;
  }sessions.EndUpdate();rendering=false;RebuildEvents();status.Text=data.Warning!=""?data.Warning:"只读监控中 · 关闭此面板后，小窗继续自动跟随";}
 IEnumerable<Entry> VisibleEvents(){return data.Events.Where(x=>selected==""||x.Session==selected).OrderByDescending(x=>x.Time);}
 static string Kind(string k){return k=="compacted"?"压缩（计数）":k=="context_record"?"上下文记录（不计数）":"通知（不计数）";}
 void RebuildEvents(){if(data==null)return;events.BeginUpdate();events.Items.Clear();foreach(var e in VisibleEvents()){DateTimeOffset t;string time=DateTimeOffset.TryParse(e.Time,out t)?t.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"):e.Time;events.Items.Add(new ListViewItem(new string[]{time,Kind(e.Kind),ChatName(e.Session)}));}events.EndUpdate();}
 void Export(){using(var d=new SaveFileDialog {Filter="CSV 文件|*.csv",FileName="compact-history.csv"})if(d.ShowDialog(this)==DialogResult.OK){try{File.WriteAllLines(d.FileName,new string[]{"session_id,timestamp_utc,kind,byte_offset"}.Concat(VisibleEvents().Select(e=>e.Session+","+e.Time+","+e.Kind+","+e.ByteOffset)),new UTF8Encoding(true));}catch{MessageBox.Show(this,"无法保存，请选择可写入的位置。");}}}
}
public static class Program {
 [STAThread]public static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  string home=Environment.GetEnvironmentVariable("CODEX_HOME");if(String.IsNullOrEmpty(home))home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
  string root=Path.Combine(home,"sessions"),data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexCompactCounter");bool scan=false;
  for(int i=0;i<args.Length;i++){if(args[i]=="--sessions"&&i+1<args.Length)root=args[++i];else if(args[i]=="--data"&&i+1<args.Length)data=args[++i];else if(args[i]=="--scan-once")scan=true;else if(args[i]=="--watch"){}else if(args[i]=="--bind"&&i+1<args.Length)i++;else{MessageBox.Show("参数：--sessions 日志目录 --data 状态目录 --scan-once","压缩计数器");return 2;}}
  try {
   root=Path.GetFullPath(root);data=Path.GetFullPath(data);
   foreach(string forbidden in new string[]{root,home,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex")}){string prefix=Path.GetFullPath(forbidden).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if((data.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar).StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("状态目录必须位于 Codex 日志和配置目录之外。");}
   string state=Path.Combine(data,"state.json"),name;using(var sha=System.Security.Cryptography.SHA256.Create())name="Local\\CodexCompactCounter-"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(state.ToLowerInvariant()))).Replace("-","");
   bool first;using(var mutex=new Mutex(true,name,out first)){
    if(!first){if(!scan)MessageBox.Show("计数器已在运行，请查看桌面上的小窗。","压缩计数器");return 3;}
    if(scan){var engine=new Engine(root,state);int passes=0;do{engine.Poll();passes++;}while(engine.Pending>0&&passes<1000);Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"scan-result.txt"),"sessions="+engine.Data.Sessions.Count+"\r\ncompacted="+engine.Data.Events.Count(x=>x.Kind=="compacted")+"\r\npending="+engine.Pending+"\r\nwarning="+engine.Warning);return engine.Pending>0||engine.Warning!=""?1:0;}
    Application.Run(new Lifecycle(root,state));mutex.ReleaseMutex();
   }
  }catch(Exception e){if(!scan)MessageBox.Show("无法启动："+e.Message,"压缩计数器");return 1;}
  return 0;
 }
}
}
