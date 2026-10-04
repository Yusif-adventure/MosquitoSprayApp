// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
	const darkModeToggle = document.querySelector("[data-dark-mode-toggle]");
	const themeStorageKey = "smart-mosquito-theme";
	const applyTheme = (theme) => {
		document.documentElement.dataset.theme = theme;
		if (darkModeToggle) {
			const isDark = theme === "dark";
			darkModeToggle.setAttribute("aria-checked", isDark.toString());
			darkModeToggle.classList.toggle("is-on", isDark);
		}
	};

	let savedTheme = "light";
	try {
		savedTheme = window.localStorage.getItem(themeStorageKey) || "light";
	} catch {
		// Keep the light theme when browser storage is unavailable.
	}
	applyTheme(savedTheme);

	darkModeToggle?.addEventListener("click", () => {
		const theme = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
		applyTheme(theme);
		try {
			window.localStorage.setItem(themeStorageKey, theme);
		} catch {
			// The theme still applies for this page when browser storage is unavailable.
		}
	});

	const settingsToggle = document.querySelector("[data-settings-toggle]");
	const settingsOptions = document.querySelector("[data-settings-options]");
	if (settingsToggle && settingsOptions) {
		settingsToggle.addEventListener("click", () => {
			const isExpanded = settingsToggle.getAttribute("aria-expanded") === "true";
			settingsToggle.setAttribute("aria-expanded", (!isExpanded).toString());
			settingsOptions.hidden = isExpanded;
		});
	}

	const scanSprayerButton = document.querySelector("[data-scan-sprayer]");
	const scanPanel = document.querySelector("[data-sprayer-scan-panel]");
	const scanVideo = document.querySelector("[data-sprayer-scan-video]");
	const scanStatus = document.querySelector("[data-sprayer-scan-status]");
	const deviceIdInput = document.querySelector("#sprayer-device-id");
	const stopScanButton = document.querySelector("[data-stop-sprayer-scan]");
	if (scanSprayerButton && scanPanel && scanVideo && scanStatus && deviceIdInput && stopScanButton) {
		let scanStream;
		let scanFrame;
		let isScanning = false;

		const stopSprayerScan = () => {
			isScanning = false;
			if (scanFrame) {
				window.cancelAnimationFrame(scanFrame);
				scanFrame = undefined;
			}
			scanStream?.getTracks().forEach((track) => track.stop());
			scanStream = undefined;
			scanVideo.srcObject = null;
			scanPanel.hidden = true;
		};

		const getSprayerId = (value) => {
			const rawValue = value.trim();
			try {
				const scannedUrl = new URL(rawValue);
				const idFromQuery = scannedUrl.searchParams.get("deviceId") || scannedUrl.searchParams.get("id");
				if (idFromQuery) return idFromQuery.trim();
			} catch {
				// A QR code may contain the ID directly rather than a URL.
			}
			return rawValue;
		};

		scanSprayerButton.addEventListener("click", async () => {
			if (!("BarcodeDetector" in window) || !navigator.mediaDevices?.getUserMedia) {
				scanStatus.textContent = "QR scanning is not supported in this browser. Enter the sprayer ID manually.";
				return;
			}

			try {
				const detector = new window.BarcodeDetector({ formats: ["qr_code"] });
				scanStream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: "environment" } }, audio: false });
				scanPanel.hidden = false;
				scanVideo.srcObject = scanStream;
				await scanVideo.play();
				scanStatus.textContent = "Camera ready. Point it at the sprayer QR code.";
				isScanning = true;

				const scanFrameForCode = async () => {
					if (!isScanning) return;
					try {
						const codes = await detector.detect(scanVideo);
						if (codes.length) {
							const scannedId = getSprayerId(codes[0].rawValue);
						if (/^[a-z0-9][a-z0-9-]{3,63}$/i.test(scannedId)) {
							deviceIdInput.value = scannedId;
							scanStatus.textContent = "Sprayer ID scanned. Review it, then select Pair Sprayer.";
							deviceIdInput.focus();
							stopSprayerScan();
							return;
						}
						scanStatus.textContent = "That QR code does not contain a valid sprayer ID.";
						}
					} catch {
						scanStatus.textContent = "Unable to read the QR code. Adjust the camera and try again.";
					}
					scanFrame = window.requestAnimationFrame(scanFrameForCode);
				};
				scanFrame = window.requestAnimationFrame(scanFrameForCode);
			} catch {
				stopSprayerScan();
				scanStatus.textContent = "Camera access failed. Allow camera permission or enter the sprayer ID manually.";
			}
		});

		stopScanButton.addEventListener("click", () => {
			stopSprayerScan();
			scanStatus.textContent = "QR scanning cancelled.";
		});
		window.addEventListener("pagehide", stopSprayerScan);
	}

	document.querySelectorAll("[data-unlink-device]").forEach((form) => {
		form.addEventListener("submit", (event) => {
			const deviceName = form.querySelector("[aria-label^='Unlink']")?.getAttribute("aria-label")?.replace("Unlink ", "") || "this device";
			if (!window.confirm(`Unlink ${deviceName}?`)) {
				event.preventDefault();
			}
		});
	});

	const sprayDurationSetting = document.querySelector("[data-spray-duration-setting]");
	const sprayDurationValue = document.querySelector("[data-spray-duration-value]");
	if (sprayDurationSetting && sprayDurationValue) {
		const updateSprayDurationValue = () => {
			sprayDurationValue.textContent = `${sprayDurationSetting.value} sec`;
		};
		sprayDurationSetting.addEventListener("input", updateSprayDurationValue);
		updateSprayDurationValue();
	}

	const passwordInput = document.querySelector("[data-password-input]");
	const passwordToggle = document.querySelector("[data-password-toggle]");
	if (passwordInput && passwordToggle) {
		passwordToggle.addEventListener("click", () => {
			const isPassword = passwordInput.type === "password";
			passwordInput.type = isPassword ? "text" : "password";
			passwordToggle.setAttribute("aria-label", isPassword ? "Hide password" : "Show password");
		});
	}


	const form = document.querySelector("[data-spray-form]");
	const button = document.querySelector("[data-spray-button]");

	if (!form || !button) {
		return;
	}

	const label = form.querySelector("[data-spray-label]");
	const countdown = form.querySelector("[data-spray-countdown]");
	const status = form.querySelector("[data-spray-status]");
	const durationSeconds = Number.parseInt(form.dataset.duration, 10) || 30;
	let remainingSeconds = durationSeconds;
	let timerId;

	button.addEventListener("click", () => {
		if (button.disabled) {
			return;
		}

		button.disabled = true;
		button.setAttribute("aria-pressed", "true");
		button.setAttribute("aria-label", `Spraying, ${remainingSeconds} seconds remaining`);
		button.classList.add("is-spraying");
		label.textContent = "Spraying...";
		countdown.textContent = `${remainingSeconds}s`;
		status.textContent = `Spraying. ${remainingSeconds} seconds remaining.`;

		timerId = window.setInterval(() => {
			remainingSeconds -= 1;

			if (remainingSeconds <= 0) {
				window.clearInterval(timerId);
				button.classList.remove("is-spraying");
				button.setAttribute("aria-label", "Spray cycle complete");
				label.textContent = "Spray Complete";
				countdown.textContent = "0s";
				status.textContent = "Spray cycle complete.";
				form.submit();
				return;
			}

			countdown.textContent = `${remainingSeconds}s`;
			button.setAttribute("aria-label", `Spraying, ${remainingSeconds} seconds remaining`);
			status.textContent = `Spraying. ${remainingSeconds} seconds remaining.`;
		}, 1000);
	}, { once: true });
	const liveClock = document.querySelector("[data-live-clock]");
	if (liveClock) {
		const updateClock = () => {
			const now = new Date();
			let hours = now.getHours();
			const minutes = now.getMinutes().toString().padStart(2, '0');
			hours = hours % 12 || 12;
			liveClock.textContent = `${hours}:${minutes}`;
		};
		updateClock();
		window.setInterval(updateClock, 10000);
	}
})();
