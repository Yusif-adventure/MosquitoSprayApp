// Small, page-independent UI behaviour: password toggle, settings slider label, clock, flash messages.
(() => {
	const passwordInput = document.querySelector("[data-password-input]");
	const passwordToggle = document.querySelector("[data-password-toggle]");
	if (passwordInput && passwordToggle) {
		passwordToggle.setAttribute("aria-pressed", "false");
		passwordToggle.addEventListener("click", () => {
			const isPassword = passwordInput.type === "password";
			passwordInput.type = isPassword ? "text" : "password";
			passwordToggle.setAttribute("aria-label", isPassword ? "Hide password" : "Show password");
			passwordToggle.setAttribute("aria-pressed", isPassword.toString());
		});
	}

	const sprayDurationSetting = document.querySelector("[data-spray-duration-setting]");
	const sprayDurationValue = document.querySelector("[data-spray-duration-value]");
	if (sprayDurationSetting && sprayDurationValue) {
		const updateSprayDurationValue = () => {
			sprayDurationValue.textContent = `${sprayDurationSetting.value} sec`;
		};
		sprayDurationSetting.addEventListener("input", updateSprayDurationValue);
		updateSprayDurationValue();
	}

	const liveClock = document.querySelector("[data-live-clock]");
	if (liveClock) {
		const updateClock = () => {
			const now = new Date();
			const hours = now.getHours() % 12 || 12;
			const minutes = now.getMinutes().toString().padStart(2, "0");
			liveClock.textContent = `${hours}:${minutes}`;
		};
		updateClock();
		window.setInterval(updateClock, 10000);
	}

	// Flash messages fade out on their own (errors stay a bit longer) and can be dismissed with a click.
	document.querySelectorAll("[data-flash]").forEach((flash) => {
		const dismiss = () => flash.remove();
		flash.addEventListener("click", dismiss);
		window.setTimeout(dismiss, flash.classList.contains("flash-error") ? 10000 : 6000);
	});
})();
