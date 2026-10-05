// Manual spray page. The spray is queued on the server the moment the user taps (so closing the tab
// can't lose it); the on-screen countdown just mirrors the configured duration.
(() => {
	const form = document.querySelector("[data-spray-form]");
	const button = document.querySelector("[data-spray-button]");
	if (!form || !button) {
		return;
	}

	const label = form.querySelector("[data-spray-label]");
	const countdown = form.querySelector("[data-spray-countdown]");
	const status = form.querySelector("[data-spray-status]");
	const defaultDuration = Number.parseInt(form.dataset.duration, 10) || 30;
	const idleLabel = label.textContent;
	let timerId;

	const reset = () => {
		window.clearInterval(timerId);
		button.disabled = false;
		button.setAttribute("aria-pressed", "false");
		button.setAttribute("aria-label", "Tap to spray");
		button.classList.remove("is-spraying");
		label.textContent = idleLabel;
		countdown.textContent = "";
	};

	const runCountdown = (seconds) => {
		let remaining = seconds;
		button.classList.add("is-spraying");
		label.textContent = "Spraying...";
		countdown.textContent = `${remaining}s`;
		button.setAttribute("aria-label", `Spraying, ${remaining} seconds remaining`);
		status.textContent = `Spraying. ${remaining} seconds remaining.`;

		timerId = window.setInterval(() => {
			remaining -= 1;

			if (remaining <= 0) {
				window.clearInterval(timerId);
				button.classList.remove("is-spraying");
				button.setAttribute("aria-label", "Spray cycle complete");
				label.textContent = "Spray Complete";
				countdown.textContent = "0s";
				status.textContent = "Spray cycle complete.";
				window.setTimeout(reset, 3000);
				return;
			}

			countdown.textContent = `${remaining}s`;
			button.setAttribute("aria-label", `Spraying, ${remaining} seconds remaining`);
			status.textContent = `Spraying. ${remaining} seconds remaining.`;
		}, 1000);
	};

	button.addEventListener("click", async () => {
		if (button.disabled) {
			return;
		}

		button.disabled = true;
		button.setAttribute("aria-pressed", "true");
		label.textContent = "Starting...";
		status.textContent = "";

		try {
			const response = await fetch(form.action, {
				method: "POST",
				body: new FormData(form),
				headers: { "X-Requested-With": "XMLHttpRequest", "Accept": "application/json" },
				credentials: "same-origin"
			});

			let result = null;
			try {
				result = await response.json();
			} catch {
				// Not JSON (for example a redirect to the sign-in page after the session expired).
			}

			if (!response.ok || !result?.success) {
				throw new Error(result?.message || "Could not start the spray. Please sign in again and retry.");
			}

			runCountdown(Number.parseInt(result.durationSeconds, 10) || defaultDuration);
		} catch (error) {
			reset();
			status.textContent = error.message || "Could not start the spray.";
		}
	});
})();
