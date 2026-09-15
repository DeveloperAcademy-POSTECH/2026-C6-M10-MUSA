#!/usr/bin/env python3
"""Run only explicitly-owned local Mac probes; never physical-device evidence."""
import argparse,datetime,json,pathlib,subprocess,time,statistics
p=argparse.ArgumentParser();p.add_argument('--app',required=True);p.add_argument('--output',required=True);a=p.parse_args()
app=pathlib.Path(a.app);out=pathlib.Path(a.output);assert app.is_file() and out.is_absolute() and not out.exists()
out.mkdir(parents=True);children=[];results={}
def read(p):
 try:return json.loads(p.read_text())
 except (FileNotFoundError,json.JSONDecodeError):return {}
def wait(fn,seconds,label):
 until=time.monotonic()+seconds
 while not fn():
  if time.monotonic()>until:raise RuntimeError(label+' timeout')
  time.sleep(.2)
def launch(role,w,h):
 d=out/role; log=(out/(role+'-stdout.log')).open('w')
 args=[str(app),'-screen-fullscreen','0','-screen-width',str(w),'-screen-height',str(h),'-c6T10BNetworkProbeDirectory',str(d),'-c6T10BNetworkRole',role,'-c6T10BNetworkPort','25125','-c6T10BNetworkProbeQuit','-logFile',str(out/(role+'-unity.log'))]
 q=subprocess.Popen(args,stdout=log,stderr=subprocess.STDOUT);children.append((q,log));return q,d
error=None;comparison={}
try:
 host,hd=launch('host',390,844)
 wait(lambda:(hd/'host-lobby-ready.json').exists() or host.poll() is not None,40,'host lobby')
 if host.poll() is not None:raise RuntimeError('Host exited before lobby: '+str(read(hd/'result.json').get('error')))
 client,cd=launch('client',560,746)
 wait(lambda:host.poll() is not None and client.poll() is not None,420,'pair complete')
 for name,proc,d in [('host',host,hd),('client',client,cd)]:
  results[name]=read(d/'result.json')
  if proc.returncode!=0 or results[name].get('status')!='PASS':raise RuntimeError(name+' failed: '+str(results[name].get('error')))
 h,c=results['host'],results['client']; hp={(v['round'],v['revision']):v for v in h['proofs']};cp={(v['round'],v['revision']):v for v in c['proofs']};common=hp.keys()&cp.keys()
 assert len(common)>50,'Insufficient same-revision samples'
 mismatches=[key for key in common if hp[key]['hash']!=cp[key]['hash']];assert not mismatches,str(mismatches[:10])
 phases={hp[k]['phase'] for k in common};assert {'Ready','Playing','Victory','Defeat'}<=phases,str(phases)
 assert {k[0] for k in common}=={1,2}
 assert h['initialAcks']==2 and c['missingInitialAckBlocked'] and c['droppedReceipts']==1
 assert h['fullDurationDefeat'] and c['fullDurationDefeat']
 hc=[v for v in h['clocks'] if v['phase']=='Playing'];cc=[v for v in c['clocks'] if v['phase']=='Playing']
 stamp=lambda v:datetime.datetime.fromisoformat(v['utc'].replace('Z','+00:00')).timestamp()
 deviations=[]
 for v in cc:
  peers=[x for x in hc if x['round']==v['round']]
  if not peers:continue
  other=min(peers,key=lambda x:abs(stamp(x)-stamp(v)));dt=stamp(v)-stamp(other)
  if abs(dt)<=.75:deviations.append(abs(v['remaining']-(other['remaining']-dt)))
 assert len(deviations)>50 and max(deviations)<=1.0,(len(deviations),max(deviations,default=-1))
 comparison=dict(commonSnapshots=len(common),mismatches=[],phases=sorted(phases),rounds=[1,2],clockToleranceSeconds=1.0,clockSamples=len(deviations),clockMaxDifference=max(deviations),clockMedianDifference=statistics.median(deviations))
 print('Two-process gameplay, 2 rounds, matching revisions and clock: PASS',flush=True)
except Exception as e:error=str(e)
finally:
 for proc,log in children:
  if proc.poll() is not None:log.close()
 summary=dict(status='FAIL' if error else 'PASS',error=error,physicalDevice=False,executedUTC=datetime.datetime.now(datetime.timezone.utc).isoformat(),app=str(app),results={k:v.get('status') for k,v in results.items()},comparison=comparison)
 (out/'summary.json').write_text(json.dumps(summary,indent=2))
if error:raise SystemExit(error)
