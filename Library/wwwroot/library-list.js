function initLists() {
    document.querySelectorAll('[data-list]').forEach(el => {
        new List(el, { valueNames: el.dataset.list.split(',') });
    });
}
document.addEventListener('DOMContentLoaded', initLists);
Blazor.addEventListener('enhancedload', initLists);
