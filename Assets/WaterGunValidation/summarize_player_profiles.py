"""Summarize validated 0.6.5 player captures without pooling different settings."""
import argparse
import hashlib
import json
import math
import statistics
from pathlib import Path


def stats(values):
    values = sorted(values)
    if not values:
        return {"count": 0}
    return dict(count=len(values), mean=statistics.mean(values), median=statistics.median(values),
                p95=values[math.ceil(len(values) * .95) - 1], maximum=max(values))


def summarize(root):
    reports = []
    for run in ("off-r1", "off-r2", "on-r1", "on-r2", "off-cpu-r1", "off-cpu-r2"):
        folder = root / run
        source = folder / "samples.json"
        data = json.loads(source.read_text(encoding="utf-8"))
        assert len(data["rows"]) == 2160, run
        assert data["actualThreadingMode"] == ("LegacyJobified" if run.startswith("on-") else "MultiThreaded"), run
        assert all(data["availability"].values()), run
        assert data["surfaceCount"] == 7 and data["geometryCount"] == 4, run
        gpu = json.loads((folder / "gpu.json").read_text()) if run.startswith("off-r") else []
        conditions = []
        for scenario in range(12):
            rows = [r for r in data["rows"] if r["scenario"] == scenario]
            assert len(rows) == 180, (run, scenario)
            metrics = {}
            for index, name in enumerate(data["metricNames"]):
                divisor = 1 if name == "GC Allocated In Frame" or name.endswith("Count") else 1e6
                metrics[name] = stats([r["metrics"][index] / divisor for r in rows])
            assert metrics["Draw Calls Count"]["median"] > 0, (run, scenario, "no rendering")
            assert (sum(r["firing"] for r in rows) == 180) == (scenario % 6 == 4), (run, scenario, "fire mismatch")
            assert (sum(r["deposits"] for r in rows) == 180) == (scenario % 6 == 4), (run, scenario, "deposit mismatch")
            gpu_frames = [r for r in gpu if r["scenario"] == scenario and r["sample"] and r["gpuNs"] > 0]
            assert len({r["frame"] for r in gpu_frames}) == len(gpu_frames), "duplicated GPU frames"
            if gpu:
                assert len(gpu_frames) >= 170, (run, scenario, "missing GPU capture")
            conditions.append(dict(scenario=scenario, mode=data["modes"][scenario % 6], motion="stress" if scenario >= 6 else "stationary",
                                   elapsedMs=stats([r["elapsedMs"] for r in rows]), metrics=metrics,
                                   gpuMs=stats([r["gpuNs"] / 1e6 for r in gpu_frames]),
                                   over16_67Ms=sum(r["elapsedMs"] > 1000/60 for r in rows),
                                   pathCounts=sorted(set(r["pathCount"] for r in rows)),
                                   geometryRefresh=stats([r["geometryRefresh"] for r in rows]),
                                   firingFrames=sum(r["firing"] for r in rows), deposits=sum(r["deposits"] for r in rows)))
        files = [source] + sorted(folder.glob("scenario-*.raw"))
        reports.append(dict(run=run, metadata={k: v for k, v in data.items() if k not in ("rows", "availability", "metricNames", "modes")},
                            gpuProfilerEnabled=run.startswith("off-r"), conditions=conditions,
                            evidence=[dict(path=str(p.relative_to(root.parent)), bytes=p.stat().st_size,
                                           sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in files]))
    audit_path = root / "gpu-audit.json"
    return dict(date="2026-10-04", version="WaterGun 0.6.5", productionSourceCommit="950a982", cpuSamples=12960,
                validGpuSamples=sum(c["gpuMs"]["count"] for r in reports for c in r["conditions"]),
                scope="Development Windows Player; explicit 1200x600 offscreen rendering; Warudo wrapper and presentation excluded",
                percentile="nearest rank p95", gpuAudit=json.loads(audit_path.read_text()) if audit_path.exists() else [], reports=reports)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    result = summarize(args.root)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"cpuSamples": result["cpuSamples"], "validGpuSamples": result["validGpuSamples"], "runs": len(result["reports"]) }))
