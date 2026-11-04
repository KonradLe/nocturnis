"""Restore the exact Unity source assets for this release. Python 3.10+, stdlib only."""
from pathlib import Path, PurePosixPath
import argparse, hashlib, json, zipfile, sys
ROOT = Path(__file__).resolve().parents[1]
def sha_file(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for data in iter(lambda:f.read(4*1024*1024),b''): h.update(data)
    return h.hexdigest()
def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', required=True, type=Path)
    parser.add_argument('--verify-only', action='store_true')
    args=parser.parse_args()
    release=json.loads((ROOT/'docs/project-release.json').read_text(encoding='utf-8'))
    rows=json.loads((ROOT/'docs/project-files.json').read_text(encoding='utf-8'))['files']
    if sha_file(args.archive)!=release['sha256']:
        raise ValueError('Archive SHA256 differs from the pinned release. No files changed.')
    pending=[]
    with zipfile.ZipFile(args.archive) as archive:
        names=archive.namelist()
        if len(names)!=len(set(names)): raise ValueError('Duplicate ZIP member')
        for row in rows:
            rel=PurePosixPath(row['path'])
            if rel.is_absolute() or '..' in rel.parts or ':' in str(rel) or '\\' in str(rel):
                raise ValueError('Unsafe archive path')
            target=ROOT/str(rel)
            if not target.resolve().is_relative_to(ROOT.resolve()):
                raise ValueError('Target escapes the project')
            if target.is_symlink(): raise ValueError('Symlink target: '+str(rel))
            info=archive.getinfo(str(rel))
            if info.file_size!=row['bytes']: raise ValueError('Wrong member size: '+str(rel))
            if target.exists():
                if not target.is_file() or sha_file(target)!=row['sha256']:
                    raise ValueError('Existing modified file; preserve/move it before restore: '+str(rel))
            else: pending.append(row)
        for row in rows:
            h=hashlib.sha256()
            with archive.open(row['path']) as stream:
                for data in iter(lambda:stream.read(4*1024*1024),b''): h.update(data)
            if h.hexdigest()!=row['sha256']: raise ValueError('Invalid member: '+row['path'])
        if not args.verify_only:
            for row in pending:
                target=ROOT/row['path']; target.parent.mkdir(parents=True,exist_ok=True)
                with archive.open(row['path']) as source, target.open('xb') as output:
                    for data in iter(lambda:source.read(4*1024*1024),b''):output.write(data)
    print(json.dumps({'passed':True,'verified':len(rows),'restored':0 if args.verify_only else len(pending),'verify_only':args.verify_only}))
if __name__=='__main__':
    try: main()
    except (ValueError,OSError,KeyError,zipfile.BadZipFile) as exc:
        print(str(exc),file=sys.stderr);raise SystemExit(1)
