#!/usr/bin/env python3
"""Explicitly owned Mac pair only. No device, discovery, or physical-touch PASS."""
import argparse
import datetime
import hashlib
import json
import pathlib
import subprocess
import time


def sha(path):
    with path.open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def read(path):
    try:
        return json.loads(path.read_text())
    except (FileNotFoundError, json.JSONDecodeError):
        return {}


def wait(predicate, seconds, label):
    until = time.monotonic() + seconds
    while not predicate():
        if time.monotonic() >= until:
            raise RuntimeError(label + ' timeout')
        time.sleep(.15)


def compare(host, client):
    assert host['buildGuid'] == client['buildGuid'] and host['buildGuid'], 'Build GUID differs'
    for result in [host, client]:
        assert result['status'] == 'PASS', result.get('error')
        assert result['physicalDevice'] is False and result['bonjourValidated'] is False
        assert result['gameplayStateInjected'] is False
        assert result['syntheticFocusMaintained'] and result['noAutomaticReconnect']
        assert result['previousSessionRequestHadNoEffect'] and result['finalClean']
        assert result['firstSession'] != result['secondSession']
        assert result['firstRoom'] != result['secondRoom']
        assert result['firstNonce'] != result['secondNonce']
        points = {entry['name']: entry for entry in result['checkpoints']}
        for name in ['watchdog-clean-lobby', 'synthetic-pause-clean-lobby']:
            assert points[name]['clean'] and points[name]['localViews'] == 0
        for name in ['epoch-1-playing-empty-full', 'epoch-2-playing-empty-full']:
            snap = points[name]['snapshot']
            assert snap['battle']['duration'] == 180 and snap['roundId'] == 1
            assert snap['attack']['hp'] == 100 and not snap['attack']['orbs']
            assert all(p['stamina'] == 100 and p['generatedTotal'] == 0 for p in snap['resources']['players'])
    assert host['firstSession'] == client['firstSession']
    assert host['secondSession'] == client['secondSession']
    assert host['firstRoom'] == client['firstRoom'] and host['secondRoom'] == client['secondRoom']
    assert host['firstNonce'] != client['firstNonce'] and host['secondNonce'] != client['secondNonce']
    assert 7 <= host['faultToErrorSeconds'] <= 13
    assert host['rejectedControlsAtWatchdog'] > host['rejectedControlsBeforeFault']
    assert client['invalidControlsSent'] > 0
    assert host['watchdogInterruption'] == 'PEER_GAME_RESPONSE_TIMEOUT'
    assert client['previousSessionRequestsSent'] == 1
    assert client['syntheticLobbyPauseInvoked'] and not host['syntheticLobbyPauseInvoked']
    key = lambda point: (point['session'], point['round'], point['revision'])
    hp = {key(point): point for point in host['proofs']}
    cp = {key(point): point for point in client['proofs']}
    common = hp.keys() & cp.keys()
    mismatches = [point for point in common if hp[point]['hash'] != cp[point]['hash']]
    assert not mismatches, f'Common state mismatch: {mismatches[:5]}'
    counts = {session: sum(1 for point in common if point[0] == session)
              for session in [host['firstSession'], host['secondSession']]}
    assert all(count >= 10 for count in counts.values()), f'Insufficient common revisions: {counts}'
    return dict(commonSnapshots=len(common), perSessionCommonSnapshots=counts,
                commonMismatchCount=0, validResponseWatchdogObservedSeconds=host['faultToErrorSeconds'],
                invalidControlsSent=client['invalidControlsSent'],
                maximumRejectedControls=host['maximumRejectedControls'],
                rejectedControlsBeforeFault=host['rejectedControlsBeforeFault'],
                rejectedControlsAtWatchdog=host['rejectedControlsAtWatchdog'],
                rejectedControlsDuringFault=host['rejectedControlsAtWatchdog'] - host['rejectedControlsBeforeFault'],
                oldSessionRequestsSent=client['previousSessionRequestsSent'],
                watchdogReasons={role: report['watchdogInterruption'] for role, report in [('host', host), ('client', client)]},
                pauseReasons={role: report['pauseInterruption'] for role, report in [('host', host), ('client', client)]})


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--app', required=True, help='Mac .app/Contents/MacOS executable')
    parser.add_argument('--output', required=True, help='New absolute evidence directory')
    parser.add_argument('--port', type=int, default=25133)
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
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()

    def launch(role, width, height):
        directory = out / role
        log = (out / (role + '-stdout.log')).open('w')
        command = [str(app), '-screen-fullscreen', '0', '-screen-width', str(width), '-screen-height', str(height),
                   '-c6T13ProbeDirectory', str(directory), '-c6T13Role', role, '-c6T13Port', str(args.port),
                   '-c6T13ProbeQuit', '-logFile', str(out / (role + '-unity.log'))]
        process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT)
        children.append((role, process, log, directory))
        (out / (role + '-launch.json')).write_text(json.dumps(dict(command=command, pid=process.pid), indent=2))
        return process, directory

    try:
        host, hd = launch('host', 390, 844)
        wait(lambda: (hd / 'host-open-1.json').exists() or host.poll() is not None, 40, 'Host lobby')
        if host.poll() is not None:
            raise RuntimeError('Host exited before first room: ' + str(read(hd / 'result.json').get('error')))
        client, cd = launch('client', 560, 746)
        wait(lambda: host.poll() is not None and client.poll() is not None, 150, 'T13 Mac pair')
        for role, process, _, directory in children:
            results[role] = read(directory / 'result.json')
            if process.returncode != 0 or results[role].get('status') != 'PASS':
                raise RuntimeError(role + ' failed: ' + str(results[role].get('error')))
        comparison = compare(results['host'], results['client'])
    except Exception as exception:
        error = str(exception)
    finally:
        running = []
        for role, process, log, directory in children:
            if process.poll() is not None:
                log.close()
            else:
                running.append(dict(role=role, pid=process.pid))
            if role not in results:
                results[role] = read(directory / 'result.json')
        summary = dict(status='FAIL' if error else 'PASS', error=error, startedAtUtc=started,
                       finishedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                       scope='MAC_TWO_PROCESS_DIRECT_IP_SYNTHETIC_INTERRUPTION_CALLBACKS',
                       physicalDevice=False, bonjourValidated=False, gameplayStateInjected=False,
                       app=str(app), appExecutableSha256=sha(app), runnerSha256=sha(pathlib.Path(__file__)),
                       results={role: value.get('status', 'NOT_RUN') for role, value in results.items()},
                       comparison=comparison, processesLeftRunning=running,
                       processPolicy='Only newly launched probes; no existing processes killed; no timeout counted PASS.')
        (out / 'summary.json').write_text(json.dumps(summary, indent=2))
        print(json.dumps(summary, indent=2), flush=True)
    if error:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
