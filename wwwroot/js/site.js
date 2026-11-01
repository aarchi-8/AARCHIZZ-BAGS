(() => {
	const menuToggle = document.querySelector('.menu-toggle');
	const navigation = document.querySelector('#site-navigation');
	const searchToggle = document.querySelector('.search-toggle');
	const searchPanel = document.querySelector('#search-panel');

	menuToggle?.addEventListener('click', () => {
		const isOpen = menuToggle.getAttribute('aria-expanded') === 'true';
		menuToggle.setAttribute('aria-expanded', String(!isOpen));
		navigation?.classList.toggle('is-open', !isOpen);
	});

	searchToggle?.addEventListener('click', () => {
		const isOpen = searchToggle.getAttribute('aria-expanded') === 'true';
		searchToggle.setAttribute('aria-expanded', String(!isOpen));
		if (searchPanel) searchPanel.hidden = isOpen;
		if (!isOpen) document.querySelector('#site-search')?.focus();
	});

})();
