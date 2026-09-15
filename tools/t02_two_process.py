#!/usr/bin/env python3
"""Run explicit T02 loopback transport checks against a built development macOS app."""
import argparse
import json
import pathlib
import plistlib
import socket
import subprocess
import time


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--app", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    info = plistlib.loads((args.app / "Contents/Info.plist").read_bytes())
    executable = args.app / "Contents/MacOS" / info["CFBundleExecutable"]
    children, handles, checks = [], [], []

    def free_port():
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
            sock.bind(("127.0.0.1", 0))
            return sock.getsockname()[1]

    def read(name):
        path = args.output / (name + ".json")
        try:
            return json.loads(path.read_text())
        except (FileNotFoundError, json.JSONDecodeError):
            return {}

    def wait_for(predicate, seconds, label):
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            if predicate():
                checks.append({"check": label, "result": "PASS"})
                return
            time.sleep(0.2)
        raise AssertionError(label + " timed out")

    def launch(name, role, scenario, port, cycles=1, hold=3):
        handle = (args.output / (name + "-process.log")).open("w")
        handles.append(handle)
        process = subprocess.Popen([
            str(executable), "-batchmode", "-nographics",
            "-screen-width", "390", "-screen-height", "844",
            "-logFile", str(args.output / (name + "-player.log")),
            "-c6Role", role, "-c6Scenario", scenario,
            "-c6Host", "127.0.0.1", "-c6Port", str(port),
            "-c6Cycles", str(cycles), "-c6Hold", str(hold),
            "-c6Results", str(args.output / (name + ".json"))
        ], stdout=handle, stderr=subprocess.STDOUT)
        children.append((name, process))
        return process

    def connected(name, count):
        value = read(name)
        return value.get("currentState") == "Connected" and value.get("participantCount") == count

    def completed(name, process):
        return process.poll() == 0 and read(name).get("result") == "PASS"

    try:
        port = free_port()
        host = launch("cycles-host", "host", "cycles", port, cycles=2)
        wait_for(lambda: connected("cycles-host", 1), 35, "host starts with its own participant")
        client = launch("cycles-client", "client", "cycles", port, cycles=2)
        wait_for(lambda: completed("cycles-host", host) and completed("cycles-client", client),
                 80, "two independent processes connect, end, and manually connect again")
        for name, expected in [("cycles-host", 4), ("cycles-client", 2)]:
            observations = read(name)["observations"]
            notifications = [s for s in observations if s.startswith("STATE Connected ")]
            assert len(notifications) == expected, (name, notifications)
            checks.append({"check": name + " connection callback count", "result": "PASS", "observed": len(notifications)})

        port = free_port()
        host = launch("capacity-host", "host", "cycles", port, hold=15)
        wait_for(lambda: connected("capacity-host", 1), 35, "capacity host starts")
        client = launch("capacity-client", "client", "cycles", port, hold=15)
        wait_for(lambda: connected("capacity-host", 2) and connected("capacity-client", 2),
                 15, "legitimate two participants connected before rejection check")
        third = launch("capacity-third", "client", "reject", port)
        wait_for(lambda: completed("capacity-third", third), 12, "third instance gets explicit room-full rejection")
        assert host.poll() is None and client.poll() is None
        assert connected("capacity-host", 2) and connected("capacity-client", 2)
        checks.append({"check": "rejected third instance does not end legitimate connection", "result": "PASS"})
        wait_for(lambda: completed("capacity-host", host) and completed("capacity-client", client),
                 35, "legitimate participants end normally after rejection")

        client = launch("timeout-client", "client", "timeout", free_port())
        wait_for(lambda: completed("timeout-client", client), 30, "unused endpoint fails, permits manual retry and cancellation")
        result = {"task": "T02", "result": "PASS", "scope": "Actual separate macOS player processes over IPv4 loopback; not two iPhones or automatic discovery.", "checks": checks}
        print(json.dumps(result, indent=2), flush=True)
        (args.output / "summary.json").write_text(json.dumps(result, indent=2) + "\n")
    except Exception as error:
        result = {"task": "T02", "result": "FAIL", "error": str(error), "checks": checks,
                  "processes": [{"name": name, "exitCode": process.poll(), "receipt": read(name)} for name, process in children]}
        (args.output / "summary.json").write_text(json.dumps(result, indent=2) + "\n")
        raise
    finally:
        # These are only the test players this script created, never a Unity Editor or user app.
        for _, process in children:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
        for handle in handles:
            handle.close()


if __name__ == "__main__":
    main()
