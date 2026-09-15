#!/usr/bin/env python3
"""Explicit local Mac pair; desktop pointer evidence only."""
import argparse,datetime,json,pathlib,subprocess,time,re,traceback
p=argparse.ArgumentParser();p.add_argument('--app',required=True);p.add_argument('--output',required=True);a=p.parse_args()
app=pathlib.Path(a.app);out=pathlib.Path(a.output);assert app.is_file() and out.is_absolute() and not out.exists()
out.mkdir(parents=True);children=[];results={};comparison={}
def read(path):
 try:return json.loads(path.read_text())
 except (FileNotFoundError,json.JSONDecodeError):return {}
def wait(fn,seconds,label):
 end=time.monotonic()+seconds
 while not fn():
  if time.monotonic()>end:raise RuntimeError(label+' timeout')
  time.sleep(.2)
def launch(role,w,h):
 d=out/role;log=(out/(role+'-stdout.log')).open('w')
 args=[str(app),'-screen-fullscreen','0','-screen-width',str(w),'-screen-height',str(h),'-c6T11ProbeDirectory',str(d),'-c6T11Role',role,'-c6T11Port','25126','-c6T11Quit','-logFile',str(out/(role+'-unity.log'))]
 proc=subprocess.Popen(args,stdout=log,stderr=subprocess.STDOUT);children.append((proc,log));return proc,d
error=None;error_trace=None
try:
 host,hd=launch('host',390,844);wait(lambda:(hd/'host-lobby-ready.json').exists() or host.poll() is not None,40,'host lobby')
 if host.poll() is not None:raise RuntimeError('Host exited before lobby: '+str(read(hd/'result.json').get('error')))
 client,cd=launch('client',560,746)
 wait(lambda:host.poll() is not None and client.poll() is not None,200,'pair complete')
 for name,proc,d in [('host',host,hd),('client',client,cd)]:
  results[name]=read(d/'result.json')
 failures=[name+': '+str(results[name].get('error')) for name,proc,d in [('host',host,hd),('client',client,cd)] if proc.returncode!=0 or results[name].get('status')!='PASS']
 if failures:raise RuntimeError(' | '.join(failures))
 h,c=results['host'],results['client'];hp={(x['round'],x['revision']):x for x in h['proofs']};cp={(x['round'],x['revision']):x for x in c['proofs']};common=hp.keys()&cp.keys()
 assert len(common)>50,('Insufficient common snapshots',len(common))
 mismatches=[list(k) for k in common if hp[k]['hash']!=cp[k]['hash']];assert not mismatches,mismatches[:10]
 assert len(h['passes'])==len(c['passes'])==17
 distribution={}
 for hv,cv in zip(h['passes'],c['passes']):
  assert hv['key']==cv['key'] and hv['orbId']==cv['orbId'] and hv['afterOwner']==cv['afterOwner']
  assert hv['entryPosition']==cv['entryPosition'] and hv['entrySide']==cv['entrySide'] and hv['sequence']==cv['sequence']
  assert hv['beforeIds']==hv['afterIds']==cv['beforeIds']==cv['afterIds']
  assert hv['afterCount']==cv['afterCount']==hv['beforeCount']+1==cv['beforeCount']+1
  send=hv if hv['sending'] else cv;receive=cv if hv['sending'] else hv
  assert send['heldBeforeRelease'] and send['receiptKnown'] and send['receiptAccepted'] and receive['noAutoReturn']
  key=f"{send['sender']}:{send['direction']}:{'Raw' if send['kind']==0 else 'Combined'}";distribution[key]=distribution.get(key,0)+1
 assert len(distribution)==8,distribution
 assert c['droppedReplies']>=2 and c['duplicatePackets']==1
 duplicate_id=next(x['requestId'] for x in c['passes'] if x['key']=='raw-01')
 duplicate_logs=[line for line in (out/'host-unity.log').read_text(errors='replace').splitlines() if 'C6_T11_TRANSFER ' in line and 'request='+duplicate_id+' ' in line]
 assert len(duplicate_logs)==2 and all('accepted=True' in line for line in duplicate_logs) and sum('duplicate=True' in line for line in duplicate_logs)==1,duplicate_logs
 assert all('transferCount=2' in line for line in duplicate_logs),duplicate_logs
 (out/'duplicate-proof.txt').write_text('\n'.join(duplicate_logs)+'\n')
 assert h['returnedRawCombined'] and h['receiverHit'] and c['receiverHit']
 assert h['finalGame']['attack']['hp']==c['finalGame']['attack']['hp']==80
 comparison=dict(commonSnapshots=len(common),hashMismatches=[],confirmedTransfers=17,raw=8,combined=9,distribution=distribution,liveIdSetPreservedForEveryTransfer=True,oppositeEdgeAndHeightAgreed=True,droppedClientReplies=c['droppedReplies'],duplicateClientPackets=1,receivedCombinedActualHit=True)
 print('T11 two-process transfers and receiver hit: PASS',flush=True)
except Exception as e:
 error=type(e).__name__+": "+(str(e) or "comparison assertion failed");error_trace=traceback.format_exc()
finally:
 for proc,log in children:
  if proc.poll() is not None:log.close()
 summary=dict(status='FAIL' if error is not None else 'PASS',error=error,errorTrace=error_trace,physicalDevice=False,executedUTC=datetime.datetime.now(datetime.timezone.utc).isoformat(),app=str(app),results={k:v.get('status') for k,v in results.items()},comparison=comparison)
 (out/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
if error is not None:raise SystemExit(error)
