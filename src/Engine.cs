using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Web.Script.Serialization;

namespace CompactCounter {
public class Entry { public string Key,Session,Time,Kind; public long ByteOffset; public string File; }
public class SessionInfo { public string Id,First="",Last="",Version=""; }
public class Cursor { public string Identity,Path,Session="",Tail=""; public long Offset; public int Invalid; public bool Complete; }
public class State {
 public int Schema=1; public string Root="",Bound="",Mode="manual";
 public List<Entry> Events=new List<Entry>();
 public Dictionary<string,SessionInfo> Sessions=new Dictionary<string,SessionInfo>();
 public Dictionary<string,Cursor> Files=new Dictionary<string,Cursor>();
}
public sealed class Engine {
 public State Data; public readonly string StatePath; public string Warning=""; public long BytesRead; public int Pending;
 public string PrioritySession="";
 public static bool IsPlainLog(string path){return System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path),@"\.jsonl(?:\.[0-9]+|\.old|\.bak)?$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);}
 readonly HashSet<string> keys=new HashSet<string>();
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer { MaxJsonLength=int.MaxValue,RecursionLimit=256 };
 [StructLayout(LayoutKind.Sequential)] struct FileInfoNative { public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Creation,Access,Write; public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow; }
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandle(SafeFileHandle h,out FileInfoNative i);
 public static string Identity(FileStream s) { FileInfoNative f; if(!GetFileInformationByHandle(s.SafeFileHandle,out f))throw new IOException("无法读取文件标识");return f.Volume.ToString("x")+":"+f.IndexHigh.ToString("x")+":"+f.IndexLow.ToString("x")+":"+f.Creation.dwHighDateTime+":"+f.Creation.dwLowDateTime; }
 public Engine(string root,string statePath) {
  StatePath=Path.GetFullPath(statePath);Data=new State {Root=Path.GetFullPath(root)};
  if(File.Exists(StatePath)) {
   try {Data=Load(StatePath);} catch { if(File.Exists(StatePath+".bak")){Data=Load(StatePath+".bak");Warning="主索引损坏，已恢复备份；将继续补读日志。";}else throw new IOException("索引无法读取。请先备份状态目录，再重建索引。"); }
   if(!String.Equals(Data.Root,Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase))throw new IOException("此状态目录属于另一日志目录，请指定新的 --data 目录。");
  }
  bool migrated=false;foreach(var item in Data.Files.Where(x=>!IsPlainLog(x.Value.Path)).ToArray()){Data.Files.Remove(item.Key);migrated=true;}
  foreach(var id in Data.Sessions.Keys.ToArray())if(!Data.Files.Values.Any(x=>x.Session==id)&&!Data.Events.Any(x=>x.Session==id)){Data.Sessions.Remove(id);migrated=true;}
  foreach(var e in Data.Events)keys.Add(e.Key);if(migrated)Save();
 }
 static State Load(string path) { var d=Json.Deserialize<State>(File.ReadAllText(path,Encoding.UTF8));if(d==null||d.Schema!=1||d.Sessions==null||d.Events==null||d.Files==null)throw new IOException();return d; }
 public void Save() {
  Directory.CreateDirectory(Path.GetDirectoryName(StatePath));string temp=StatePath+".tmp";
  byte[] bytes=Encoding.UTF8.GetBytes(Json.Serialize(Data));
  using(var s=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){s.Write(bytes,0,bytes.Length);s.Flush(true);}
  if(File.Exists(StatePath))File.Replace(temp,StatePath,StatePath+".bak");else File.Move(temp,StatePath);
 }
 static string Tail(FileStream stream,long end) {
  if(end==0)return "";int count=(int)Math.Min(128,end);byte[] b=new byte[count];stream.Position=end-count;int n=0,r;while(n<count&&(r=stream.Read(b,n,count-n))>0)n+=r;
  using(var h=SHA256.Create())return Convert.ToBase64String(h.ComputeHash(b,0,n));
 }
 public static string ValidId(string s) { Guid id;return Guid.TryParse(s,out id)?id.ToString():""; }
 void Accept(Cursor c,Dictionary<string,string>d,long offset) {
  string kind=Projection.Get(d,"type"),time=Projection.Get(d,"timestamp");DateTimeOffset parsed;
  if(!DateTimeOffset.TryParse(time,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out parsed))return;
  time=parsed.ToUniversalTime().ToString("O");
  if(kind=="session_meta") {
   string id=ValidId(Projection.Get(d,"p.id"));if(id=="")return;c.Session=id;
   if(!Data.Sessions.ContainsKey(id))Data.Sessions[id]=new SessionInfo {Id=id,First=time};
   Data.Sessions[id].Version=Projection.Get(d,"p.cli_version");
  }
  if(c.Session==""||!Data.Sessions.ContainsKey(c.Session))return;
  var session=Data.Sessions[c.Session];if(String.CompareOrdinal(time,session.Last)>0)session.Last=time;
  // Only the verified durable rollout marker counts. response_item/compaction
  // and event_msg/context_compacted are diagnostics, never extra increments.
  string category=kind=="compacted"?"compacted":kind=="response_item"&&Projection.Get(d,"p.type")=="compaction"?"context_record":kind=="event_msg"&&Projection.Get(d,"p.type")=="context_compacted"?"notice":"";
  if(category=="")return;
  string eventId=Projection.Get(d,"p.event_id");
  string key=c.Session+"|"+category+"|"+(eventId!=""?"id:"+eventId:"time:"+time);
  if(keys.Add(key))Data.Events.Add(new Entry {Key=key,Session=c.Session,Time=time,Kind=category,File=c.Path,ByteOffset=offset});
 }
 public bool Poll() {
  bool changed=false;Pending=0;var errors=new List<string>();
  if(!Directory.Exists(Data.Root)){Warning="日志目录不存在或不可访问："+Data.Root;return false;}
  string[] paths;
  try { paths=Directory.GetFiles(Data.Root,"*",SearchOption.AllDirectories).Where(IsPlainLog).OrderByDescending(p=>PrioritySession!=""&&p.IndexOf(PrioritySession,StringComparison.OrdinalIgnoreCase)>=0).ThenByDescending(File.GetLastWriteTimeUtc).ToArray(); }
  catch(IOException){Warning="无法枚举日志目录，稍后重试。";return false;}catch(UnauthorizedAccessException){Warning="日志目录读取权限不足。";return false;}
  var seen=new HashSet<string>();
  foreach(string path in paths) {
   try {
    using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete,65536,FileOptions.SequentialScan)) {
     string id=Identity(stream);if(!seen.Add(id))continue;
     Cursor c; if(!Data.Files.TryGetValue(id,out c)){c=new Cursor {Identity=id,Path=path};Data.Files[id]=c;changed=true;}
     c.Path=path;
     long size=stream.Length;
     if(size<c.Offset || c.Offset>0&&Tail(stream,c.Offset)!=c.Tail){c.Offset=0;c.Session="";c.Tail="";changed=true;}
     if(c.Session=="") {
      var match=System.Text.RegularExpressions.Regex.Match(Path.GetFileName(path),@"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\.jsonl(?:\.|$)");
      if(match.Success){c.Session=ValidId(match.Groups[1].Value);if(!Data.Sessions.ContainsKey(c.Session))Data.Sessions[c.Session]=new SessionInfo {Id=c.Session};}
     }
     if(size==c.Offset){c.Complete=true;continue;}c.Complete=false;
     long start=c.Offset;stream.Position=start;
     // One complete record at a time; a 16 MiB fairness budget per file per poll.
     // Projection owns the stream; checkpoint only after a newline is committed.
     using(var reader=new Projection(stream,start,size)) {
      Dictionary<string,string>d;bool bad;
      while(reader.Next(out d,out bad)) {
       long recordStart=c.Offset;c.Offset=reader.Offset;changed=true;
       if(bad)c.Invalid++;else Accept(c,d,recordStart);
       if(c.Offset-start>=16L*1024*1024){if(c.Offset<size)Pending++;break;}
      }
      BytesRead+=reader.Offset-start;
      c.Tail=Tail(stream,c.Offset);
      c.Complete=c.Offset>=size;
     }
    }
   }catch(IOException){errors.Add("文件暂不可读");}catch(UnauthorizedAccessException){errors.Add("文件读取权限不足");}
  }
  Warning=errors.Count>0?errors.Count+" 个文件暂未读取，下轮自动重试。":"";
  int invalid=Data.Files.Values.Sum(x=>x.Invalid);if(invalid>0)Warning+=" 已跳过 "+invalid+" 条非完整格式记录。";
  if(changed)Save();return changed;
 }
 public int Count(string id){return Data.Events.Count(x=>x.Session==id&&x.Kind=="compacted");}
 public string Target(){return Data.Mode=="recent"?Data.Sessions.Values.OrderByDescending(x=>x.Last).Select(x=>x.Id).FirstOrDefault()??"":Data.Bound;}
}
}
