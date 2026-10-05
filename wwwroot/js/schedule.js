// Add-schedule form: default the picker to "an hour from now" and submit the chosen moment as UTC,
// so the server never has to guess the user's time zone.
(() => {
	const form = document.querySelector("[data-schedule-form]");
	const when = form?.querySelector("[data-schedule-when]");
	const utc = form?.querySelector("[data-schedule-utc]");
	if (!form || !when || !utc) {
		return;
	}

	const pad = (value) => String(value).padStart(2, "0");
	const toLocalInputValue = (date) =>
		`${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;

	if (!when.value) {
		const hours = Number.parseInt(when.dataset.defaultHours, 10) || 1;
		when.value = toLocalInputValue(new Date(Date.now() + hours * 60 * 60 * 1000));
	}

	form.addEventListener("submit", (event) => {
		// A datetime-local value has no zone; the Date constructor reads it as the browser's local time.
		const picked = new Date(when.value);
		if (Number.isNaN(picked.getTime())) {
			event.preventDefault();
			when.reportValidity();
			return;
		}
		utc.value = picked.toISOString();
	});
})();
