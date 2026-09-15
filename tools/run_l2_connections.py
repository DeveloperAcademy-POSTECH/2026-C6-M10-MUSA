#!/usr/bin/env python3
"""Run explicit L2 Mac fixture pairs. Never terminate processes or erase previous output."""
from __future__ import annotations
import argparse
import json
import os
import plistlib
import re
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

SCENARIOS = ("fallback", "reject", "cancelpending", "cancelbetween")


def utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def emit(event: str, **values) -> None:
    print(json.dumps({"event": event, **values}, ensure_ascii=False), flush=True)


def write_json(path: Path, value) -> None:
    temporary = path.with_name(path.name + ".tmp")
    fd = os.open(temporary, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
    os.replace(temporary, path)


def read_json(path: Path):
    try:
        with path.open(encoding="utf-8") as stream:
            value = json.load(stream)
        return value if isinstance(value, dict) else None
    except (OSError, json.JSONDecodeError):
        return None


def safe_code(value) -> str | None:
    return value if isinstance(value, str) and re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]{0,63}", value) else None


def summarize_receipt(receipt: dict | None) -> dict:
    if receipt is None:
        return {"present": False}
    events = receipt.get("events") or []
    events = [event for event in events if isinstance(event, dict)]
    families = sorted({event.get("family") for event in events if event.get("family") in ("IPv4", "IPv6", "NONE", "DUAL")})
    codes = sorted({code for event in events if (code := safe_code(event.get("failureCode")))})
    stages = sorted({code for event in events if (code := safe_code(event.get("stage")))})
    return {
        "present": True,
        "status": receipt.get("status") if receipt.get("status") in ("PASS", "FAIL", "OBSERVING") else "UNKNOWN",
        "appBuild": receipt.get("appBuild") if receipt.get("appBuild") == "26" else "MISMATCH",
        "gameBuild": receipt.get("gameBuild") if receipt.get("gameBuild") == "24" else "MISMATCH",
        "mode": receipt.get("mode") if receipt.get("mode") == "EXPLICIT_MAC_AUTOMATION" else "MISMATCH",
        "errorPresent": bool(receipt.get("error")),
        "runtimeErrorPresent": bool(receipt.get("runtimeError")),
        "expectedFirstCandidateConnectErrorCount": receipt.get("expectedFirstCandidateConnectErrorCount", 0),
        "truncated": bool(receipt.get("truncated")),
        "maximumParticipants": receipt.get("maximumParticipants"),
        "duplicateRosterObserved": bool(receipt.get("duplicateRosterObserved")),
        "observedTwo": bool(receipt.get("observedTwo")),
        "observedReturnToOne": bool(receipt.get("observedReturnToOne")),
        "rejoined": bool(receipt.get("rejoined")),
        "cleanFinalLeave": bool(receipt.get("cleanFinalLeave")),
        "initialStateReadyObserved": any(event.get("initialStateReady") is True for event in events),
        "eventCount": len(events),
        "families": families,
        "stages": stages,
        "failureCodes": codes,
        "maximumCandidateAttempt": max((event.get("candidateAttempt", 0) for event in events if isinstance(event.get("candidateAttempt", 0), int)), default=0),
        "firstJoinNonceFingerprintCount": len(receipt.get("firstJoinNonceFingerprints") or []),
        "rejectionWasBuildMismatch": receipt.get("rejectionReason") == "BUILD_MISMATCH",
        "cancelObserved": receipt.get("cancelObserved") if receipt.get("cancelObserved") in ("PENDING", "BETWEEN_CANDIDATES") else "NONE",
        "cancelledAttempt": receipt.get("cancelledAttempt"),
        "completedPhases": [phase for phase in (receipt.get("completedPhases") or []) if phase in (
            "first-joined", "first-left", "rejected", "cancelled", "rejoined", "final-left")],
        "gameplay": "NOT_ASSERTED_BY_PROBE",
    }


def validate_receipt(kind: str, scenario: str, receipt: dict | None, code: int | None) -> tuple[dict, list[str]]:
    view = summarize_receipt(receipt)
    failures: list[str] = []
    if code != 0:
        failures.append(kind + "_NONZERO_OR_UNFINISHED_EXIT")
    if not receipt:
        failures.append(kind + "_RECEIPT_MISSING")
        return view, failures
    for valid, reason in (
        (view["status"] == "PASS", "TERMINAL_PASS_MISSING"),
        (view["appBuild"] == "26" and view["gameBuild"] == "24", "BUILD_MISMATCH"),
        (view["mode"] == "EXPLICIT_MAC_AUTOMATION", "WRONG_MODE"),
        (not view["errorPresent"] and not view["runtimeErrorPresent"], "RUNTIME_OR_ASSERTION_ERROR"),
        (not view["truncated"], "EVIDENCE_TRUNCATED"),
        (not view["duplicateRosterObserved"] and view["maximumParticipants"] == 2, "ROSTER_INVALID"),
        (view["initialStateReadyObserved"], "INITIAL_STATE_NOT_OBSERVED"),
    ):
        if not valid:
            failures.append(kind + "_" + reason)
    if kind == "HOST":
        if not view["observedTwo"] or not view["observedReturnToOne"]:
            failures.append("HOST_JOIN_LEAVE_NOT_OBSERVED")
    else:
        if not view["rejoined"] or not view["cleanFinalLeave"]:
            failures.append("CLIENT_REJOIN_LEAVE_NOT_CONFIRMED")
        if "IPv4" not in view["families"]:
            failures.append("CLIENT_IPV4_REJOIN_NOT_OBSERVED")
        expected_errors = view["expectedFirstCandidateConnectErrorCount"]
        if expected_errors not in (0, 1) or (scenario not in ("fallback", "cancelbetween") and expected_errors != 0):
            failures.append("CLIENT_UNEXPECTED_ERROR_ALLOWANCE")
        if scenario == "fallback":
            if view["maximumCandidateAttempt"] != 2 or "IPv6" not in view["families"]:
                failures.append("CLIENT_IPV6_FALLBACK_NOT_CONFIRMED")
            if view["firstJoinNonceFingerprintCount"] != 1:
                failures.append("CLIENT_RETRY_NONCE_CHANGED_OR_MISSING")
        if scenario == "reject":
            if not view["rejectionWasBuildMismatch"] or view["maximumCandidateAttempt"] != 1:
                failures.append("CLIENT_REJECTION_DID_NOT_STOP_AT_FIRST_CANDIDATE")
        if scenario.startswith("cancel"):
            wanted = "BETWEEN_CANDIDATES" if scenario == "cancelbetween" else "PENDING"
            if view["cancelObserved"] != wanted or view["cancelledAttempt"] != 1 or view["maximumCandidateAttempt"] != 1:
                failures.append("CLIENT_CANCELLATION_NOT_CONFIRMED")
        if not {"rejoined", "final-left"}.issubset(view["completedPhases"]):
            failures.append("CLIENT_HOST_BARRIERS_INCOMPLETE")
    return view, failures


def launch(executable: Path, case: Path, shared: Path, kind: str, scenario: str, port: int) -> subprocess.Popen:
    output = case / (kind + "-output")  # The probe alone creates this fresh output directory.
    stdout = case / (kind + "-stdout.log")
    fd = os.open(stdout, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "wb") as log:
        return subprocess.Popen([
            str(executable), "-batchmode", "-nographics", "-logFile", str(case / (kind + ".log")),
            "-c6L2Output", str(output), "-c6L2Shared", str(shared), "-c6L2Role", kind,
            "-c6L2Scenario", scenario, "-c6L2Port", str(port),
        ], stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)


def await_condition(check, seconds: float, processes: tuple[subprocess.Popen, ...] = ()) -> str:
    deadline = time.monotonic() + seconds
    while True:
        if check():
            return "READY"
        if any(process.poll() is not None for process in processes):
            return "PROCESS_EXITED"
        if time.monotonic() >= deadline:
            return "BUDGET_EXPIRED"
        time.sleep(.2)


def run_case(executable: Path, root: Path, scenario: str, port: int, args) -> dict:
    case = root / scenario
    case.mkdir(mode=0o700)
    shared = case / "markers"
    shared.mkdir(mode=0o700)
    result = {"scenario": scenario, "startedUtc": utc(), "status": "RUNNING", "hostExitCode": None,
              "clientExitCode": None, "failures": [], "forcedTermination": False}
    host = client = None
    try:
        host = launch(executable, case, shared, "host", scenario, port)
        result["hostPid"] = host.pid
        write_json(case / "summary.json", result)
        emit("host_started", scenario=scenario, pid=host.pid)
        state = await_condition(lambda: read_json(shared / "host-ready.json") is not None,
                                args.host_ready_seconds, (host,))
        if state != "READY":
            result["status"] = "BLOCKED" if host.poll() is None else "FAIL"
            result["failures"].append("HOST_READY_" + state)
            return result
        client = launch(executable, case, shared, "client", scenario, port)
        result["clientPid"] = client.pid
        emit("client_started", scenario=scenario, pid=client.pid)
        state = await_condition(lambda: client.poll() is not None, args.client_seconds, (host,))
        if state != "READY":
            result["status"] = "BLOCKED"
            result["failures"].append("CLIENT_COMPLETION_" + state)
            return result
        # An explicit marker asks the fixture to close its own room and exit normally.
        # No signal, termination API, process-kill command or timeout-kill is ever used.
        (shared / "stop-host").write_text("Client completed; finish the owned fixture normally.\n", encoding="utf-8")
        state = await_condition(lambda: host.poll() is not None, args.host_exit_seconds)
        if state != "READY":
            result["status"] = "BLOCKED"
            result["failures"].append("HOST_GRACEFUL_EXIT_" + state)
            return result
        host_view, host_errors = validate_receipt("HOST", scenario, read_json(case / "host-output/result.json"), host.poll())
        client_view, client_errors = validate_receipt("CLIENT", scenario, read_json(case / "client-output/result.json"), client.poll())
        result["hostReceipt"] = host_view
        result["clientReceipt"] = client_view
        result["failures"] += host_errors + client_errors
        result["status"] = "PASS" if not result["failures"] else "FAIL"
        return result
    except (OSError, ValueError) as error:
        result["status"] = "BLOCKED" if any(p is not None and p.poll() is None for p in (host, client)) else "FAIL"
        # Exception messages may contain private paths; keep only the type in the shareable summary.
        result["failures"].append("RUNNER_" + type(error).__name__)
        return result
    finally:
        result["finishedUtc"] = utc()
        result["hostExitCode"] = host.poll() if host is not None else None
        result["clientExitCode"] = client.poll() if client is not None else None
        result["livePids"] = [p.pid for p in (host, client) if p is not None and p.poll() is None]
        if "hostReceipt" not in result:
            result["hostReceipt"] = summarize_receipt(read_json(case / "host-output/result.json"))
        if "clientReceipt" not in result:
            result["clientReceipt"] = summarize_receipt(read_json(case / "client-output/result.json"))
        write_json(case / "summary.json", result)
        emit("case_complete", scenario=scenario, status=result["status"], failures=result["failures"], livePids=result["livePids"])


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("app", type=Path, help="Built L2 Development .app containing C6_L2_CHECKS")
    parser.add_argument("--output-root", type=Path, required=True, help="New private output directory; must not already exist")
    parser.add_argument("--base-port", type=int, default=25360)
    parser.add_argument("--scenarios", nargs="+", choices=SCENARIOS, default=list(SCENARIOS))
    parser.add_argument("--host-ready-seconds", type=float, default=45)
    parser.add_argument("--client-seconds", type=float, default=112)
    parser.add_argument("--host-exit-seconds", type=float, default=12)
    args = parser.parse_args()
    app = args.app.expanduser().resolve()
    root = args.output_root.expanduser().absolute()
    if not app.is_dir() or root.exists() or not 1024 <= args.base_port <= 65531:
        parser.error("A built app, a nonexistent output root, and a base port from1024to65531 are required.")
    if len(set(args.scenarios)) != len(args.scenarios) or min(args.host_ready_seconds, args.client_seconds, args.host_exit_seconds) <= 0:
        parser.error("Scenarios must be unique and wait budgets must be positive.")
    with (app / "Contents/Info.plist").open("rb") as stream:
        info = plistlib.load(stream)
    name = info.get("CFBundleExecutable")
    if not isinstance(name, str) or Path(name).name != name:
        parser.error("The app's executable name is invalid.")
    executable = app / "Contents/MacOS" / name
    if not executable.is_file() or not os.access(executable, os.X_OK):
        parser.error("The app executable is missing or not executable.")
    root.mkdir(parents=True, mode=0o700)
    report = {"task": "L2", "startedUtc": utc(), "status": "RUNNING", "scenarios": [],
              "gameplay": "NOT_ASSERTED_BY_PROBE", "forcedTermination": False,
              "notes": "Receipts and logs remain local. Summaries omit endpoint addresses, room identifiers, and nonce fingerprints."}
    write_json(root / "summary.json", report)
    for index, scenario in enumerate(args.scenarios):
        result = run_case(executable, root, scenario, args.base_port + index, args)
        report["scenarios"].append(result)
        write_json(root / "summary.json", report)
        if result["status"] == "BLOCKED" or result["livePids"]:
            break  # Preserve the running processes and all output; do not create extra fixture pairs.
    statuses = [result["status"] for result in report["scenarios"]]
    report["finishedUtc"] = utc()
    report["status"] = "BLOCKED" if "BLOCKED" in statuses else "PASS" if len(statuses) == len(args.scenarios) and all(s == "PASS" for s in statuses) else "FAIL"
    report["requestedCases"] = len(args.scenarios)
    report["attemptedCases"] = len(statuses)
    report["completedCases"] = sum(status in ("PASS", "FAIL") for status in statuses)
    write_json(root / "summary.json", report)
    emit("run_complete", status=report["status"], completedCases=report["completedCases"], requestedCases=report["requestedCases"])
    return {"PASS": 0, "FAIL": 1, "BLOCKED": 2}[report["status"]]


if __name__ == "__main__":
    raise SystemExit(main())
