// Light/dark theme and the collapsible settings panel.
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
})();
