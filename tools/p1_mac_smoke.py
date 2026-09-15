#!/usr/bin/env python3
"""Build21 P1: two new Mac processes, synthetic controller pointers, no process killing."""
import argparse
import datetime
import hashlib
import json
import pathlib
import subprocess
import time


def digest(path):
    with path.open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def read(path):
    try:
        return json.loads(path.read_text())
    except (FileNotFoundError, json.JSONDecodeError):
        return {}


def utc():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def verify(host, client):
    assert host['buildGuid'] and host['buildGuid'] == client['buildGuid'], 'Build GUID mismatch'
    assert host['session'] == client['session'] and host['room'] == client['room'], 'Session mismatch'
    assert host['localPlayer'] != client['localPlayer'], 'Both processes reported the same player'
    for role, result in [('host', host), ('client', client)]:
        assert result['status'] == 'PASS', result.get('error')
        assert result['physicalDevice'] is False and result['bonjourValidated'] is False
        assert result['normalGenerated'] == 1 and abs(result['normalGenerateCost'] - 20) < 1e-5
        assert result['coastPreserved'] and result['cancelStopped'] and result['coastDistance'] > .001
        assert result['releaseVelocity']['x'] > .01
        assert result['sharedHp80'] and result['finalClean']
        snap = result['finalGame']
        assert snap['battle']['duration'] == 180 and snap['battle']['phase'] == 'Playing'
        assert snap['attack']['hp'] == 80 and snap['attack']['roundHits'] == 1
        assert len(snap['attack']['orbs']) == 2, 'Expected only the two original paid Raw orbs after fixture hit'
        assert all(player['generatedTotal'] == 1 for player in snap['resources']['players'])
        assert len(result['transfers']) == 2
        for item in result['transfers']:
            assert item['orbId'] == host['normalOrbId']
            if item['receiver']:
                assert item['receivedStopped'] and item['owner'] == result['localPlayer']
        assert result['explicitFixtureRawCount'] == (2 if role == 'host' else 0)
    assert host['actualHostProjectileObserved'] and not client['actualHostProjectileObserved']
    assert host['combinedId'] == client['combinedId']
    for hp, cp in zip(host['transfers'], client['transfers']):
        for field in ['orbId', 'count', 'owner', 'entrySide', 'direction']:
            assert hp[field] == cp[field], f'Transfer {field} mismatch'
        assert hp['receiver'] != cp['receiver']
    key = lambda p: (p['session'], p['round'], p['revision'])
    host_proofs = {key(point): point['hash'] for point in host['proofs']}
    client_proofs = {key(point): point['hash'] for point in client['proofs']}
    common = host_proofs.keys() & client_proofs.keys()
    assert len(common) >= 10, f'Too few paired authoritative revisions: {len(common)}'
    mismatches = [entry for entry in common if host_proofs[entry] != client_proofs[entry]]
    assert not mismatches, f'Logical state mismatch: {mismatches[:5]}'
    return dict(commonSnapshots=len(common), commonMismatchCount=0, transfers=2,
                normalPaidGenerations=2, explicitRawGeometryFixtures=2, actualHost3DHits=1,
                hostCoastDistance=host['coastDistance'], clientCoastDistance=client['coastDistance'],
                localBodyPositionsCompared=False,
                positionScope='Owner-local presentation positions differ by design; authenticated logical state hashes compared.')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--app', required=True, help='Build21 .app/Contents/MacOS executable')
    parser.add_argument('--output', required=True, help='New absolute evidence directory')
    parser.add_argument('--port', type=int, default=25141)
    args = parser.parse_args()
    app = pathlib.Path(args.app).resolve()
    out = pathlib.Path(args.output)
    assert app.is_file() and out.is_absolute() and not out.exists()
    assert 1024 <= args.port <= 65535
    out.mkdir(parents=True)
    children = []
    results = {}
    comparison = {}
    error = None
    started = utc()
    deadline = time.monotonic() + 90

    def wait(predicate, seconds, label):
        until = min(deadline, time.monotonic() + seconds)
        while not predicate():
            if time.monotonic() >= until:
                raise RuntimeError(label + ' timeout')
            time.sleep(.1)

    def launch(role, width, height):
        directory = out / role
        log = (out / (role + '-stdout.log')).open('w')
        command = [str(app), '-screen-fullscreen', '0', '-screen-width', str(width), '-screen-height', str(height),
                   '-c6P1ProbeDirectory', str(directory), '-c6P1Role', role, '-c6P1Port', str(args.port),
                   '-c6P1Quit', '-logFile', str(out / (role + '-unity.log'))]
        process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT)
        children.append((role, process, log, directory))
        (out / (role + '-launch.json')).write_text(json.dumps(dict(command=command, pid=process.pid), indent=2))
        return process, directory

    try:
        host, hd = launch('host', 390, 844)
        wait(lambda: (out / 'host-open.json').exists() or host.poll() is not None, 20, 'Host lobby')
        if host.poll() is not None:
            raise RuntimeError('Host exited before room: ' + str(read(hd / 'result.json').get('error')))
        client, cd = launch('client', 560, 746)
        wait(lambda: host.poll() is not None and client.poll() is not None, 85, 'P1 pair completion')
        for role, process, _, directory in children:
            results[role] = read(directory / 'result.json')
            if process.returncode != 0 or results[role].get('status') != 'PASS':
                raise RuntimeError(role + ' failed: ' + str(results[role].get('error')))
            for label in ['01-normal-coasting', '02-shared-real-hit']:
                screenshot = directory / (label + '.png')
                assert screenshot.is_file() and screenshot.stat().st_size > 1000, f'{role} {label} screenshot missing'
        comparison = verify(results['host'], results['client'])
    except Exception as exception:
        error = str(exception)
    finally:
        running = []
        for role, process, log, directory in children:
            if process.poll() is None:
                running.append(dict(role=role, pid=process.pid))
            else:
                log.close()
            if role not in results:
                results[role] = read(directory / 'result.json')
        summary = dict(status='FAIL' if error else 'PASS', error=error, startedAtUtc=started, finishedAtUtc=utc(),
                       scope='BUILD21_MAC_DIRECT_IP_LOCAL_2D_PHYSICS_AND_EXISTING_HOST_3D_HIT',
                       physicalDevice=False, physicalTouch=False, bonjourValidated=False,
                       p2ReleaseBased3DThrowImplemented=False,
                       explicitFixtureScope='Host adds two named Raw geometry fixtures after both normal Generate/coast and two passes.',
                       app=str(app), appExecutableSha256=digest(app), runnerSha256=digest(pathlib.Path(__file__)),
                       comparison=comparison, results={role: result.get('status', 'NOT_RUN') for role, result in results.items()},
                       processesLeftRunning=running,
                       processPolicy='New probes only; 70-second self-deadline and graceful Application.Quit; runner overall 90 seconds; no process kill.')
        (out / 'summary.json').write_text(json.dumps(summary, indent=2))
        print(json.dumps(summary, indent=2), flush=True)
    if error:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
