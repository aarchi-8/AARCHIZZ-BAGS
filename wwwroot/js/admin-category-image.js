(() => {
    document.querySelectorAll('[data-category-image-control]').forEach((control) => {
        const picker = control.querySelector('[data-category-image-picker]');
        const filename = control.querySelector('[data-category-image-filename]');
        const preview = control.querySelector('[data-category-image-preview]');
        if (!picker || !filename || !preview) return;

        const originalSource = preview.dataset.originalSrc;
        const originalAlt = preview.dataset.originalAlt;
        let objectUrl;

        picker.addEventListener('change', () => {
            if (objectUrl) URL.revokeObjectURL(objectUrl);
            const file = picker.files?.[0];
            filename.textContent = file?.name || 'No image selected';
            if (file) {
                objectUrl = URL.createObjectURL(file);
                preview.src = objectUrl;
                preview.alt = file.name;
            } else {
                objectUrl = undefined;
                preview.src = originalSource;
                preview.alt = originalAlt;
            }
        });

        control.closest('form')?.addEventListener('submit', () => {
            if (objectUrl) URL.revokeObjectURL(objectUrl);
        });
    });
})();
