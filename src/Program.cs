using System;using System.IO;using System.Linq;using System.Text;using System.Threading;using System.Windows.Forms;
namespace CompactCounter {
public static class Program {
 [STAThread]public static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  string home=Environment.GetEnvironmentVariable("CODEX_HOME");if(String.IsNullOrEmpty(home))home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
  string root=Path.Combine(home,"sessions"),data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexCompactCounter");bool scan=false;
  for(int i=0;i<args.Length;i++){if(args[i]=="--sessions"&&i+1<args.Length)root=args[++i];else if(args[i]=="--data"&&i+1<args.Length)data=args[++i];else if(args[i]=="--scan-once")scan=true;else if(args[i]=="--watch"){}else if(args[i]=="--bind"&&i+1<args.Length)i++;else{new NoticeWindow(null,"启动参数","参数：--sessions 日志目录 --data 状态目录 --scan-once").ShowDialog();return 2;}}
  try {
   root=Path.GetFullPath(root);data=Path.GetFullPath(data);
   foreach(string forbidden in new string[]{root,home,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex")}){string prefix=Path.GetFullPath(forbidden).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if((data.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar).StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("状态目录必须位于 Codex 日志和配置目录之外。");}
   string state=Path.Combine(data,"state.json"),name;using(var sha=System.Security.Cryptography.SHA256.Create())name="Local\\CodexCompactCounter-"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(state.ToLowerInvariant()))).Replace("-","");
   bool first;using(var mutex=new Mutex(true,name,out first)){
    if(!first){if(!scan)new NoticeWindow(null,"计数器已在运行","切回 Codex 即可查看计数卡片。").ShowDialog();return 3;}
    if(scan){var engine=new Engine(root,state);int passes=0;do{engine.Poll();passes++;}while(engine.Pending>0&&passes<1000);Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"scan-result.txt"),"sessions="+engine.Data.Sessions.Count+"\r\ncompacted="+engine.Data.Events.Count(x=>x.Kind=="compacted")+"\r\npending="+engine.Pending+"\r\nwarning="+engine.Warning);return engine.Pending>0||engine.Warning!=""?1:0;}
    Application.Run(new Lifecycle(root,state));mutex.ReleaseMutex();
   }
  }catch(Exception e){if(!scan)new NoticeWindow(null,"无法启动",e.Message).ShowDialog();return 1;}
  return 0;
 }
}
}
