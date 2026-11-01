(() => {
  const host = document.querySelector('[data-variants]');
  const button = document.querySelector('[data-add-variant]');
  if (!host || !button) return;
  button.addEventListener('click', () => {
    const index = host.querySelectorAll('.admin-variant-row').length;
    const row = document.createElement('div');
    row.className = 'admin-variant-row';
    const fields = [
      ['Name','Name','text'], ['Color','Color','text'], ['Size','Size','text'], ['SKU','Sku','text'],
      ['Price override','PriceOverride','number'], ['Stock','StockQuantity','number']
    ];
    for (const [labelText, key, type] of fields) {
      const label = document.createElement('label'); label.textContent = labelText;
      const input = document.createElement('input'); input.name = `Variants[${index}].${key}`; input.type = type;
      if (key === 'PriceOverride') { input.min = '0.01'; input.step = '0.01'; }
      if (key === 'StockQuantity') { input.min = '0'; input.step = '1'; input.value = '0'; }
      label.append(input); row.append(label);
    }
    row.dataset.variantIndex = String(index);
    row.dataset.variantName = `Variant ${index + 1}`;
    const availableLabel = document.createElement('label'); availableLabel.className = 'admin-form__field--inline';
    const available = document.createElement('input'); available.type = 'checkbox'; available.name = `Variants[${index}].IsAvailable`; available.value = 'true'; available.checked = true;
    availableLabel.append(available, document.createTextNode(' Available'));
    const hidden = document.createElement('input'); hidden.type = 'hidden'; hidden.name = `Variants[${index}].IsAvailable`; hidden.value = 'false';
    availableLabel.append(hidden); row.append(availableLabel);
    const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'button button--outline'; remove.textContent = 'Remove'; remove.addEventListener('click', () => row.remove()); row.append(remove);
    host.append(row);
  });
})();
