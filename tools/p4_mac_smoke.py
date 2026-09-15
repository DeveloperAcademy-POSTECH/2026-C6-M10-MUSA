#!/usr/bin/env python3
"""Build24 continuous-edge checks using separate Mac apps; never terminate user processes."""
import argparse
import collections
import datetime
import hashlib
import json
import math
import pathlib
import subprocess
import time

SPEED_TOLERANCE = 0.001
HEIGHT_TOLERANCE = 0.00001
PROBE_DEADLINE_SECONDS = 170
RUNNER_DEADLINE_SECONDS = 210


def read(path):
    try:
        return json.loads(path.read_text())
    except (FileNotFoundError, json.JSONDecodeError):
        return {}


def utc():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def motion_events(result):
    events = []
    for ordinal, line in enumerate(result.get('motionLogs', [])):
        parts = line.split()
        require(parts and parts[0] in ('C6_P4_EDGE_CAPTURE', 'C6_P4_RESUME'), f'Unknown motion event: {line}')
        event = dict(part.split('=', 1) for part in parts[1:])
        event.update(type=parts[0], observer=result['index'], localPlayer=result['localPlayer'], ordinal=ordinal)
        for name in ('vx', 'vy', 'height', 'serverTime'):
            event[name] = float(event[name])
            require(math.isfinite(event[name]), f'Nonfinite motion {name}: {line}')
        require(0 <= event['height'] <= 1 and event['serverTime'] >= 0, f'Invalid motion coordinate/time: {line}')
        event['speed'] = math.hypot(event['vx'], event['vy'])
        if event['type'] == 'C6_P4_RESUME':
            for name in ('count', 'owner', 'entry'):
                event[name] = int(event[name])
            require(event['owner'] == result['localPlayer'] and event.get('playing') == 'True', f'Resume on wrong/nonplaying owner: {line}')
        else:
            require(event.get('source') == 'PHYSICS' and event.get('request'), f'Capture is not physical edge request: {line}')
        events.append(event)
    return events


def compare_motion_pass(pass_record, players, events):
    orb_id, right, crossings = pass_record['id'], pass_record['right'], pass_record['crossings']
    roster = players[0]['roster']
    captures = collections.defaultdict(list)
    resumes = {}
    requests = set()
    last_resume_by_observer = {}
    for event in events:
        if event['orb'] != orb_id:
            continue
        if event['type'] == 'C6_P4_EDGE_CAPTURE':
            require(event['request'] not in requests, f'Duplicate edge request for {orb_id}')
            requests.add(event['request'])
            captures[event['localPlayer']].append(event)
        else:
            require(event['count'] > last_resume_by_observer.get(event['observer'], 0), f'{orb_id}: resume count regressed on one app')
            last_resume_by_observer[event['observer']] = event['count']
            require(event['count'] not in resumes, f'Duplicate physical resume epoch {orb_id}/{event["count"]}')
            resumes[event['count']] = event
    require(len(requests) == crossings, f'{orb_id}: capture count {len(requests)} != approved crossings {crossings}')
    require(set(resumes) == set(range(1, crossings + 1)), f'{orb_id}: missing/repeated resume epochs')
    capture_cursor = collections.Counter()
    pairs, previous_resume, max_increase = [], None, 0.0
    for step in range(1, crossings + 1):
        offset = step - 1 if right else -(step - 1)
        source_owner = roster[offset % len(roster)]
        target_owner = roster[(offset + (1 if right else -1)) % len(roster)]
        cursor = capture_cursor[source_owner]
        require(cursor < len(captures[source_owner]), f'{orb_id}/{step}: missing source-ring capture')
        capture = captures[source_owner][cursor]
        capture_cursor[source_owner] += 1
        resume = resumes[step]
        direction = 'TransferRight' if right else 'TransferLeft'
        require(capture['direction'] == direction, f'{orb_id}/{step}: wrong edge direction')
        require((capture['vx'] > 0) if right else (capture['vx'] < 0), f'{orb_id}/{step}: reflected capture velocity')
        require(resume['owner'] == target_owner and resume['entry'] == (1 if right else 2), f'{orb_id}/{step}: wrong opposite-edge receiver')
        require((resume['vx'] >= 0) if right else (resume['vx'] <= 0), f'{orb_id}/{step}: reflected arrival velocity')
        require(abs(capture['height'] - resume['height']) <= HEIGHT_TOLERANCE, f'{orb_id}/{step}: normalized arrival height changed')
        require(resume['serverTime'] + .25 >= capture['serverTime'], f'{orb_id}/{step}: receiver time precedes capture beyond clock tolerance')
        increase = resume['speed'] - capture['speed']
        max_increase = max(max_increase, increase)
        require(increase <= SPEED_TOLERANCE, f'{orb_id}/{step}: network arrival added speed {increase}')
        # With one moving orb and no contacts along this horizontal fixture, each subsequent
        # capture must have lost speed through friction, rather than resetting to release speed.
        if previous_resume is not None:
            require(capture['serverTime'] + .25 >= previous_resume['serverTime'], f'{orb_id}/{step}: later capture time regressed')
            increase = capture['speed'] - previous_resume['speed']
            max_increase = max(max_increase, increase)
            require(increase <= SPEED_TOLERANCE, f'{orb_id}/{step}: next screen added speed {increase}')
        if capture['speed'] > SPEED_TOLERANCE and resume['speed'] > SPEED_TOLERANCE:
            cross = capture['vx'] * resume['vy'] - capture['vy'] * resume['vx']
            require(abs(cross) <= SPEED_TOLERANCE * max(1.0, capture['speed'] * resume['speed']), f'{orb_id}/{step}: handoff rotated velocity')
        pairs.append(dict(count=step, source=source_owner, receiver=target_owner, entry=resume['entry'],
            captureSpeed=capture['speed'], resumeSpeed=resume['speed'], normalizedHeight=capture['height'],
            capturedServerTime=capture['serverTime'], resumedServerTime=resume['serverTime'], request=capture['request']))
        previous_resume = resume
    return dict(id=orb_id, kind=pass_record['kind'], direction='Right' if right else 'Left', crossings=crossings,
        finalOwner=pass_record['finalOwner'], stopped=True, singlePush=True, pairs=pairs, maximumSpeedIncrease=max_increase)


def verify(results, count):
    players = [results[str(index)] for index in range(count)]
    host = players[0]
    require(host.get('buildGuid') and host.get('session'), 'Missing build/session identity')
    require(len(host['roster']) == count and len(set(host['roster'])) == count and host['roster'][0] == 0, 'Invalid Host-first roster')
    reference_passes = host['passes']
    require(len(reference_passes) == 2, 'Expected Raw right and Combined left passes')
    require([(p['kind'], p['right']) for p in reference_passes] == [('Raw', True), ('Combined', False)], 'Wrong pass fixture order')
    require(len({p['id'] for p in reference_passes}) == 2, 'Raw and Combined fixtures reuse an ID')
    for index, result in enumerate(players):
        require(result['status'] == 'PASS', f'{index}: {result.get("error")}')
        require(result['index'] == index and result['participants'] == count, f'{index}: participant identity differs')
        require(result['buildGuid'] == host['buildGuid'] and result['session'] == host['session'], f'{index}: build/session mismatch')
        require(result['roster'] == host['roster'] and result['localPlayer'] == host['roster'][index], f'{index}: roster differs')
        require(result['left'] == (index + count - 1) % count + 1 and result['right'] == (index + 1) % count + 1, f'{index}: wrong ring neighbor')
        require(result['width'] == (320 if index % 2 == 0 else 390) and result['height'] == 700, f'{index}: requested aspect not used')
        require(result['scope'] == 'BUILD24_MAC_DIRECT_IP_EXPLICIT_RAW_COMBINED_CAPACITY_FIXTURES_DEBUG_POINTER', f'{index}: unsupported test scope')
        require(not result['physicalTouch'] and not result['bonjourValidated'], f'{index}: automated result claims physical/Bonjour validation')
        require(result['retryClean'] and result['fullRecipientStopped'], f'{index}: cleanup/capacity probe incomplete')
        require(result['pushes'] == (3 if index == 0 else 0), f'{index}: unexpected additional push')
        if index == 0:
            require(result['heldRetained'], 'Held orb retention was not checked')
        require(result['passes'] == reference_passes, f'{index}: observers disagree on same-ID stopped passes')
        for record in result['passes']:
            require(record['crossings'] >= 2 and record['stopped'] and record['singlePush'], f'{index}: pass did not roll across multiple screens then stop')
            offset = record['crossings'] if record['right'] else -record['crossings']
            require(record['finalOwner'] == host['roster'][offset % count], f'{index}: final ring owner differs')
        final = result['finalGame']
        require(final['sessionId'] == host['session'] and final['roundId'] == 2 and final['continuousTransfers'], f'{index}: final retry context differs')
        require(final['roomId'] == host['finalGame']['roomId'], f'{index}: final room differs')
        require(final['attack']['hp'] == 100 and not final['attack']['projectiles'], f'{index}: passive physics triggered an attack')
        orbs = final['attack']['orbs']
        require(len(orbs) == 21 and len({o['id'] for o in orbs}) == 21, f'{index}: final capacity fixture count differs')
        require(all(o['state'] == 0 and o['kind'] == 0 and o['transferCount'] == 0 and not o['hasTransferMotion'] for o in orbs), f'{index}: capacity rejection mutated orb state')
        owners = collections.Counter(o['owner'] for o in orbs)
        require(owners == {host['roster'][0]: 1, host['roster'][1]: 20}, f'{index}: capacity rejection changed ownership')
        require(all(not p['generatedTotal'] for p in final['resources']['players']), f'{index}: explicit capacity fixture mislabeled as paid generation')
    events = [event for result in players for event in motion_events(result)]
    motion_comparison = [compare_motion_pass(record, players, events) for record in reference_passes]
    full_id = next(o['id'] for o in host['finalGame']['attack']['orbs'] if o['owner'] == host['roster'][0])
    full_events = [event for event in events if event['orb'] == full_id]
    require(len(full_events) == 1 and full_events[0]['type'] == 'C6_P4_EDGE_CAPTURE'
        and full_events[0]['localPlayer'] == host['roster'][0] and full_events[0]['direction'] == 'TransferRight', 'Full recipient caused resume, retry flood, or missing capture')
    expected_ids = {full_id, *(record['id'] for record in reference_passes)}
    require(all(event['orb'] in expected_ids for event in events), 'Unexpected orb edge transaction')
    key = lambda proof: (proof['session'], proof['round'], proof['revision'])
    maps = []
    for result in players:
        entries = {}
        for proof in result['proofs']:
            proof_key = key(proof)
            require(proof['session'] == host['session'] and proof.get('hash'), 'Malformed aggregate proof')
            require(proof_key not in entries or entries[proof_key] == proof['hash'], 'One observer recorded conflicting same-revision state')
            entries[proof_key] = proof['hash']
        maps.append(entries)
    common = set.intersection(*(set(entries) for entries in maps))
    require(len(common) >= 10, f'Too few all-player state comparisons: {len(common)}')
    mismatches = [proof_key for proof_key in common if len({entries[proof_key] for entries in maps}) != 1]
    require(not mismatches, f'Aggregate state mismatches: {mismatches[:4]}')
    return dict(allPlayerCommonSnapshots=len(common), mismatches=0, passes=motion_comparison,
        approvedCrossings=sum(p['crossings'] for p in reference_passes), distinctAspectRatios=True,
        maximumSpeedIncrease=max(p['maximumSpeedIncrease'] for p in motion_comparison), speedTolerance=SPEED_TOLERANCE,
        normalizedHeightTolerance=HEIGHT_TOLERANCE, heldRetained=True, retryClean=True,
        fullRecipientStopped=True, fullRecipientStoredOrbs=20, preservedSourceOrb=full_id, fullRecipientAttempts=1,
        pointerScope='Synthetic pointer over real frames; explicit Raw/Combined/capacity fixtures; NOT physical Touch or Bonjour')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--app', required=True, help='Built .app/Contents/MacOS executable')
    parser.add_argument('--output', required=True)
    parser.add_argument('--count', type=int, choices=[2, 3, 4, 5], required=True)
    parser.add_argument('--port', type=int, default=25243)
    args = parser.parse_args()
    app = pathlib.Path(args.app).resolve()
    output = pathlib.Path(args.output)
    require(app.is_file() and output.is_absolute() and not output.exists(), 'Existing executable and fresh absolute output required')
    require(1024 <= args.port <= 65535, 'Port outside supported diagnostic range')
    output.mkdir(parents=True)
    children, results, comparison, error = [], {}, {}, None
    started, deadline = utc(), time.monotonic() + RUNNER_DEADLINE_SECONDS

    def wait(test, seconds, label):
        until = min(deadline, time.monotonic() + seconds)
        while not test():
            if time.monotonic() >= until:
                raise RuntimeError(label + ' timeout')
            time.sleep(.1)

    def launch(index):
        directory = output / str(index)
        log = (output / f'{index}-stdout.log').open('w')
        command = [str(app), '-screen-fullscreen', '0', '-screen-width', str(320 if index % 2 == 0 else 390),
            '-screen-height', '700', '-c6P4Output', str(directory), '-c6P4Index', str(index),
            '-c6P4Count', str(args.count), '-c6P4Port', str(args.port), '-logFile', str(output / f'{index}-unity.log')]
        process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT)
        children.append((index, process, log, directory))
        (output / f'{index}-launch.json').write_text(json.dumps(dict(command=command, pid=process.pid), indent=2))
        return process

    try:
        for index in range(args.count):
            process = launch(index)
            wait(lambda: (output / f'joined-{index}.json').exists() or process.poll() is not None, 25, f'join {index}')
            require(process.poll() is None, f'{index} exited during join: {read(output / str(index) / "result.json").get("error")}')
        wait(lambda: all(p.poll() is not None for _, p, _, _ in children), 180, 'all probes complete')
        for index, process, _, directory in children:
            results[str(index)] = read(directory / 'result.json')
            require(process.returncode == 0 and results[str(index)].get('status') == 'PASS',
                f'{index} failed: {results[str(index)].get("error")}')
            for name in ('01-continuous-stopped', '02-capacity-stop'):
                require((directory / (name + '.png')).stat().st_size > 1000, f'{index}: missing {name} screenshot')
        comparison = verify(results, args.count)
    except Exception as exception:
        error = str(exception)
    finally:
        running = []
        for index, process, log, directory in children:
            if process.poll() is None:
                running.append(dict(index=index, pid=process.pid))
            else:
                log.close()
            results[str(index)] = read(directory / 'result.json')
        summary = dict(status='FAIL' if error else 'PASS', error=error, participants=args.count,
            startedAtUtc=started, finishedAtUtc=utc(), app=str(app), appSha256=digest(app),
            runnerSha256=digest(pathlib.Path(__file__)), comparison=comparison,
            results={key: value.get('status', 'NOT_RUN') for key, value in results.items()},
            processesLeftRunning=running, physicalTouch=False, bonjourValidated=False,
            processPolicy=f'Only fresh diagnostic apps, {PROBE_DEADLINE_SECONDS}s self-deadline and graceful Application.Quit; no force kill.')
        (output / 'summary.json').write_text(json.dumps(summary, indent=2))
        print(json.dumps(summary, indent=2), flush=True)
    if error:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
