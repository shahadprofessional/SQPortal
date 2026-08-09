// Shared page behaviors, kept out of inline <script> blocks and inline
// on* attributes so the Content-Security-Policy can stay strict about
// script sources.
(function () {
    'use strict';

    // Confirmation prompts (data-confirm on a form or button). Delegated so
    // content loaded into dialogs (the case-details panel) is covered too.
    document.addEventListener('submit', function (e) {
        var form = e.target instanceof Element ? e.target.closest('form[data-confirm]') : null;
        if (form && !window.confirm(form.getAttribute('data-confirm'))) {
            e.preventDefault();
        }
    }, true);

    document.addEventListener('click', function (e) {
        var el = e.target instanceof Element ? e.target.closest('button[data-confirm]') : null;
        if (el && !window.confirm(el.getAttribute('data-confirm'))) {
            e.preventDefault();
            e.stopImmediatePropagation();
        }
    }, true);

    // Selects that submit their form on change (the weekly range picker).
    document.addEventListener('change', function (e) {
        var el = e.target instanceof Element ? e.target.closest('[data-autosubmit]') : null;
        if (el && el.form) el.form.submit();
    });

    // Status toasts.
    document.addEventListener('DOMContentLoaded', function () {
        if (!window.bootstrap) return;
        document.querySelectorAll('.toast.sq-auto-toast').forEach(function (el) {
            new bootstrap.Toast(el, { delay: 3500 }).show();
        });
    });
})();
