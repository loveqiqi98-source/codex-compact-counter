using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace CompactCounter {
// Native UI Automation supports AriaProperties; the older managed .NET wrapper
// does not. Only current=page controls are queried. No document/text/value API.
[ComImport,Guid("FF48DBA4-60EF-4201-AA87-54103EEF594E"),ClassInterface(ClassInterfaceType.None)]class CUIAutomation {}
[ComImport,Guid("30CBE57D-D9D0-452A-AB13-7AC5AC4825EE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IUIAutomation {
 void CompareElements();void CompareRuntimeIds();void GetRootElement();UIElement ElementFromHandle(IntPtr hwnd);
 void ElementFromPoint();void GetFocusedElement();void GetRootElementBuildCache();void ElementFromHandleBuildCache();void ElementFromPointBuildCache();void GetFocusedElementBuildCache();void CreateTreeWalker();void get_ControlViewWalker();void get_ContentViewWalker();void get_RawViewWalker();void get_RawViewCondition();void get_ControlViewCondition();void get_ContentViewCondition();void CreateCacheRequest();void CreateTrueCondition();void CreateFalseCondition();
 [return:MarshalAs(UnmanagedType.Interface)]object CreatePropertyCondition(int propertyId,[MarshalAs(UnmanagedType.Struct)]object value);
 [return:MarshalAs(UnmanagedType.Interface)]object CreatePropertyConditionEx(int propertyId,[MarshalAs(UnmanagedType.Struct)]object value,int flags);
}
[ComImport,Guid("D22108AA-8AC5-49A5-837B-37BBB3D7591E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface UIElement {
 void SetFocus();void GetRuntimeId();UIElement FindFirst(int scope,[MarshalAs(UnmanagedType.Interface)]object condition);UIElements FindAll(int scope,[MarshalAs(UnmanagedType.Interface)]object condition);
 void FindFirstBuildCache();void FindAllBuildCache();void BuildUpdatedCache();[return:MarshalAs(UnmanagedType.Struct)]object GetCurrentPropertyValue(int propertyId);
}
[ComImport,Guid("14314595-B4BC-4055-95F2-58F2E42C9855"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface UIElements {int Length {get;}UIElement GetElement(int index);}
public sealed class ActiveResult {public string Id="",Title="",Status="等待打开聊天";public IntPtr Window;}
public sealed class TitleIndex {
 readonly string path;long length=-1;DateTime changed;readonly Dictionary<string,string> names=new Dictionary<string,string>();
 public TitleIndex(string indexPath){path=indexPath;}
 public string Title(string id){string n;return names.TryGetValue(id,out n)?n:id;}
 public Dictionary<string,string> Snapshot(){return new Dictionary<string,string>(names);}
 public void Refresh(){
  var f=new FileInfo(path);if(!f.Exists)return;if(f.Length==length&&f.LastWriteTimeUtc==changed)return;
  var latest=new Dictionary<string,string>();var json=new JavaScriptSerializer();
  using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream,Encoding.UTF8)) {
   string line;while((line=reader.ReadLine())!=null){try{var row=json.Deserialize<Dictionary<string,object>>(line);object id,name;if(row!=null&&row.TryGetValue("id",out id)&&row.TryGetValue("thread_name",out name)){string key=Engine.ValidId(id as string);if(key!=""&&name is string)latest[key]=(string)name;}}catch(ArgumentException){} }
  }
  names.Clear();foreach(var kv in latest)names[kv.Key]=kv.Value;length=f.Length;changed=f.LastWriteTimeUtc;
 }
 public string[] Resolve(string title){return names.Where(x=>x.Value==title).Select(x=>x.Key).ToArray();}
}
public sealed class ActiveChat : IDisposable {
 readonly IUIAutomation automation=(IUIAutomation)new CUIAutomation();readonly object currentCondition;
 readonly TitleIndex titles;IntPtr last;
 public ActiveChat(TitleIndex index){titles=index;currentCondition=automation.CreatePropertyConditionEx(30102,"current=page",2);}
 [StructLayout(LayoutKind.Sequential)]public struct Rect{public int Left,Top,Right,Bottom;}
 delegate bool EnumProc(IntPtr h,IntPtr p);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc cb,IntPtr p);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
 [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr h,uint flags);
 [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr h,int attr,out int value,int size);
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowLong(IntPtr h,int index);
 [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr h,uint command);
 public static List<IntPtr> FindWindows(){
  var ids=new HashSet<int>();foreach(var p in Process.GetProcessesByName("ChatGPT"))using(p)ids.Add(p.Id);
  foreach(var p in Process.GetProcessesByName("Codex"))using(p)ids.Add(p.Id);
  var windows=new List<IntPtr>();EnumWindows((h,p)=>{uint pid;GetWindowThreadProcessId(h,out pid);Rect r;int cloaked;if(ids.Contains((int)pid)&&IsWindowVisible(h)&&!IsIconic(h)&&(GetWindowLong(h,-20)&0x80)==0&&GetWindow(h,4)==IntPtr.Zero&&MonitorFromWindow(h,0)!=IntPtr.Zero&&DwmGetWindowAttribute(h,14,out cloaked,4)==0&&cloaked==0&&GetWindowRect(h,out r)&&r.Right-r.Left>=500&&r.Bottom-r.Top>=300){var caption=new StringBuilder(256);GetWindowText(h,caption,256);if(caption.ToString()=="ChatGPT"||caption.ToString()=="Codex")windows.Add(h);}return true;},IntPtr.Zero);
  return windows;
 }
 public ActiveResult Read(){
  titles.Refresh();var windows=FindWindows();
  IntPtr fg=GetForegroundWindow();IntPtr hwin=windows.Contains(fg)?fg:windows.Contains(last)?last:windows.Count>0?windows[0]:IntPtr.Zero;
  if(hwin==IntPtr.Zero)return new ActiveResult {Status=windows.Count>1?"请切到要查看的聊天窗口":"等待 Codex 窗口"};
  last=hwin;UIElement root=null;UIElements rows=null;var matches=new Dictionary<string,string>();bool ambiguous=false;
  try {
   root=automation.ElementFromHandle(hwin);var rootBounds=root.GetCurrentPropertyValue(30001) as double[];rows=root.FindAll(4,currentCondition);
   for(int i=0;i<rows.Length;i++){var row=rows.GetElement(i);try {
    // Side navigation entries are buttons. Skip current message anchors/links.
    object role=row.GetCurrentPropertyValue(30003);if(!(role is int)||(int)role!=50000)continue;
    string aria=row.GetCurrentPropertyValue(30102) as string;if(aria==null||!aria.Split(';').Contains("current=page"))continue;
    // Bounding rectangle is metadata; ignore controls outside the left sidebar.
    // UIA rectangles are physical pixels. Do not compare them with virtualized
    // WinForms/GetWindowRect coordinates on a scaled monitor.
    var b=row.GetCurrentPropertyValue(30001) as double[];if(b==null||b.Length!=4||rootBounds==null||rootBounds.Length!=4||b[0]>rootBounds[0]+Math.Min(650,rootBounds[2]*0.5)||b[2]>650)continue;
    string title=row.GetCurrentPropertyValue(30005) as string;if(String.IsNullOrEmpty(title))continue;
    string[] found=titles.Resolve(title);if(found.Length>1)ambiguous=true;else if(found.Length==1)matches[found[0]]=title;
   }finally{Marshal.ReleaseComObject(row);}}
  }finally{if(rows!=null)Marshal.ReleaseComObject(rows);if(root!=null)Marshal.ReleaseComObject(root);}
  if(ambiguous||matches.Count>1)return new ActiveResult {Window=hwin,Status="同名聊天，暂无法确定"};
  if(matches.Count==1)return new ActiveResult {Window=hwin,Id=matches.First().Key,Title=matches.First().Value,Status="自动跟随"};
  return new ActiveResult {Window=hwin,Status=titles.Snapshot().Count==0?"会话索引暂不可读":"当前页面没有本地聊天"};
 }
 public void Dispose(){Marshal.ReleaseComObject(currentCondition);Marshal.ReleaseComObject(automation);}
}
}
