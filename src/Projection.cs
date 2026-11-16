using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace CompactCounter {
// Streaming JSON projection. Unselected string VALUES are skipped, never decoded
// or accumulated. No chat message, prompt, summary or tool output is retained.
public sealed class Projection : IDisposable {
    readonly Stream stream; readonly byte[] buffer = new byte[65536];
    int pos, length; public long Offset; public long Limit;
    public Projection(Stream s, long start, long limit) { stream=s; s.Position=start; Offset=start; Limit=limit; }
    int Peek() { if (Offset>=Limit) return -1; if(pos==length) { length=stream.Read(buffer,0,(int)Math.Min(buffer.Length,Limit-Offset)); pos=0; if(length==0)return -1; } return buffer[pos]; }
    int Take() { int c=Peek(); if(c>=0){pos++;Offset++;} return c; }
    void Space() { while(Peek()==32 || Peek()==9 || Peek()==13) Take(); }
    void Need(int c) { if(Take()!=c)throw new FormatException("Invalid JSON structure"); }
    string Str(bool keep) {
        Need(34); StringBuilder b=keep?new StringBuilder():null; int c;
        while((c=Take())>=0) {
            if(c==34)return keep?b.ToString():null;
            if(c<32)throw new FormatException("Unterminated JSON string");
            if(c==92) { c=Take(); if(c<0)throw new EndOfStreamException();
                if(c==117) { int code=0; for(int i=0;i<4;i++){int h=Take(); int v=h>=48&&h<=57?h-48:h>=65&&h<=70?h-55:h>=97&&h<=102?h-87:-1;if(v<0)throw new FormatException();code=(code<<4)+v;} if(keep&&b.Length<256)b.Append((char)code);continue; }
                if(c!=34&&c!=92&&c!=47&&c!=98&&c!=102&&c!=110&&c!=114&&c!=116)throw new FormatException();
                if(keep&&b.Length<256)b.Append((char)c);continue;
            }
            if(keep&&b.Length<256)b.Append((char)c); // selected metadata is ASCII
        }
        throw new EndOfStreamException();
    }
    void Skip(int depth) {
        if(depth>128)throw new FormatException("JSON nesting too deep");
        Space(); int c=Peek();
        if(c==34){Str(false);return;}
        if(c==123||c==91){Take();int end=c==123?125:93;Space();if(Peek()==end){Take();return;}while(true){if(c==123){Str(false);Space();Need(58);}Skip(depth+1);Space();if(Peek()==end){Take();return;}Need(44);Space();}}
        else { int n=0; while((c=Peek())>=0&&c!=44&&c!=125&&c!=93&&c!=10&&c!=13&&c!=32&&c!=9){Take();n++;} if(n==0)throw new FormatException(); }
    }
    static bool Allowed(string k, bool payload) {
        return payload ? k=="type"||k=="id"||k=="session_id"||k=="turn_id"||k=="event_id"||k=="cli_version"||k=="forked_from_id" : k=="type"||k=="timestamp";
    }
    void Obj(Dictionary<string,string> d, bool payload) {
        Need(123);Space();if(Peek()==125){Take();return;}
        while(true){string k=Str(true);Space();Need(58);Space();
            if(!payload && k=="payload" && Peek()==123)Obj(d,true);
            else if(Allowed(k,payload)&&Peek()==34)d[(payload?"p.":"")+k]=Str(true);
            else Skip(0);
            Space();if(Peek()==125){Take();return;}Need(44);Space();
        }
    }
    public bool Next(out Dictionary<string,string> d, out bool invalid) {
        d=new Dictionary<string,string>(); invalid=false; Space();if(Peek()<0)return false;
        try { Obj(d,false);Space();if(Peek()!=10){if(Peek()<0)return false;throw new FormatException();}Take();return true; }
        catch(EndOfStreamException){return false;}
        catch(FormatException){invalid=true;d.Clear();int c;while((c=Take())>=0&&c!=10){}return c==10;}
    }
    public void Dispose(){stream.Dispose();}
    public static string Get(Dictionary<string,string>d,string k){string v;return d.TryGetValue(k,out v)?v:"";}
}
}
