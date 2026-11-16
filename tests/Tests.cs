using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using CompactCounter;
class Tests {
 static string a="11111111-1111-4111-8111-111111111111",b="22222222-2222-4222-8222-222222222222";
 static int checks;
 static void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);checks++;}
 static string Meta(string id){return "{\"timestamp\":\"2026-11-16T00:00:00Z\",\"type\":\"session_meta\",\"payload\":{\"id\":\""+id+"\",\"cli_version\":\"test\",\"instructions\":\"PRIVATE_BODY_SENTINEL\"}}\n";}
 static string Event(int sec,string kind="compacted"){return "{\"timestamp\":\"2026-11-16T00:00:"+sec.ToString("00")+"Z\",\"type\":\""+kind+"\",\"payload\":{\"message\":\"PRIVATE_BODY_SENTINEL\"}}\n";}
 static void Write(string p,string s){File.WriteAllText(p,s,new UTF8Encoding(false));}
 static void Append(string p,string s){File.AppendAllText(p,s,new UTF8Encoding(false));}
 static int Main(string[] args){
  try {
   string home=Path.GetFullPath(args[0]);Directory.CreateDirectory(home);string logs=Path.Combine(home,"logs");Directory.CreateDirectory(logs);
   string state=Path.Combine(home,"data","state.json"),fa=Path.Combine(logs,"rollout-"+a+".jsonl"),fb=Path.Combine(logs,"rollout-"+b+".jsonl");
   Write(fa,Meta(a)+Event(1));var e=new Engine(logs,state);e.Poll();Check(e.Count(a)==1,"initial compacted marker");
   long bytes=e.BytesRead;e.Poll();Check(e.BytesRead==bytes,"unchanged files not rescanned");
   Append(fa,Event(1)+Event(2));e.Poll();Check(e.Count(a)==2,"append and duplicate deduplication");
   string partial=Event(3);Append(fa,partial.Substring(0,partial.Length-2));e.Poll();Check(e.Count(a)==2,"partial line deferred");
   e=new Engine(logs,state);e.Poll();Check(e.Count(a)==2,"restart during partial append");Append(fa,partial.Substring(partial.Length-2));e.Poll();Check(e.Count(a)==3,"partial line completed exactly once");
   Write(fb,Meta(b)+Event(1)+Event(4));e.Poll();Check(e.Count(b)==2&&e.Count(a)==3,"parallel sessions isolated");
   e=new Engine(logs,state);e.Poll();Check(e.Count(a)==3&&e.Count(b)==2,"restart persists counts and cursors");
   File.Move(fa,fa+".1");Write(fa,Meta(a)+Event(5));e.Poll();Check(e.Count(a)==4,"rename rotation and new file");
   File.Copy(fa+".1",Path.Combine(logs,"copy.jsonl"));e.Poll();Check(e.Count(a)==4,"copied log replay deduplicated");
   Write(fa,Meta(a)+Event(6));e.Poll();Check(e.Count(a)==5,"same-size rewrite detected by checkpoint hash");
   Write(fa,Meta(a));e.Poll();Append(fa,Event(7));e.Poll();Check(e.Count(a)==6,"truncate and append retains history");
   File.Move(fa,fa+".2");Write(fa,Event(8));e.Poll();Check(e.Count(a)==7,"headerless rotation associates filename UUID");
   Append(fa,"{\"timestamp\":\"2026-11-16T00:00:09Z\",\"type\":\"response_item\",\"payload\":{\"type\":\"compaction\",\"encrypted_content\":\"PRIVATE_BODY_SENTINEL\"}}\n");
   Append(fa,"{\"timestamp\":\"2026-11-16T00:00:10Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"context_compacted\"}}\n");e.Poll();Check(e.Count(a)==7&&e.Data.Events.Count(x=>x.Kind=="context_record")==1&&e.Data.Events.Count(x=>x.Kind=="notice")==1,"noncanonical markers kept separately without double count");
   Append(fa,"{\"timestamp\":\"2026-11-16T00:00:11Z\",\"type\":\"response_item\",\"payload\":{\"type\":\"message\",\"content\":[{\"text\":\"中文 PRIVATE_BODY_SENTINEL \\\"type\\\":\\\"compacted\\\"\"}]}}\n");e.Poll();Check(e.Count(a)==7,"body mentions cannot produce false events");
   Append(fa,"not-json\n"+Event(12));e.Poll();Check(e.Count(a)==8&&e.Data.Files.Values.Sum(x=>x.Invalid)==1,"malformed line reported and next line processed");
   Check(!File.ReadAllText(state).Contains("PRIVATE_BODY_SENTINEL"),"no chat content persisted");
   long offset=e.Data.Files.Values.Single(x=>x.Path==fa).Offset;Check(offset==new FileInfo(fa).Length,"UTF-8 byte checkpoint exact");
   // Unknown nested fields / escaped quotation marks are skipped structurally.
   Append(fa,"{\"payload\":{\"arbitrary\":[{\"nested\":[1,true,null,\"a\\\"b\"]}]},\"type\":\"compacted\",\"timestamp\":\"2026-11-16T00:00:13Z\"}\n");e.Poll();Check(e.Count(a)==9,"reordered metadata and nested bodies");
   // A source removed after discovery must not erase its persistent history.
   File.Move(fb,Path.Combine(home,"removed.jsonl"));e.Poll();Check(e.Count(b)==2,"removed source preserves indexed history");
   // Backup recovery replays unseen records, then deduplicates existing ones.
   Write(state,"broken");e=new Engine(logs,state);Check(e.Warning.Contains("备份"),"corrupted state falls back to atomic backup");e.Poll();Check(e.Count(a)==9&&e.Count(b)==2,"backup recovery reconciles events");
   Write(fa+".zst","compressed-binary\nnot-json\n");int badBefore=e.Data.Files.Values.Sum(x=>x.Invalid);e.Poll();Check(e.Data.Files.Values.Sum(x=>x.Invalid)==badBefore&&!e.Data.Files.Values.Any(x=>x.Path.EndsWith(".zst")),"compressed archives are not parsed as JSONL");
   e.Data.Files["legacy-zst"]=new Cursor{Path=fa+".zst",Session="33333333-3333-4333-8333-333333333333",Invalid=100000};e.Data.Sessions["33333333-3333-4333-8333-333333333333"]=new SessionInfo{Id="33333333-3333-4333-8333-333333333333"};e.Save();
   e=new Engine(logs,state);Check(!e.Data.Files.ContainsKey("legacy-zst")&&!e.Data.Sessions.ContainsKey("33333333-3333-4333-8333-333333333333")&&e.Count(a)==9,"legacy archive warnings migrated without losing counts");
   string index=Path.Combine(home,"session_index.jsonl");Write(index,"{\"id\":\""+a+"\",\"thread_name\":\"旧标题\"}\n{\"id\":\""+a+"\",\"thread_name\":\"测试聊天\"}\n");var titles=new TitleIndex(index);titles.Refresh();
   Check(titles.Resolve("测试聊天").SequenceEqual(new string[]{a})&&titles.Resolve("旧标题").Length==0,"sidebar title maps latest rename to exact session ID");
   Append(index,"{\"id\":\""+b+"\",\"thread_name\":\"测试聊天\"}\n");titles.Refresh();Check(titles.Resolve("测试聊天").Length==2,"duplicate titles remain ambiguous, never guess");
   Check(titles.Resolve("不存在").Length==0,"unmapped pages never reuse previous session");
   Check(Engine.IsPlainLog(fa+".1")&&Engine.IsPlainLog(fa+".old")&&!Engine.IsPlainLog(fa+".zip")&&!Engine.IsPlainLog(fa+".gz"),"rotation whitelist excludes compressed formats");
   var life=new LifecycleDecision();Check(!life.ShouldShow(false,false),"no counter before Codex opens");
   Check(life.ShouldShow(true,false),"opening Codex starts counter");
   Check(!life.ShouldShow(true,true),"existing counter never duplicated");
   life.UserClosed();Check(!life.ShouldShow(true,false),"manual hide respected until next Codex opening");
   Check(!life.ShouldShow(false,false)&&life.ShouldShow(true,false),"Codex close and reopen resets lifecycle");
   var owner=new Rectangle(300,200,800,600);var size=new Size(230,98);var area=new Rectangle(-1920,0,5760,2160);
   var dock=DockPlacement.Default(owner,size,area);var initial=dock.Position(owner,size,area);
   Check(initial==new Point(1108,272),"default dock sits next to Codex");
   Check(dock.Position(new Rectangle(340,225,800,600),size,area)==new Point(1148,297),"moving Codex preserves relative offset");
   Check(dock.Position(new Rectangle(300,200,1000,600),size,area)==new Point(1308,272),"resizing Codex keeps right edge attachment");
   dock=DockPlacement.Capture(owner,new Rectangle(1115,350,230,98),24);Check(dock.Position(owner,size,area)==new Point(1108,350),"drag release snaps to nearby right edge");
   dock=DockPlacement.Capture(owner,new Rectangle(40,360,230,98),24);Check(dock.Position(owner,size,area)==new Point(62,360),"drag release snaps to left edge");
   dock=DockPlacement.Capture(owner,new Rectangle(600,450,230,98),24);Check(dock.Position(new Rectangle(350,240,800,600),size,area)==new Point(650,490),"freely dragged position follows without jumping to default");
   string dockPath=Path.Combine(home,"window.json");dock.Save(dockPath);var restored=DockPlacement.Load(dockPath);Check(restored.Position(owner,size,area)==new Point(600,450),"dragged position survives restart");
   dock=new DockPlacement();var preferred=dock.Position(owner,size,area);dock.Position(owner,size,new Rectangle(0,0,1000,700));Check(dock.Position(owner,size,area)==preferred,"screen clamping does not overwrite preferred attachment");
   Check(dock.Position(new Rectangle(-1600,100,800,600),size,area)==new Point(-792,172),"negative monitor coordinates remain valid");
   Console.WriteLine("ALL "+checks+" CHECKS PASSED");return 0;
  }catch(Exception ex){Console.WriteLine(ex.ToString());return 1;}
 }
}
