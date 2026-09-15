#!/usr/bin/env python3
"""Explicit local Mac processes; preserves outputs and never claims physical-device results."""
import argparse,datetime,json,pathlib,subprocess,time
p=argparse.ArgumentParser();p.add_argument('--app',required=True);p.add_argument('--output',required=True);a=p.parse_args()
app=pathlib.Path(a.app);out=pathlib.Path(a.output)
assert app.is_file() and out.is_absolute() and not out.exists()
out.mkdir(parents=True);children=[];results={}
def launch(name,role,port=25114,**opts):
 directory=out/name;log=(out/(name+'.log')).open('w')
 args=[str(app),'-screen-fullscreen','0','-screen-width',str(opts.pop('width',390)),'-screen-height',str(opts.pop('height',844)),
       '-c6T10ProbeDirectory',str(directory),'-c6T10Role',role,'-c6T10Port',str(port),'-c6T10ProbeQuit','-logFile',str(out/(name+'-unity.log'))]
 for k,v in opts.items():args.extend(['-c6T10'+k,str(v)])
 proc=subprocess.Popen(args,stdout=log,stderr=subprocess.STDOUT);children.append((proc,log,name));return proc,directory

def read(directory,name='progress.json'):
 try:return json.loads((directory/name).read_text())
 except (FileNotFoundError,json.JSONDecodeError):return {}

def wait_for(predicate,seconds,label):
 end=time.monotonic()+seconds
 while not predicate():
  if time.monotonic()>end:raise RuntimeError(label+' timed out; preserve all logs')
  time.sleep(.2)

def finish(proc,directory):
 wait_for(lambda:proc.poll() is not None,150,directory.name)
 result=read(directory,'result.json');results[directory.name]=result
 if proc.returncode!=0 or result.get('status')!='PASS':raise RuntimeError(directory.name+' failed: '+str(result.get('error')))
 print(directory.name+': PASS',flush=True)

def pair(prefix,port,route,guards):
 host,hd=launch(prefix+'-host','host',port,RoomName='C6-'+prefix,StartDelay=20 if guards else 1)
 wait_for(lambda:read(hd).get('label')=='host-advertised',40,'Host advertise')
 if guards:
  for name,extra in [('wrong-build',dict(JoinBuild='13',ExpectedReason='BUILD_MISMATCH')),
                     ('stale-room',dict(RoomId='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',ExpectedReason='STALE_ROOM'))]:
   q,d=launch(name,'reject',port,**extra);finish(q,d)
 client,cd=launch(prefix+'-client','client',port,RoomName='C6-'+prefix,JoinRoute=route)
 if guards:
  wait_for(lambda:read(cd).get('label') in ('local-ready','playing'),75,'Client Ready')
  q,d=launch('third-participant','reject',port,ExpectedReason='C6_T02_ROOM_FULL');finish(q,d)
 finish(host,hd);finish(client,cd)
 h=results[hd.name]['finalSnapshot'];c=results[cd.name]['finalSnapshot']
 for field in ('protocol','build','roomId','sessionId','revision','phase','seed','roundId','hostConfigJson','configFingerprint','p1','p2Entries','canStart','startEntries'):
  assert h[field]==c[field],field+' differs between peers'
 assert h['phase']=='Playing' and h['roundId']==1 and results[cd.name]['joinRoute']==route
 print(prefix+': matching Host Start contract via '+route,flush=True)
error=None
try:
 q,d=launch('discovery-lifecycle','discovery',25116,RoomName='C6-discovery-fixture');finish(q,d)
 pair('bonjour',25114,'BONJOUR',True)
 pair('direct-fallback',25115,'DIRECT_IP',False)
 q,d=launch('render-tablet','render',25117,width=560,height=746);finish(q,d)
except Exception as ex:error=str(ex)
finally:
 # Only these explicit probes own this loop. On failure leave a bounded probe running to write its timeout evidence;
 # do not force-kill user apps or another project's Unity Editor.
 for proc,log,name in children:
  if proc.poll() is not None:log.close()
 (out/'summary.json').write_text(json.dumps(dict(status='FAIL' if error else 'PASS',error=error,executedUTC=datetime.datetime.now(datetime.timezone.utc).isoformat(),physicalDevice=False,app=str(app),results={k:v.get('status') for k,v in results.items()}),indent=2))
if error:raise SystemExit(error)
