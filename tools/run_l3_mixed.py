#!/usr/bin/env python3
"""Existing P4 scenario with alternating IPv6/IPv4 direct clients.
Requires the diagnostic-only -c6P4Host extension in the newly built Mac app.
"""
import argparse
import importlib.util
import json
import re
from pathlib import Path
import subprocess
import sys
sys.dont_write_bytecode = True
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--runner', default=str(Path(__file__).with_name('p4_mac_smoke.py')))
    parser.add_argument('--count', type=int, choices=[3, 5], required=True)
    parser.add_argument('--port', type=int, default=25333)
    args = parser.parse_args()
    spec = importlib.util.spec_from_file_location('p4_mac_smoke', args.runner)
    p4 = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(p4)
    app, output = Path(args.app).resolve(), Path(args.output)
    p4.require(app.is_file() and output.is_absolute() and not output.exists(),
               'Existing executable and fresh absolute output required')
    p4.require(1024 <= args.port <= 65535, 'Port outside supported diagnostic range')
    output.mkdir(parents=True)
    children, results, comparison, error = [], {}, {}, None
    started, deadline = p4.utc(), time.monotonic() + p4.RUNNER_DEADLINE_SECONDS
    addresses = {str(i): ('::1' if i % 2 else '127.0.0.1') for i in range(1, args.count)}

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
                   '-c6P4Count', str(args.count), '-c6P4Port', str(args.port),
                   '-logFile', str(output / f'{index}-unity.log')]
        if index:
            command.extend(['-c6P4Host', addresses[str(index)]])
        process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT)
        children.append((index, process, log, directory))
        (output / f'{index}-launch.json').write_text(json.dumps(dict(command=command, pid=process.pid,
             directAddress=addresses.get(str(index)), role='Host' if index == 0 else 'Client'), indent=2))
        return process

    try:
        for index in range(args.count):
            process = launch(index)
            wait(lambda: (output / f'joined-{index}.json').exists() or process.poll() is not None, 25, f'join {index}')
            p4.require(process.poll() is None,
                       f'{index} exited during join: {p4.read(output / str(index) / "result.json").get("error")}')
        wait(lambda: all(p.poll() is not None for _, p, _, _ in children), 180, 'all probes complete')
        for index, process, _, directory in children:
            results[str(index)] = p4.read(directory / 'result.json')
            p4.require(process.returncode == 0 and results[str(index)].get('status') == 'PASS',
                       f'{index} failed: {results[str(index)].get("error")}')
            for name in ('01-continuous-stopped', '02-capacity-stop'):
                p4.require((directory / (name + '.png')).stat().st_size > 1000, f'{index}: missing {name} screenshot')
            p4.require(results[str(index)].get('victory') is True, f'{index}: missing shared Victory')
            p4.require(results[str(index)].get('actualHits') == (5 if index == 0 else 0), f'{index}: wrong actual hit count')
        for index in range(1, args.count):
            family = 'IPv6' if index % 2 else 'IPv4'
            log_text = (output / f'{index}-unity.log').read_text(errors='replace')
            p4.require(re.search(r'C6_NET_CONNECTION[^\n]*family=' + family + r' stage=Connected', log_text),
                       f'{index}: actual connected family {family} not recorded')
        comparison = p4.verify(results, args.count)
        comparison.update(sharedVictory=True, actualHost3DHits=5)
    except Exception as exception:
        error = str(exception)
    finally:
        running = []
        for index, process, log, directory in children:
            if process.poll() is None:
                running.append(dict(index=index, pid=process.pid))
            else:
                log.close()
            results[str(index)] = p4.read(directory / 'result.json')
        summary = dict(status='FAIL' if error else 'PASS', error=error, participants=args.count,
            startedAtUtc=started, finishedAtUtc=p4.utc(), app=str(app), appSha256=p4.digest(app),
            runnerSha256=p4.digest(Path(__file__)), baseRunnerSha256=p4.digest(Path(args.runner)),
            directClientAddresses=addresses, networkScope='Single Mac loopback, IPv6 odd clients and IPv4 even clients',
            diagnosticBuildRequirement='P4 JoinDirect uses explicit -c6P4Host; original default is unchanged',
            comparison=comparison, results={key: value.get('status', 'NOT_RUN') for key, value in results.items()},
            processesLeftRunning=running, physicalTouch=False, bonjourValidated=False,
            coverageGaps=['Paid generation and direct Yin+Yang combination are not performed by the P4 probe.',
                          'All five actual 3D hits are Host attacks; no client attacker +5 receipt assertion.',
                          'No physical Touch, Bonjour discovery, real wireless or separate mobile devices.'],
            processPolicy=f'Fresh diagnostic apps, {p4.PROBE_DEADLINE_SECONDS}s self-deadline and graceful Application.Quit; no force kill.')
        (output / 'summary.json').write_text(json.dumps(summary, indent=2))
        print(json.dumps({k: summary[k] for k in ('status','participants','error','processesLeftRunning')}), flush=True)
    return 1 if error else 0


if __name__ == '__main__':
    raise SystemExit(main())
