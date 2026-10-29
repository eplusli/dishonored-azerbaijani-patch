// LZO decoder adapted from LZOkay, Copyright (c) 2018 Jack Andersen, MIT.
// See licenses/LZOkay-MIT.txt. Package layout is restricted to the verified build.
using System;
using System.IO;

static class UnrealPackage {
    static void Need(bool value) { if (!value) throw new InvalidDataException("Invalid compressed game package."); }
    static int Read(byte[] b, ref int p) { Need(p < b.Length); return b[p++]; }
    static int Word(byte[] b, ref int p) { int a=Read(b,ref p); return a | (Read(b,ref p)<<8); }
    static int Extended(byte[] b, ref int p) { int n=0; while (true) { int a=Read(b,ref p); if(a!=0) return checked(n+a); n=checked(n+255); } }
    static void Literal(byte[] b, ref int p, byte[] o, ref int q, int n) {
        Need(n>=0 && p<=b.Length-n && q<=o.Length-n); Buffer.BlockCopy(b,p,o,q,n); p+=n; q+=n;
    }
    public static byte[] Lzo(byte[] b, int size) {
        byte[] o=new byte[size]; int p=0,q=0,state=0,ns=0,len=0,back=0;
        Need(b.Length>=3);
        if(b[0]>=22) { int n=Read(b,ref p)-17; Literal(b,ref p,o,ref q,n); state=4; }
        else if(b[0]>=18) { ns=Read(b,ref p)-17; state=ns; Literal(b,ref p,o,ref q,ns); }
        while(true) {
            int inst=Read(b,ref p);
            if((inst&0xc0)!=0) { back=q-((Read(b,ref p)<<3)+((inst>>2)&7)+1); len=(inst>>5)+1; ns=inst&3; }
            else if((inst&32)!=0) {
                len=(inst&31)+2; if(len==2) len=checked(len+31+Extended(b,ref p));
                ns=Word(b,ref p); back=q-((ns>>2)+1); ns&=3;
            } else if((inst&16)!=0) {
                len=(inst&7)+2; if(len==2) len=checked(len+7+Extended(b,ref p));
                ns=Word(b,ref p); back=q-(((inst&8)<<11)+(ns>>2)); ns&=3;
                if(back==q) { Need(len==3 && p==b.Length && q==size); return o; }
                back-=16384;
            } else if(state==0) {
                int n=inst+3; if(n==3) n=checked(n+15+Extended(b,ref p));
                Literal(b,ref p,o,ref q,n); state=4; continue;
            } else {
                ns=inst&3; back=q-((inst>>2)+(Read(b,ref p)<<2)+(state==4?2049:1)); len=state==4?3:2;
            }
            Need(back>=0 && back<q && len>=0 && q<=o.Length-len);
            for(int i=0;i<len;i++) o[q++]=o[back++];
            state=ns; Literal(b,ref p,o,ref q,ns);
        }
    }
    static int I(byte[] b,int p) { Need(p>=0 && p<=b.Length-4); return BitConverter.ToInt32(b,p); }
    static uint U(byte[] b,int p) { return unchecked((uint)I(b,p)); }
    static void Put(byte[] b,int p,int v) { Buffer.BlockCopy(BitConverter.GetBytes(v),0,b,p,4); }
    public static byte[] Decode(string path) {
        byte[] b=File.ReadAllBytes(path); Need(U(b,0)==0x9e2a83c1);
        int fp=checked(16+I(b,12)); int p=checked(fp+4+7*4+4*4+16);
        int gen=I(b,p); Need(gen>=0 && gen<1000); p=checked(p+4+gen*12+8);
        int flags=I(b,p),count=I(b,p+4); if(flags==0) return b;
        Need(flags==2 && count>0 && count<10000);
        int max=0;
        for(int i=0;i<count;i++) { int c=p+8+i*16; max=Math.Max(max,checked(I(b,c)+I(b,c+4))); }
        Need(max>0 && max<1024*1024*1024); byte[] o=new byte[max];
        int first=I(b,p+8),compressedFirst=I(b,p+16),tail=p+8+count*16;
        Need(compressedFirst>=tail && first==p+8+compressedFirst-tail && first<=max);
        Buffer.BlockCopy(b,0,o,0,p+8); Buffer.BlockCopy(b,tail,o,p+8,compressedFirst-tail);
        Put(o,p,0); Put(o,p+4,0); Put(o,fp,I(b,fp)&~0x02000000);
        for(int i=0;i<count;i++) {
            int c=p+8+i*16,off=I(b,c),size=I(b,c+4),coff=I(b,c+8),csz=I(b,c+12);
            Need(off>=0 && size>0 && off<=max-size && coff>=0 && csz>=16 && coff<=b.Length-csz);
            Need(U(b,coff)==0x9e2a83c1); int block=I(b,coff+4),ut=I(b,coff+12);
            Need(block>0 && ut==size); int n=checked((ut+block-1)/block),q=coff+16+n*8,dest=off;
            for(int j=0;j<n;j++) {
                int cb=I(b,coff+16+j*8),ub=I(b,coff+20+j*8);
                Need(cb>0 && ub>0 && ub<=block && q<=b.Length-cb && dest<=o.Length-ub);
                byte[] encoded=new byte[cb]; Buffer.BlockCopy(b,q,encoded,0,cb);
                byte[] decoded=Lzo(encoded,ub); Buffer.BlockCopy(decoded,0,o,dest,ub); q+=cb; dest+=ub;
            }
            Need(dest==off+size && q==coff+csz);
        }
        return o;
    }
}
