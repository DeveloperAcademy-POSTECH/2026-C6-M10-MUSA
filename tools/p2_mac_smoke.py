#!/usr/bin/env python3
"""Build22: two new Mac processes, explicit pointer/Combined fixtures, graceful exit only."""
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
    assert host['hitOrbId'] == client['hitOrbId'] and host['missOrbId'] == client['missOrbId']
    assert host['hitOrbId'] != host['missOrbId'], 'Hit and miss reused an identity'
    for role, result, size, generations in [('host', host, (390, 844), 2), ('client', client, (560, 746), 1)]:
        assert result['status'] == 'PASS', result.get('error')
        assert result['physicalDevice'] is False and result['bonjourValidated'] is False
        assert result['pointerScope'].startswith('DEBUG_POINTER:')
        assert (result['width'], result['height']) == size, 'Unexpected player viewport dimensions'
        assert result['normalGenerated'] == generations and abs(result['normalGenerateCost'] - 20) < 1e-5
        assert result['heldUpperPreserved'] and result['cancelPreserved']
        assert result['framingFits'] and result['framingStatus'] == 'TARGET_CLEAR_OF_HUD'
        assert result['syntheticMovingSamples'] >= 2, 'Throw did not use multiple real-frame samples'
        assert result['sharedHp80'] and result['sharedMissConsumed'] and result['finalClean']
        assert result['explicitFixtureCombinedCount'] == (2 if role == 'host' else 0)
        snap = result['finalGame']
        assert snap['battle']['duration'] == 180 and snap['battle']['phase'] == 'Playing'
        assert snap['attack']['hp'] == 80 and snap['attack']['roundHits'] == 1
        assert len(snap['attack']['projectiles']) == 0
        assert len(snap['attack']['orbs']) == 3, 'Expected only three paid Raw orbs after two fixtures consumed'
        assert sorted(player['generatedTotal'] for player in snap['resources']['players']) == [1, 2]
        assert not snap['resources']['debugTestMode'], 'Normal resource mode was changed'
        assert all(orb['id'] not in {host['hitOrbId'], host['missOrbId']} for orb in snap['attack']['orbs'])
    motions = {item['orbId']: item for item in host['motions']}
    assert set(motions) == {host['hitOrbId'], host['missOrbId']}, 'Expected exactly two real Host projectiles'
    assert not client['motions'] and client['clientDisplayFrames'] > 1, 'Client must show presentation without Host physics'
    for item in motions.values():
        assert item['ballistic'] and item['dynamicBody'] and item['samples'] >= 2
        assert item['initialVelocity']['y'] > 0 and item['initialVelocity']['z'] > 0 and item['gravity']['y'] < 0
    miss = motions[host['missOrbId']]
    assert miss['attacker'] == client['localPlayer'] and miss['initialVelocity']['x'] > 0
    assert miss['groundContactObserved'] and miss['bounceObserved'] and miss['maximumCollisionCallbacks'] >= 1
    assert motions[host['hitOrbId']]['attacker'] == host['localPlayer']
    assert len(host['recoveries']) == 1 and not client['recoveries'], 'Only Host actual-hit recovery receipt is expected'
    reward = host['recoveries'][0]
    assert reward['orbId'] == host['hitOrbId'] and reward['attacker'] == host['localPlayer']
    assert reward['accepted'] and not reward['duplicate'] and abs(reward['added'] - 5) < 1e-5
    key = lambda point: (point['session'], point['round'], point['revision'])
    host_proofs = {key(point): point['hash'] for point in host['proofs']}
    client_proofs = {key(point): point['hash'] for point in client['proofs']}
    common = host_proofs.keys() & client_proofs.keys()
    assert len(common) >= 10, f'Too few paired authoritative revisions: {len(common)}'
    mismatches = [entry for entry in common if host_proofs[entry] != client_proofs[entry]]
    assert not mismatches, f'Logical state mismatch: {mismatches[:5]}'
    return dict(commonSnapshots=len(common), commonMismatchCount=0, normalPaidGenerations=3,
                explicitCombinedFixtures=2, actualHost3DHits=1, validLateralMisses=1, actualAttackerRecovery=5,
                hostGroundCollisionCallbacks=miss['maximumCollisionCallbacks'], clientDisplayFrames=client['clientDisplayFrames'],
                heldAndCancelOwners=2, framingViewportSizes=[[390, 844], [560, 746]],
                localBodyPositionsCompared=False, physicalTouch=False)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--app', required=True, help='Build22 .app/Contents/MacOS executable')
    parser.add_argument('--output', required=True, help='New absolute evidence directory')
    parser.add_argument('--port', type=int, default=25142)
    args = parser.parse_args()
    app = pathlib.Path(args.app).resolve()
    out = pathlib.Path(args.output)
    assert app.is_file() and out.is_absolute() and not out.exists()
    assert 1024 <= args.port <= 65535
    out.mkdir(parents=True)
    children, results, comparison = [], {}, {}
    error = None
    started = utc()
    deadline = time.monotonic() + 120

    def wait(predicate, seconds, label):
        until = min(deadline, time.monotonic() + seconds)
        while not predicate():
            if time.monotonic() >= until:
                raise RuntimeError(label + ' timeout')
            time.sleep(.1)

    def launch(role, width, height, x):
        directory = out / role
        log = (out / (role + '-stdout.log')).open('w')
        command = [str(app), '-screen-fullscreen', '0', '-screen-width', str(width), '-screen-height', str(height),
                   '-c6P2ProbeDirectory', str(directory), '-c6P2Role', role, '-c6P2Port', str(args.port),
                   '-c6P2WindowX', str(x), '-c6P2WindowY', '60', '-c6P2Quit', '-logFile', str(out / (role + '-unity.log'))]
        process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT)
        children.append((role, process, log, directory))
        (out / (role + '-launch.json')).write_text(json.dumps(dict(command=command, pid=process.pid,
            requestedWindowPosition=[x, 60], windowPlacement='Probe calls Unity Screen.MoveMainWindowTo on its own new window.'), indent=2))
        return process, directory

    try:
        host, hd = launch('host', 390, 844, 20)
        wait(lambda: (out / 'host-open.json').exists() or host.poll() is not None, 20, 'Host lobby')
        if host.poll() is not None:
            raise RuntimeError('Host exited before room: ' + str(read(hd / 'result.json').get('error')))
        client, cd = launch('client', 560, 746, 450)
        wait(lambda: host.poll() is not None and client.poll() is not None, 105, 'P2 pair completion')
        for role, process, _, directory in children:
            results[role] = read(directory / 'result.json')
            if process.returncode != 0 or results[role].get('status') != 'PASS':
                raise RuntimeError(role + ' failed: ' + str(results[role].get('error')))
            for label in ['01-normal-framing', '02-held-upper-preview', '03-center-projectile',
                          '04-shared-real-hit', '05-lateral-projectile', '06-expired-and-clean-inventory']:
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
                       scope='BUILD22_MAC_DIRECT_IP_RELEASE_THROW_ACTUAL_HOST_PHYSICS_CLIENT_PRESENTATION',
                       physicalDevice=False, physicalTouch=False, bonjourValidated=False,
                       explicitFixtureScope='Normal two-player room and paid Generate; Host adds one Combined per actual owner for DEBUG_POINTER throw geometry.',
                       app=str(app), appExecutableSha256=digest(app), runnerSha256=digest(pathlib.Path(__file__)),
                       comparison=comparison, results={role: result.get('status', 'NOT_RUN') for role, result in results.items()},
                       processesLeftRunning=running,
                       processPolicy='New probes only; 100-second self-deadline and graceful Application.Quit; runner overall 120 seconds; no process kill.')
        (out / 'summary.json').write_text(json.dumps(summary, indent=2))
        print(json.dumps(summary, indent=2), flush=True)
    if error:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
