using System;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace CompactCounter {
// One lightweight process, no service, hooks, injected code or polling UIA while
// Codex is closed. The login shortcut starts this background application context.
public sealed class LifecycleDecision {
 public bool WasOpen,Suppressed;
 public bool ShouldShow(bool codexOpen,bool counterOpen){
  if(!codexOpen){WasOpen=false;Suppressed=false;return false;}
  if(!WasOpen){Suppressed=false;WasOpen=true;}
  return !counterOpen&&!Suppressed;
 }
 public void UserClosed(){Suppressed=true;}
}
public sealed class Lifecycle : ApplicationContext {
 [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr window,int command);
 readonly string root,state;readonly Timer timer=new Timer();readonly LifecycleDecision decision=new LifecycleDecision();
 LiteWindow window;Engine engine;bool automaticClose;bool exiting;
 public Lifecycle(string logRoot,string statePath){root=logRoot;state=statePath;timer.Interval=1000;timer.Tick+=(s,e)=>Tick();timer.Start();Tick();}
 void Tick(){
  if(exiting)return;bool codexOpen=ActiveChat.FindWindows().Count>0;
  if(!codexOpen&&window!=null&&!window.IsDisposed){automaticClose=true;window.Close();window=null;automaticClose=false;}
  if(decision.ShouldShow(codexOpen,window!=null&&!window.IsDisposed)){
   try {if(engine==null)engine=new Engine(root,state);window=new LiteWindow(engine);window.FormClosed+=(s,e)=>{if(!automaticClose)decision.UserClosed();window=null;};window.Show();
    // STARTUPINFO can override the first Show with SW_HIDE/SW_MINIMIZE.
    // Explicitly show the counter after that first call, without taking focus.
    ShowWindowAsync(window.Handle,4);
   }
   catch(Exception e){decision.UserClosed();new NoticeWindow(null,"计数器无法启动",e.Message).ShowDialog();}
  }
 }
 protected override void ExitThreadCore(){exiting=true;timer.Stop();timer.Dispose();automaticClose=true;if(window!=null&&!window.IsDisposed)window.Close();base.ExitThreadCore();}
}
}
