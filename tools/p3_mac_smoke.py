#!/usr/bin/env python3
"""Build23 independent Mac processes. New probes gracefully self-exit; never kill user apps."""
import argparse, datetime, hashlib, json, pathlib, subprocess, time

def read(p):
    try:return json.loads(p.read_text())
    except (FileNotFoundError,json.JSONDecodeError):return {}
def utc():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def verify(results,n):
    players=[results[str(i)] for i in range(n)]; host=players[0]
    for i,r in enumerate(players):
        assert r['status']=='PASS',r.get('error')
        assert r['buildGuid']==host['buildGuid'] and r['session']==host['session'] and r['room']==host['room']
        assert r['roster']==host['roster'] and len(set(r['roster']))==n
        assert r['localPlayer']==r['roster'][i] and r['left']==(i+n-1)%n+1 and r['right']==(i+1)%n+1
        assert not r['physicalTouch'] and not r['bonjourValidated'] and r['inputScope'].startswith('DEBUG_POINTER')
        assert all(r[k] for k in ['lobbyReseat','clientHit','victory','retryClean','disconnectClean','initialAckGate'])
        assert r['transfers']==n*4+1
        assert r['paidGenerations']==(1 if n==5 else 2)+(1 if i==n-1 else 0)
        assert r['explicitRawFixtures']==(102 if n==5 else 2) if i==0 else r['explicitRawFixtures']==0
        assert r['explicitCombinedFixtures']==(104 if n==5 else 4) if i==0 else r['explicitCombinedFixtures']==0
        assert r['finalGame']['roundId']==2 and r['finalGame']['attack']['hp']==100
        if n==5:
            assert r['allStaminaVisible'] and r['capacityReceived'] and r['fullCapacitySimultaneouslyObserved'] and r['maximumObservedOrbs']==200 and r['maximumObservedFlying']==100
            assert r['maximumObservedJsonBytes']+5<=327680
            assert len(r['finalGame']['attack']['orbs'])==100 and not r['finalGame']['attack']['projectiles']
    assert host['unreadyStartRejected'] and len(host['recoveries'])==5
    hit=host['recoveries'][0]
    assert hit['attacker']==host['roster'][-1] and hit['added']==5 and hit['accepted'] and not hit['duplicate']
    assert all(not r['recoveries'] for r in players[1:])
    if n==5:assert results['5']['sixthRejected'] and results['5']['status']=='PASS'
    key=lambda p:(p['session'],p['round'],p['revision'])
    maps=[{key(p):p['hash'] for p in r['proofs']} for r in players]
    common=set.intersection(*(set(m) for m in maps)); assert len(common)>=10,('too few all-player snapshots',len(common))
    mismatch=[k for k in common if len({m[k] for m in maps})!=1];assert not mismatch,mismatch[:4]
    capacity_common=set.intersection(*({key(p) for p in r['proofs'] if p.get('activeOrbs')==200 and p.get('flying')==100} for r in players)) if n==5 else set()
    if n==5:assert capacity_common,'No common full-capacity observation across all five apps'
    return dict(allPlayerCommonSnapshots=len(common),mismatches=0,transfersPerObserver=n*4+1,actual3DHits=5,
        attackerOnlyRecovery=5,lobbyReseat=True,retry=True,battleDisconnect=True,sixthRejected=n==5,
        maxObservedJsonBytes=max(r['maximumObservedJsonBytes'] for r in players),capacity200=n==5,allPlayerFullCapacitySnapshots=len(capacity_common),
        pointerScope='Synthetic controller pointer over real frames; NOT physical Touch')

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--app',required=True);ap.add_argument('--output',required=True);ap.add_argument('--count',type=int,choices=[2,3,4,5],required=True);ap.add_argument('--port',type=int,default=25153);a=ap.parse_args()
    app=pathlib.Path(a.app).resolve();out=pathlib.Path(a.output);assert app.is_file() and out.is_absolute() and not out.exists()
    out.mkdir(parents=True);children=[];results={};error=None;comparison={};started=utc();deadline=time.monotonic()+260
    def wait(test,seconds,label):
        until=min(deadline,time.monotonic()+seconds)
        while not test():
            if time.monotonic()>until:raise RuntimeError(label+' timeout')
            time.sleep(.1)
    def launch(i):
        directory=out/str(i);log=(out/f'{i}-stdout.log').open('w')
        command=[str(app),'-screen-fullscreen','0','-screen-width','320','-screen-height','700','-c6P3Output',str(directory),'-c6P3Index',str(i),'-c6P3Count',str(a.count),'-c6P3Port',str(a.port),'-logFile',str(out/f'{i}-unity.log')]
        p=subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT);children.append((i,p,log,directory));(out/f'{i}-launch.json').write_text(json.dumps(dict(command=command,pid=p.pid),indent=2));return p,directory
    try:
        for i in range(a.count):
            p,d=launch(i);wait(lambda:(out/f'joined-{i}.json').exists() or p.poll() is not None,25,f'join {i}')
            if p.poll() is not None:raise RuntimeError(f'{i} exited: '+str(read(d/'result.json').get('error')))
        if a.count==5:launch(5)
        wait(lambda:all(p.poll() is not None for _,p,_,_ in children),235,'all probes complete')
        for i,p,_,d in children:
            results[str(i)]=read(d/'result.json');assert p.returncode==0 and results[str(i)].get('status')=='PASS',f'{i} failed: '+str(results[str(i)].get('error'))
            if i<a.count:
                for name in ['01-lobby-ready','02-playing','03-client-real-hit','04-victory']+(['05-capacity'] if a.count==5 else []):
                    assert (d/(name+'.png')).stat().st_size>1000
        comparison=verify(results,a.count)
    except Exception as e:error=str(e)
    finally:
        running=[]
        for i,p,log,d in children:
            if p.poll() is None:running.append(dict(index=i,pid=p.pid))
            else:log.close()
            results[str(i)]=read(d/'result.json')
        summary=dict(status='FAIL' if error else 'PASS',error=error,participants=a.count,startedAtUtc=started,finishedAtUtc=utc(),app=str(app),appSha256=digest(app),runnerSha256=digest(pathlib.Path(__file__)),comparison=comparison,results={k:v.get('status','NOT_RUN') for k,v in results.items()},processesLeftRunning=running,physicalTouch=False,bonjourValidated=False,processPolicy='New diagnostic processes only, 230s self-deadline with graceful Application.Quit; no force kill.')
        (out/'summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2),flush=True)
    if error:raise SystemExit(1)
if __name__=='__main__':main()
