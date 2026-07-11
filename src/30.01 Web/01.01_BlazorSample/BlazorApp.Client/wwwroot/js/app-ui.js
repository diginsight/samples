// UI helpers for the Blazor sample: resizable sidebar (VS Code style) and theme switching.
window.appUi = (function () {
    "use strict";

    const THEME_KEY = "app-theme";   // stored value: light | dark | auto
    const WIDTH_KEY = "app-sidebar-width";
    const MIN_WIDTH = 150;
    const MAX_WIDTH = 600;
    const DEFAULT_WIDTH = 250;

    function normalizeTheme(theme) {
        return (theme === "dark" || theme === "auto") ? theme : "light";
    }

    function resolveMode(theme) {
        if (theme === "auto") {
            return (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches)
                ? "dark" : "light";
        }
        return theme === "dark" ? "dark" : "light";
    }

    function applyTheme(theme) {
        const t = normalizeTheme(theme);
        const html = document.documentElement;
        html.setAttribute("data-theme", t);
        html.setAttribute("data-bs-theme", resolveMode(t));
        try { localStorage.setItem(THEME_KEY, t); } catch (e) { /* ignore */ }
        return t;
    }

    function getTheme() {
        try {
            return normalizeTheme(localStorage.getItem(THEME_KEY));
        } catch (e) {
            return "light";
        }
    }

    // Re-apply the theme when the system preference changes while in "auto" mode.
    if (window.matchMedia) {
        const mq = window.matchMedia("(prefers-color-scheme: dark)");
        const onChange = function () {
            if (getTheme() === "auto") {
                document.documentElement.setAttribute("data-bs-theme", resolveMode("auto"));
            }
        };
        if (mq.addEventListener) {
            mq.addEventListener("change", onChange);
        } else if (mq.addListener) {
            mq.addListener(onChange);
        }
    }

    function clampWidth(px) {
        return Math.min(MAX_WIDTH, Math.max(MIN_WIDTH, px));
    }

    function setWidth(px, persist) {
        const w = clampWidth(px);
        document.documentElement.style.setProperty("--sidebar-width", w + "px");
        if (persist) {
            try { localStorage.setItem(WIDTH_KEY, w + "px"); } catch (e) { /* ignore */ }
        }
        return w;
    }

    function initResizer() {
        const handle = document.querySelector(".sidebar-resizer");
        if (!handle || handle.dataset.bound === "1") {
            return;
        }
        handle.dataset.bound = "1";

        let dragging = false;

        function onMove(e) {
            if (!dragging) {
                return;
            }
            const x = (e.touches && e.touches.length) ? e.touches[0].clientX : e.clientX;
            setWidth(x, false);
        }

        function onUp() {
            if (!dragging) {
                return;
            }
            dragging = false;
            document.body.classList.remove("resizing");
            const current = getComputedStyle(document.documentElement)
                .getPropertyValue("--sidebar-width").trim();
            try { localStorage.setItem(WIDTH_KEY, current); } catch (e) { /* ignore */ }
            window.removeEventListener("pointermove", onMove);
            window.removeEventListener("pointerup", onUp);
        }

        handle.addEventListener("pointerdown", function (e) {
            dragging = true;
            document.body.classList.add("resizing");
            window.addEventListener("pointermove", onMove);
            window.addEventListener("pointerup", onUp);
            e.preventDefault();
        });

        // Double-click the handle to reset to the default width.
        handle.addEventListener("dblclick", function () {
            setWidth(DEFAULT_WIDTH, true);
        });

        // Keyboard support for accessibility.
        handle.addEventListener("keydown", function (e) {
            const current = parseInt(getComputedStyle(document.documentElement)
                .getPropertyValue("--sidebar-width"), 10) || DEFAULT_WIDTH;
            if (e.key === "ArrowLeft") {
                setWidth(current - 10, true);
                e.preventDefault();
            } else if (e.key === "ArrowRight") {
                setWidth(current + 10, true);
                e.preventDefault();
            }
        });
    }

    return {
        applyTheme: applyTheme,
        getTheme: getTheme,
        initResizer: initResizer
    };
})();
