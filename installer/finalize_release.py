"""Package notices and checksums only after executable tests pass."""
from pathlib import Path
import json,hashlib,zipfile
ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'release'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 exe=OUT/'Dishonored-AZ-Setup-0.1.0-beta.1.exe'
 report=json.loads((OUT/'installer-validation.json').read_text(encoding='utf-8'))
 assert report['status']=='passed' and report['installer_sha256']==sha(exe)
 assert all(x['success'] for x in report['tests'])
 notices=OUT/'THIRD-PARTY-NOTICES.zip'
 with zipfile.ZipFile(notices,'w',compression=zipfile.ZIP_DEFLATED) as z:
  for p in sorted((ROOT/'installer/licenses').glob('*.txt')):z.write(p,p.name)
 assets=[exe,OUT/'installer-validation.json',notices]
 (OUT/'SHA256SUMS.txt').write_text(''.join(sha(p)+'  '+p.name+'\n' for p in assets),encoding='ascii')
 print(json.dumps({'status':'release_ready','assets':[p.name for p in assets],'installer_bytes':exe.stat().st_size}))
if __name__=='__main__':main()
