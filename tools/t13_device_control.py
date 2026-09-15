"""T13 exact-file device transport; each explicit command is sent once and read back."""
import argparse, datetime, hashlib, json, os, pathlib, subprocess, time, uuid
ROOT=pathlib.Path(os.environ.get('C6_T13_EVIDENCE_ROOT', str(pathlib.Path(__file__).resolve().parents[1]/'Logs'/'T13'/'device-evidence')))
DEVICES={'iphone':'55DF5446-D2F2-5DAC-8697-6AF48375313E','ipad':'866577E8-9037-5AE4-8A23-C43C21539A36'}
BUNDLE='com.wolfuraark.c6prototype'
ENV=dict(os.environ,DEVELOPER_DIR='/Applications/Xcode.app/Contents/Developer')
def write(p,v):
 p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2))
def call(args,where):
 where.mkdir(parents=True,exist_ok=False)
 result=where/'core.json'
 args=list(map(str,args));index=args.index('--') if '--' in args else len(args)
 proc=subprocess.run(['xcrun','devicectl',*args[:index],'--timeout','30','--json-output',str(result),*args[index:]],env=ENV,capture_output=True,text=True)
 (where/'stdout.log').write_text(proc.stdout);(where/'stderr.log').write_text(proc.stderr)
 core=json.loads(result.read_text()) if result.is_file() else {}
 assert proc.returncode==0 and core.get('info',{}).get('outcome')=='success',(str(where),proc.returncode,core.get('error'))
 return core
class Device:
 def __init__(self,device,run):
  assert device in DEVICES and run and all(c.isalnum() or c=='-' for c in run)
  self.name=device;self.id=DEVICES[device];self.run=run;self.remote='Documents/T13/'+run
  self.folder=ROOT/run/device;self.folder.mkdir(parents=True,exist_ok=True)
 def entry(self,label):
  p=self.folder/(datetime.datetime.now(datetime.timezone.utc).strftime('%H%M%S%f')+'-'+label);p.mkdir();return p
 def copy(self,direction,source,destination,entry):
  return call(['device','copy',direction,'--device',self.id,'--domain-type','appDataContainer','--domain-identifier',BUNDLE,'--source',source,'--destination',destination],entry)
 def fetch(self,name,label='read'):
  assert '/' not in name and name not in {'.','..'}
  entry=self.entry(label);p=entry/name;self.copy('from',self.remote+'/'+name,p,entry/'transport');return p
 def status(self):
  p=self.fetch('status.json','status');v=json.loads(p.read_text());assert v['run']==self.run and v['build']=='20',v;return v
 def launch(self):
  e=self.entry('launch');return call(['device','process','launch','--device',self.id,'--environment-variables',json.dumps({'C6_T13_DEVICE_RUN':self.run}),BUNDLE],e/'transport')
 def send(self,command):
  command=dict(command)
  command.setdefault('id',uuid.uuid4().hex);command['run']=self.run;command['schema']=1;command['build']='20'
  e=self.entry('send-'+command['action']);p=e/'command.json';write(p,command);raw=p.read_bytes()
  # Whole-second mtime avoids CoreDevice treating equal-sized commands as unchanged.
  target=int(time.time())+1
  while time.time()<target+.02:time.sleep(.05)
  os.utime(p,(target,target))
  self.copy('to',p,self.remote+'/command.json',e/'send')
  returned=e/'readback.json';self.copy('from',self.remote+'/command.json',returned,e/'readback')
  assert returned.read_bytes()==raw,'Remote command differs; never resend an uncertain action.'
  write(e/'dispatch.json',{'id':command['id'],'sha256':hashlib.sha256(raw).hexdigest(),'byteExact':True,'sentOnce':True})
  return command,e
 def act(self,action,**fields):
  before=self.status();command=dict(action=action,expectedBoot=before['boot'],**fields)
  if action not in {'createRoom','joinDirect','capture'}:
   command.update(expectedSession=before['sessionId'],expectedRoom=before['roomId'],expectedRound=before['roundId'])
  else:
   command.update(expectedSession='',expectedRoom='',expectedRound=0)
  command,entry=self.send(command);deadline=time.monotonic()+45
  while True:
   observed=self.status()
   if observed['lastCommandId']==command['id'] and observed['commandStatus'] in {'PASS','FAIL'}:break
   assert time.monotonic()<deadline,'Command not completed; no resend'
   time.sleep(.3)
  p=self.fetch('result-'+command['id']+'.json','result')
  result=json.loads(p.read_text())
  assert result['id']==command['id'] and result['run']==self.run and result['commandHash']==hashlib.sha256((entry/'command.json').read_bytes()).hexdigest()
  assert result['status']=='PASS',(action,result['error'],str(p))
  if result.get('screenshot'):self.fetch(result['screenshot'],'screenshot')
  return result
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('device',choices=DEVICES);parser.add_argument('run');parser.add_argument('action',choices=['status','launch']);a=parser.parse_args();d=Device(a.device,a.run);print(json.dumps(getattr(d,a.action)(),ensure_ascii=False,indent=2))
