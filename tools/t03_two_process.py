#!/usr/bin/env python3
"""Run explicit T03 automatic requests in two independently built macOS players.

No physical iPhone input or two-iPhone gate result is inferred from this script.
Each scenario starts a new session, sends 50 unique requests per participant,
and replays each participant's last request without increasing the shared value.
"""
import argparse
import datetime
import json
import pathlib
import plistlib
import socket
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    parser.add_argument("--scenario", choices=("both", "sequential", "concurrent"), default="both")
    args = parser.parse_args()
    args.app = args.app.resolve()
    args.output = args.output.resolve()
    args.output.mkdir(parents=True, exist_ok=False)
    info = plistlib.loads((args.app / "Contents/Info.plist").read_bytes())
    executable = args.app / "Contents/MacOS" / info["CFBundleExecutable"]
    if not executable.is_file():
        raise FileNotFoundError(executable)
    children, handles, checks, scenarios = [], [], [], []

    def read(name):
        try:
            return json.loads((args.output / (name + ".json")).read_text())
        except (FileNotFoundError, json.JSONDecodeError):
            return {}

    def check(success, label, observed=None):
        item = {"check": label, "result": "PASS" if success else "FAIL"}
        if observed is not None:
            item["observed"] = observed
        checks.append(item)
        if not success:
            raise AssertionError(label)

    def wait_for(predicate, seconds, label, active=()):
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            if predicate():
                check(True, label)
                return
            for name, process in active:
                code = process.poll()
                receipt = read(name)
                if code not in (None, 0) or receipt.get("result") == "FAIL":
                    raise AssertionError(f"{label}: {name} failed, exit={code}, receipt={receipt}")
                if code == 0 and receipt.get("result") != "PASS":
                    raise AssertionError(f"{label}: {name} exited without a PASS receipt")
            time.sleep(0.1)
        raise TimeoutError(label)

    def free_port():
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
            sock.bind(("127.0.0.1", 0))
            return sock.getsockname()[1]

    def launch(name, role, scenario, port, begin, release):
        handle = (args.output / (name + "-process.log")).open("w")
        handles.append(handle)
        process = subprocess.Popen([
            str(executable), "-batchmode", "-nographics",
            "-screen-width", "390", "-screen-height", "844",
            "-logFile", str(args.output / (name + "-player.log")),
            "-c3Role", role, "-c3Scenario", scenario,
            "-c3Host", "127.0.0.1", "-c3Port", str(port),
            "-c3Begin", str(begin), "-c3Release", str(release),
            "-c3Results", str(args.output / (name + ".json"))
        ], stdout=handle, stderr=subprocess.STDOUT)
        children.append((name, process))
        return process

    def ready(name):
        receipt = read(name)
        return receipt.get("currentState") == "Connected" and receipt.get("snapshotReady") is True

    def completed(name, process):
        return process.poll() == 0 and read(name).get("result") == "PASS"

    def run_scenario(scenario):
        port = free_port()
        begin = args.output / (scenario + "-begin.signal")
        release = args.output / (scenario + "-release.signal")
        host_name, client_name = scenario + "-host", scenario + "-client"
        host = launch(host_name, "host", scenario, port, begin, release)
        active = [(host_name, host)]
        host_phase = "PREFILLED" if scenario == "sequential" else "READY_FOR_PEER"
        wait_for(lambda: ready(host_name) and read(host_name).get("phase") == host_phase,
                 45, scenario + " host ready before client launch", active)
        if scenario == "sequential":
            check(read(host_name).get("value") == 50 and read(host_name).get("participantCount") == 1,
                  "sequential host really has fifty before the second process starts")
        client = launch(client_name, "client", scenario, port, begin, release)
        active.append((client_name, client))
        if scenario == "concurrent":
            wait_for(lambda: all(ready(name) and read(name).get("phase") == "READY_FOR_REQUESTS"
                                 for name, _ in active),
                     35, "both processes hold at the concurrent start barrier", active)
            check(all(read(name).get("value") == 0 for name, _ in active),
                  "concurrent requests begin from shared zero")
            begin.write_text("Begin explicit automatic requests.\n")

        wait_for(lambda: all(read(name).get("phase") == "READY_TO_RELEASE" for name, _ in active),
                 60, scenario + " both final records ready before shutdown", active)
        records = {name: read(name) for name, _ in active}
        for name, receipt in records.items():
            expected = {"participantCount": 2, "value": 100, "approved": 100,
                        "rejected": 0, "revision": 100, "sent": 50, "acknowledged": 50, "pending": 0}
            check(all(receipt.get(key) == value for key, value in expected.items()),
                  name + " authoritative value, approvals, revision, and local accounting", receipt)
            check(receipt.get("duplicates", 0) >= 1, name + " duplicate receipt without increment")
        session_ids = [receipt.get("sessionId") for receipt in records.values()]
        check(bool(session_ids[0]) and session_ids[0] == session_ids[1], scenario + " same session on both players")
        check(records[host_name].get("localClientId") != records[client_name].get("localClientId"),
              scenario + " distinct actual participant identities")
        if scenario == "sequential":
            check(records[client_name].get("initialSnapshotValue") == 50,
                  "joining client received the complete prefilled state before sending")
        else:
            def utc(value):
                return datetime.datetime.fromisoformat(value.replace("Z", "+00:00"))
            latest_start = max(utc(receipt["sendStartedUtc"]) for receipt in records.values())
            earliest_end = min(utc(receipt["sendEndedUtc"]) for receipt in records.values())
            check(latest_start < earliest_end, "the two actual process send windows overlap")

        release.write_text("Both independent final records compared successfully.\n")
        wait_for(lambda: all(completed(name, process) for name, process in active),
                 20, scenario + " both processes exit zero with preserved PASS receipts", active)
        scenarios.append({"scenario": scenario, "result": "PASS", "port": port,
                          "sessionId": session_ids[0], "receipts": [name + ".json" for name, _ in active]})

    try:
        for scenario in ("sequential", "concurrent") if args.scenario == "both" else (args.scenario,):
            run_scenario(scenario)
        if len(scenarios) == 2:
            check(scenarios[0]["sessionId"] != scenarios[1]["sessionId"], "independent scenarios use fresh sessions")
        result = {"task": "T03", "result": "PASS",
                  "scope": "Actual separate macOS player processes over IPv4 loopback with automatic requests. Physical iPhone input and two-iPhone G2 gate: NOT_RUN.",
                  "scenarios": scenarios, "checks": checks}
        (args.output / "summary.json").write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result, indent=2), flush=True)
    except Exception as error:
        result = {"task": "T03", "result": "FAIL", "error": str(error), "scenarios": scenarios, "checks": checks,
                  "processes": [{"name": name, "exitCode": process.poll(), "receipt": read(name)}
                                for name, process in children]}
        (args.output / "summary.json").write_text(json.dumps(result, indent=2) + "\n")
        raise
    finally:
        # Only children launched by this harness are stopped; no Editor or user app is touched.
        for name, process in children:
            if process.poll() is None:
                (args.output / (name + "-cleanup.txt")).write_text("Harness cleanup requested termination of its own test player.\n")
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    (args.output / (name + "-cleanup.txt")).write_text("Test child did not terminate within ten seconds; forcibly stopped this harness-owned child.\n")
                    process.kill()
                    process.wait()
        for handle in handles:
            handle.close()


if __name__ == "__main__":
    main()
