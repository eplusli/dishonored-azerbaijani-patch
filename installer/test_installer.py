"""Exercise the actual compiled installer against disposable game fixtures.

Original files are hard-linked read-only inputs; replacement never mutates their bytes.
No real installed game file is modified.
"""
from pathlib import Path
import json,os,subprocess,hashlib,shutil,time
ROOT=Path(__file__).resolve().parent.parent
EXE=ROOT/'release/Dishonored-AZ-Setup-0.1.0-beta.1.exe'
BUILD=ROOT/'work/installer_build'; FIX=BUILD/'test oyun ə ş ı'
ROWS=json.loads((ROOT/'outputs/Dishonored-AZ-TestBuild/installation_manifest.json').read_text(encoding='utf-8'))['files']
REPORT=BUILD/'test_result.json';RESULTS=[]
def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
 return h.hexdigest()
def run(name,mode='install',success=True,extra=(),contains=None):
 start=time.time()
 p=subprocess.run([str(EXE),'--headless','--'+mode,'--root',str(FIX),'--report',str(REPORT),*extra])
 data=json.loads(REPORT.read_text(encoding='utf-8-sig'))
 assert data['success']==success and (p.returncode==0)==success,(name,p.returncode,data)
 if contains:assert contains in data['message'],(name,data)
 RESULTS.append(dict(test=name,success=True,seconds=round(time.time()-start,2),result=data))
 print(json.dumps(RESULTS[-1],ensure_ascii=True),flush=True)
def originals():
 for row in ROWS:assert sha(FIX/row['relative'])==row['original_sha256'],row['relative']
def cleanup():
 resolved=FIX.resolve(); assert resolved.is_relative_to(BUILD.resolve()) and resolved.name.startswith('test oyun')
 if resolved.exists():shutil.rmtree(resolved)
def setup():
 cleanup(); FIX.mkdir(parents=True)
 fake=FIX/'Binaries/Win32/Dishonored.exe';fake.parent.mkdir(parents=True);fake.write_bytes(b'fixture marker, not executable')
 for row in ROWS:
  p=FIX/row['relative'];p.parent.mkdir(parents=True,exist_ok=True);os.link(row['backup'],p)
def main():
 setup()
 # Bad folder: no game marker and no game files changed.
 marker=FIX/'Binaries/Win32/Dishonored.exe';marker.unlink()
 run('wrong game folder rejected',success=False,contains='game root containing');assert not (FIX/'Dishonored-AZ-Patch').exists()
 marker.write_bytes(b'fixture marker, not executable')
 # Tampering replaces only the disposable hard-link directory entry.
 victim=FIX/ROWS[0]['relative'];victim.unlink();victim.write_bytes(b'unsupported game revision')
 run('modified original rejected before changes',success=False,contains='Unsupported or modified game file');assert not (FIX/'Dishonored-AZ-Patch').exists()
 victim.unlink();os.link(ROWS[0]['backup'],victim)
 run('simulated commit failure rolls back all originals',success=False,extra=('--fail-after','300'),contains='original files were restored. Simulated write failure')
 originals()
 run('full base and DLC installation with Unicode path')
 for row in ROWS:assert sha(FIX/row['relative'])==row['patched_sha256'],row['relative']
 run('repeat install is safe and idempotent')
 run('full uninstall restores every original',mode='restore');originals()
 # Restoration copied the original bytes back. Re-create the fixture with hard
 # links before the next full installation to avoid duplicating private backups
 # on disk during the optional-DLC tests.
 setup()
 # Missing DLC is skipped, while partial DLC is refused.
 dlc=FIX/'DishonoredGame/DLC/PCConsole/DLC07'
 shutil.rmtree(dlc)
 one=next(r for r in ROWS if '/DLC07/' in r['relative'].replace('\\','/'))
 p=FIX/one['relative'];p.parent.mkdir(parents=True,exist_ok=True);os.link(one['backup'],p)
 run('partial DLC rejected',success=False)
 shutil.rmtree(dlc)
 run('absent DLC skipped without installing missing content')
 state=json.loads((FIX/'Dishonored-AZ-Patch/state.json').read_text(encoding='utf-8-sig'))
 assert len(state['files'])==718
 run('restore with absent DLC',mode='restore')
 for row in ROWS:
  if '/DLC07/' not in row['relative'].replace('\\','/'):assert sha(FIX/row['relative'])==row['original_sha256']
 cleanup()
 for row in ROWS:assert sha(Path(row['backup']))==row['original_sha256']
 out=ROOT/'release/installer-validation.json'
 out.write_text(json.dumps(dict(status='passed',tests=RESULTS,files=746,original_backups_verified=True,real_game_modified=False,installer_sha256=sha(EXE)),indent=2),encoding='utf-8')
 print(json.dumps({'status':'passed','tests':len(RESULTS),'report':str(out)}),flush=True)
if __name__=='__main__':main()
