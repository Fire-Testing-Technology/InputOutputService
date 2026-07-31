(() => {
  const startBtn = document.getElementById("start-scan");
  const progress = document.getElementById("scan-progress");
  const status = document.getElementById("scan-status");
  const results = document.getElementById("scan-results");
  const log = document.getElementById("scan-log");
  if (!startBtn) return;

  let abort = null;
  let finished = false;
  const discovered = new Map();

  function appendLog(text, cls) {
    const line = document.createElement("div");
    if (cls) line.className = cls;
    line.textContent = `[${new Date().toLocaleTimeString()}] ${text}`;
    log.appendChild(line);
    log.scrollTop = log.scrollHeight;
  }

  function renderResults() {
    results.innerHTML = "";
    if (discovered.size === 0) {
      results.innerHTML = `<tr><td colspan="4" class="text-muted">No units discovered yet.</td></tr>`;
      return;
    }

    for (const unit of [...discovered.values()].sort((a, b) => a.unitId - b.unitId)) {
      const tr = document.createElement("tr");
      tr.innerHTML = `
        <td class="mono">${unit.unitId}</td>
        <td><span class="badge text-bg-secondary">${unit.detectedType}</span></td>
        <td>${unit.detail ?? "—"}</td>
        <td>${unit.registered ? '<span class="badge text-bg-success">yes</span>' : '<span class="badge text-bg-secondary">no</span>'}</td>`;
      results.appendChild(tr);
    }
  }

  function finish(message) {
    if (finished) return;
    finished = true;
    startBtn.disabled = false;
    startBtn.textContent = "Start scan";
    status.textContent = message;
  }

  function handleEvent(type, data) {
    switch (type) {
      case "started":
        appendLog(`started ${data.portName} units ${data.unitIdFrom}–${data.unitIdTo}`);
        status.textContent = `Scanning ${data.portName} (${data.unitIdFrom}–${data.unitIdTo})…`;
        break;
      case "probing":
        progress.style.width = `${data.percent ?? 0}%`;
        status.textContent = `Probing unit ${data.unitId} (${data.index}/${data.total})`;
        appendLog(`probing unit ${data.unitId} (${data.index}/${data.total})`);
        break;
      case "discovered":
        discovered.set(data.unitId, data);
        renderResults();
        appendLog(`discovered unit ${data.unitId} ${data.detectedType}`, "discovered");
        break;
      case "completed":
        progress.style.width = "100%";
        for (const unit of data.units ?? []) {
          discovered.set(unit.unitId, unit);
        }
        renderResults();
        appendLog(`completed in ${Math.round(data.durationMilliseconds)} ms — ${discovered.size} unit(s)`);
        finish(`Scan complete — ${discovered.size} unit(s) found.`);
        break;
      case "failed":
        appendLog(data?.message ?? "Scan failed.", "error");
        finish(data?.message ?? "Scan failed.");
        break;
      default:
        appendLog(`${type}: ${JSON.stringify(data)}`);
        break;
    }
  }

  function sleep(ms, signal) {
    return new Promise((resolve, reject) => {
      const timer = setTimeout(resolve, ms);
      signal?.addEventListener("abort", () => {
        clearTimeout(timer);
        reject(new DOMException("Aborted", "AbortError"));
      }, { once: true });
    });
  }

  startBtn.addEventListener("click", async () => {
    if (abort) {
      abort.abort();
      abort = null;
    }

    finished = false;
    discovered.clear();
    renderResults();
    log.innerHTML = "";
    progress.style.width = "0%";
    startBtn.disabled = true;
    startBtn.textContent = "Scanning…";
    status.textContent = "Starting scan…";
    appendLog("starting scan…");

    abort = new AbortController();
    const { signal } = abort;

    try {
      const startResponse = await fetch("/api/scan/start", {
        method: "POST",
        headers: { Accept: "application/json" },
        signal
      });

      if (!startResponse.ok) {
        let message = `Scan start failed (${startResponse.status}).`;
        try {
          const body = await startResponse.json();
          if (body?.message) message = body.message;
        } catch {
          /* ignore */
        }
        appendLog(message, "error");
        finish(message);
        return;
      }

      appendLog("scan started — polling progress");
      let after = 0;

      while (!finished) {
        const response = await fetch(`/api/scan/progress?after=${after}`, {
          headers: { Accept: "application/json" },
          signal,
          cache: "no-store"
        });

        if (!response.ok) {
          appendLog(`Progress request failed (${response.status}).`, "error");
          finish(`Progress request failed (${response.status}).`);
          return;
        }

        const snapshot = await response.json();
        for (const evt of snapshot.events ?? []) {
          handleEvent(evt.type, evt.data);
          if (finished) break;
        }
        after = snapshot.next ?? after;

        if (finished) break;
        if (snapshot.done) {
          if (!finished) {
            finish(snapshot.error ?? "Scan finished.");
            if (snapshot.error) {
              appendLog(snapshot.error, "error");
            }
          }
          break;
        }

        await sleep(100, signal);
      }
    } catch (err) {
      if (err?.name === "AbortError") {
        if (!finished) finish("Scan cancelled.");
        return;
      }
      const message = err?.message ?? "Scan failed.";
      appendLog(message, "error");
      finish(message);
    } finally {
      abort = null;
    }
  });
})();
