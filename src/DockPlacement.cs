using System;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;

namespace CompactCounter {
// Offsets belong to Codex's edges, not a monitor. Clamping never overwrites
// the preferred position, so it returns when there is space again.
public sealed class DockPlacement {
 public bool Right=true,Bottom=false;
 public int X=8,Y=72;
 public static DockPlacement Default(Rectangle owner,Size size,Rectangle area){
  return new DockPlacement {X=owner.Right+8+size.Width<=area.Right?8:-size.Width-8};
 }
 public Point Position(Rectangle owner,Size size,Rectangle area){
  int x=(Right?owner.Right:owner.Left)+X,y=(Bottom?owner.Bottom:owner.Top)+Y;
  return new Point(Math.Max(area.Left,Math.Min(x,area.Right-size.Width)),Math.Max(area.Top,Math.Min(y,area.Bottom-size.Height)));
 }
 public static DockPlacement Capture(Rectangle owner,Rectangle counter,int threshold){
  int x=counter.Left,y=counter.Top;
  if(counter.Bottom>=owner.Top&&counter.Top<=owner.Bottom){
   int left=owner.Left-counter.Width-8,right=owner.Right+8;
   if(Math.Abs(x-left)<=threshold)x=left;else if(Math.Abs(x-right)<=threshold)x=right;
  }
  if(counter.Right>=owner.Left&&counter.Left<=owner.Right){
   int top=owner.Top-counter.Height-8,bottom=owner.Bottom+8;
   if(Math.Abs(y-top)<=threshold)y=top;else if(Math.Abs(y-bottom)<=threshold)y=bottom;
  }
  bool rightEdge=Math.Abs(x+counter.Width/2-owner.Right)<Math.Abs(x+counter.Width/2-owner.Left);
  bool bottomEdge=Math.Abs(y+counter.Height/2-owner.Bottom)<Math.Abs(y+counter.Height/2-owner.Top);
  return new DockPlacement {Right=rightEdge,Bottom=bottomEdge,X=x-(rightEdge?owner.Right:owner.Left),Y=y-(bottomEdge?owner.Bottom:owner.Top)};
 }
 public static DockPlacement Load(string path){
  try{var value=new JavaScriptSerializer().Deserialize<DockPlacement>(File.ReadAllText(path));
   return value!=null&&Math.Abs((long)value.X)<100000&&Math.Abs((long)value.Y)<100000?value:null;
  }catch{return null;}
 }
 public void Save(string path){
  Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
  File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this));
  if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
 }
}
}
