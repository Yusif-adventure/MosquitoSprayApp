// The server stores and emits UTC. Rewrite every <time data-local-time> into the viewer's own time zone.
(() => {
	const formats = {
		datetime: { weekday: "short", month: "short", day: "numeric", hour: "numeric", minute: "2-digit" },
		date: { weekday: "short", month: "short", day: "numeric", year: "numeric" },
		day: { weekday: "short", month: "short", day: "numeric" },
		time: { hour: "numeric", minute: "2-digit" }
	};

	document.querySelectorAll("time[data-local-time]").forEach((element) => {
		const date = new Date(element.getAttribute("datetime"));
		if (Number.isNaN(date.getTime())) {
			return;
		}

		const options = formats[element.dataset.localTime] || formats.datetime;
		element.textContent = new Intl.DateTimeFormat(undefined, options).format(date);
		element.title = date.toLocaleString();
	});
})();
