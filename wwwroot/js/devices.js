// Linked-devices page: QR pairing and the unlink confirmation.
(() => {
	document.querySelectorAll("[data-unlink-device]").forEach((form) => {
		form.addEventListener("submit", (event) => {
			const deviceName = form.querySelector("[aria-label^='Unlink']")?.getAttribute("aria-label")?.replace("Unlink ", "") || "this device";
			if (!window.confirm(`Unlink ${deviceName}? It will stop receiving spray commands.`)) {
				event.preventDefault();
			}
		});
	});

	const scanSprayerButton = document.querySelector("[data-scan-sprayer]");
	const scanPanel = document.querySelector("[data-sprayer-scan-panel]");
	const scanVideo = document.querySelector("[data-sprayer-scan-video]");
	const scanStatus = document.querySelector("[data-sprayer-scan-status]");
	const deviceIdInput = document.querySelector("#sprayer-device-id");
	const stopScanButton = document.querySelector("[data-stop-sprayer-scan]");
	if (!(scanSprayerButton && scanPanel && scanVideo && scanStatus && deviceIdInput && stopScanButton)) {
		return;
	}

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
})();
