(() => {
    document.querySelectorAll('[data-image-picker]').forEach((picker) => {
        const previewHost = picker.closest('.admin-image-upload')?.querySelector('[data-image-previews]');
        const primaryInput = picker.closest('.admin-image-upload')?.querySelector('[data-primary-new-image]');
        picker.addEventListener('change', () => {
            if (!previewHost) return;
            previewHost.replaceChildren();
            Array.from(picker.files || []).forEach((file, index) => {
                const card = document.createElement('label');
                card.className = 'admin-image-preview';
                const image = document.createElement('img');
                image.alt = file.name;
                image.src = URL.createObjectURL(file);
                const radio = document.createElement('input');
                radio.type = 'radio';
                radio.name = 'primary-new-image-selector';
                radio.checked = index === 0;
                radio.addEventListener('change', () => { if (primaryInput) primaryInput.value = String(index); });
                const label = document.createElement('span');
                label.textContent = ' Alt text';
                const alt = document.createElement('input');
                alt.type = 'text';
                alt.name = `NewImageAltTexts[${index}]`;
                alt.maxLength = 180;
                alt.placeholder = 'Describe this image';
                const variantLabel = document.createElement('span');
                variantLabel.textContent = ' Variant image (optional)';
                const variant = document.createElement('select');
                variant.name = `NewImageVariantIndexes[${index}]`;
                const productOption = document.createElement('option'); productOption.value = ''; productOption.textContent = 'Product gallery'; variant.append(productOption);
                document.querySelectorAll('[data-variants] .admin-variant-row').forEach((row, variantIndex) => {
                    const option = document.createElement('option'); option.value = String(variantIndex); option.textContent = row.dataset.variantName || `Variant ${variantIndex + 1}`; variant.append(option);
                });
                card.append(image, radio, document.createTextNode(' Primary'), label, alt, variantLabel, variant);
                previewHost.append(card);
            });
            if (primaryInput) primaryInput.value = picker.files?.length ? '0' : '';
        });
    });
})();
