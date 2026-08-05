(() => {
  const grid = document.querySelector(".channel-grid[data-unit-id]");
  if (!grid) return;

  const unitId = Number(grid.dataset.unitId);
  const DEBOUNCE_MS = 120;

  function clamp(value, min, max) {
    const n = Number(value);
    if (Number.isNaN(n)) return min;
    return Math.min(max, Math.max(min, n));
  }

  function formatValue(value) {
    const n = Number(value);
    if (Number.isNaN(n)) return "0";
    return Number(n.toFixed(3)).toString();
  }

  function syncPair(numberInput, slider, fromSlider) {
    const min = Number(numberInput.min || 0);
    const max = Number(numberInput.max || 10);
    const raw = fromSlider ? slider.value : numberInput.value;
    const value = clamp(raw, min, max);
    const text = formatValue(value);
    numberInput.value = text;
    slider.value = value;
    return value;
  }

  async function applyChannel(card, channel, measurand) {
    const currentEl = card.querySelector(".channel-current-value");
    const voltageEl = card.querySelector(".channel-voltage-readout");
    const statusEl = card.querySelector(".channel-status");
    card.classList.add("is-pending");
    if (statusEl) statusEl.textContent = "";

    try {
      const response = await fetch(`/api/units/${unitId}/channels/${channel}`, {
        method: "PUT",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json"
        },
        body: JSON.stringify({ measurand })
      });

      if (!response.ok) {
        let message = `Failed to set channel ${channel} (${response.status}).`;
        try {
          const body = await response.json();
          if (body?.message) message = body.message;
        } catch {
          /* ignore */
        }
        throw new Error(message);
      }

      const dto = await response.json();
      if (currentEl && dto?.measurand != null) {
        currentEl.textContent = formatValue(dto.measurand);
      }
      if (voltageEl && dto?.voltage != null) {
        const eu = dto.electricalUnit ?? "V";
        voltageEl.textContent = `${formatValue(dto.voltage)} ${eu}`;
      }
      card.classList.remove("is-error");
    } catch (err) {
      card.classList.add("is-error");
      if (statusEl) statusEl.textContent = err?.message ?? "Set failed.";
    } finally {
      card.classList.remove("is-pending");
    }
  }

  for (const card of grid.querySelectorAll(".channel-card")) {
    const numberInput = card.querySelector(".channel-number");
    const slider = card.querySelector(".channel-slider");
    const channel = Number(card.dataset.channel);
    if (!numberInput || !slider || !channel) continue;

    let timer = null;
    let seq = 0;

    function scheduleApply(fromSlider) {
      const value = syncPair(numberInput, slider, fromSlider);
      const currentEl = card.querySelector(".channel-current-value");
      if (currentEl) currentEl.textContent = formatValue(value);

      const mySeq = ++seq;
      clearTimeout(timer);
      timer = setTimeout(async () => {
        await applyChannel(card, channel, value);
        if (mySeq !== seq) return;
      }, DEBOUNCE_MS);
    }

    numberInput.addEventListener("input", () => scheduleApply(false));
    numberInput.addEventListener("change", () => scheduleApply(false));
    slider.addEventListener("input", () => scheduleApply(true));
  }
})();
