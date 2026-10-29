"""Publish only the explicit public source allowlist using authenticated gh.

No credentials, game archives, backups or translation workspace are read.
"""
from pathlib import Path
import json,base64,subprocess
ROOT=Path(__file__).resolve().parent.parent
GH=ROOT/'work/installer_tools/gh/bin/gh.exe'
REPO='eplusli/dishonored-azerbaijani-patch'
TMP=ROOT/'work/installer_build/github_request.json'
def api(route,data=None,method=None):
 args=[str(GH),'api',route]
 if data is not None:
  TMP.write_text(json.dumps(data),encoding='utf-8');args+=['--method',method or 'POST','--input',str(TMP)]
 p=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
 if p.returncode:raise RuntimeError(p.stderr)
 return json.loads(p.stdout) if p.stdout.strip() else None
def main():
 repo=api('repos/'+REPO);branch=repo['default_branch']
 # Initialize an empty repository through the contents API before Git tree APIs.
 if repo['size']==0:
  try:api('repos/'+REPO+'/git/ref/heads/'+branch)
  except RuntimeError:
   api('repos/'+REPO+'/contents/README.md',dict(message='Introduce Azerbaijani Dishonored beta patch',content=base64.b64encode((ROOT/'README.md').read_bytes()).decode()),'PUT')
 ref=api('repos/'+REPO+'/git/ref/heads/'+branch)['object']['sha']
 commit=api('repos/'+REPO+'/git/commits/'+ref)
 files=[ROOT/'README.md',ROOT/'LICENSE',ROOT/'.gitignore']
 files+=sorted((ROOT/'installer').rglob('*'))
 files=[p for p in files if p.is_file() and '__pycache__' not in p.parts and p.suffix!='.pyc']
 tree=api('repos/'+REPO+'/git/trees',dict(base_tree=commit['tree']['sha'],tree=[dict(path=p.relative_to(ROOT).as_posix(),mode='100644',type='blob',content=p.read_text(encoding='utf-8-sig')) for p in files]))
 created=api('repos/'+REPO+'/git/commits',dict(message='Add delta-only Windows installer with compatibility checks and recovery',tree=tree['sha'],parents=[ref]))
 api('repos/'+REPO+'/git/refs/heads/'+branch,dict(sha=created['sha'],force=False),'PATCH')
 print(json.dumps(dict(repository=repo['html_url'],commit=created['sha'],public_source_files=len(files))))
if __name__=='__main__':main()
