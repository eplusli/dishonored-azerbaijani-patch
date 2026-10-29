"""Build delta-only distribution; never changes the installed game or backups."""
from pathlib import Path
import hashlib,json,sys,subprocess,zipfile,shutil
ROOT=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(ROOT/'work/tools'))
from probe import decompress
BASE=ROOT/'installer'; BUILD=ROOT/'work/installer_build'; BUILD.mkdir(exist_ok=True)
PAY=BUILD/'payload'; PAY.mkdir(exist_ok=True)
XDELTA=ROOT/'work/installer_tools/xdelta/xdelta3-3.2.2-windows-x86_64/xdelta3.exe'
def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
 return h.hexdigest()
def main():
 rows=json.loads((ROOT/'outputs/Dishonored-AZ-TestBuild/installation_manifest.json').read_text(encoding='utf-8'))['files']; files=[]
 for i,row in enumerate(rows):
  old,new=Path(row['backup']),Path(row['staged']); rel=row['relative'].replace('\\','/')
  assert sha(old)==row['original_sha256'] and sha(new)==row['patched_sha256'],rel
  raw=BUILD/'source.raw'; output=BUILD/'verified.raw'; patch=PAY/f'{i:04d}.vcdiff'
  isupk=rel.endswith('.upk')
  raw.write_bytes(decompress(old) if isupk else old.read_bytes())
  subprocess.run([str(XDELTA),'-e','-f','-9','-D','-A','-s',str(raw),str(new),str(patch)],check=True,capture_output=True)
  subprocess.run([str(XDELTA),'-d','-f','-D','-R','-s',str(raw),str(patch),str(output)],check=True,capture_output=True)
  assert sha(output)==row['patched_sha256'],rel
  group=rel.split('/')[3] if rel.startswith('DishonoredGame/DLC/PCConsole/') else 'Base'
  files.append(dict(path=rel,original=row['original_sha256'],patched=row['patched_sha256'],raw=sha(raw),
                    originalSize=old.stat().st_size,patchedSize=new.stat().st_size,rawSize=raw.stat().st_size,
                    delta=patch.name,deltaHash=sha(patch),unreal=isupk,group=group))
  raw.unlink(); output.unlink()
  if (i+1)%25==0:print(json.dumps({'verified':i+1,'total':len(rows),'delta_bytes':sum(p.stat().st_size for p in PAY.glob('*.vcdiff'))}),flush=True)
 shutil.copy2(XDELTA,PAY/'xdelta3.exe')
 for p in (BASE/'licenses').glob('*.txt'):shutil.copy2(p,PAY/p.name)
 manifest=dict(version='0.1.0-beta.1',files=files,toolHash=sha(PAY/'xdelta3.exe'))
 (PAY/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
 out=BUILD/'payload.zip'
 with zipfile.ZipFile(out,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
  for p in sorted(PAY.iterdir()):z.write(p,p.name)
 print(json.dumps({'status':'all_delta_roundtrips_passed','files':len(files),'payload_bytes':out.stat().st_size}),flush=True)
if __name__=='__main__':main()
