"""Audit the official pinned Windows source ZIP against a raw Git source inventory.
No source extraction/build execution. Run with --zip --inventory --output.
"""
import argparse,hashlib,json,re,zipfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--zip',type=Path,required=True);p.add_argument('--inventory',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
a=p.parse_args();sha=lambda b:hashlib.sha256(b).hexdigest()
expected='9b3078d9ea77ceb49becb0198540527b03840b8a13b020aa5a80215d6bab418d'
assert sha(a.zip.read_bytes())==expected,'Official Windows source ZIP differs from pinned SHA256'
ib=a.inventory.read_bytes();inventory=json.loads(ib);git={r['path']:r for r in inventory['trackedFiles']}
assert inventory['commit']=='00d8bae801541db0d95a698e108bb4b1b273fe6e'
changed=[]
with zipfile.ZipFile(a.zip) as z:
 names=[r.filename for r in z.infolist() if not r.is_dir()];assert len(names)==len(set(names))
 rows={n:sha(z.read(n)) for n in names}
 for n in sorted(rows.keys()&git.keys()):
  if rows[n]!=git[n]['sha256']:
   data=z.read(n);normalized=re.sub(rb'\$Id: [0-9a-f]{40} \$',b'$Id$',data)
   changed.append({'path':n,'zipSha256':rows[n],'rawGitSha256':git[n]['sha256'],'differenceKind':'git-ident-expansion' if sha(normalized)==git[n]['sha256'] else 'other'})
report={'provenance':{'sourceUrl':'https://downloads.php.net/~windows/releases/php-8.4.27-1-src.zip','checksumUrl':'https://downloads.php.net/~windows/releases/releases.json','sourceBytes':a.zip.stat().st_size,'sourceSha256':expected,'rawGitRepository':inventory['repository'],'rawGitCommit':inventory['commit'],'rawGitInventorySha256':sha(ib)},'zipFiles':len(rows),'gitFiles':len(git),'zipOnly':sorted(rows.keys()-git.keys()),'gitOnly':sorted(git.keys()-rows.keys()),'changed':changed,'samePaths':rows.keys()==git.keys(),'allDifferencesAreIdentExpansion':bool(changed) and all(r['differenceKind']=='git-ident-expansion' for r in changed),'scope':'Source byte correspondence only; generated Windows headers are separate and no native rebuild/relink was run.'}
a.output.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({k:report[k] for k in ['zipFiles','gitFiles','samePaths','allDifferencesAreIdentExpansion']}))
