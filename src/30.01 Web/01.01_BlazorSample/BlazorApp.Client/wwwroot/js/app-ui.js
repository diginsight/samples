// UI helpers for the Blazor sample: resizable sidebar (VS Code style) and theme switching.
window.appUi = (function () {
    "use strict";

    const THEME_KEY = "app-theme";   // stored value: one of THEME_MODES keys
    const WIDTH_KEY = "app-sidebar-width";
    const MIN_WIDTH = 150;
    const MAX_WIDTH = 600;
    const DEFAULT_WIDTH = 250;

    // Theme id -> Bootstrap colour mode. Keep in sync with ThemeSelector.razor,
    // the app.css [data-theme] blocks and the inline script in index.html.
    const THEME_MODES = {
        azure: "light",
        emerald: "light",
        teal: "light",
        violet: "light",
        sunset: "light",
        midnight: "dark",
        slate: "dark",
        carbon: "dark"
    };
    const DEFAULT_THEME = "azure";

    function normalizeTheme(theme) {
        return THEME_MODES[theme] ? theme : DEFAULT_THEME;
    }

    // Match the browser chrome (address bar / PWA) to the current top-bar colour.
    function updateThemeColorMeta() {
        try {
            const color = getComputedStyle(document.documentElement)
                .getPropertyValue("--app-topbar-bg").trim();
            if (!color) { return; }
            let meta = document.querySelector('meta[name="theme-color"]');
            if (!meta) {
                meta = document.createElement("meta");
                meta.setAttribute("name", "theme-color");
                document.head.appendChild(meta);
            }
            meta.setAttribute("content", color);
        } catch (e) { /* ignore */ }
    }

    function applyTheme(theme) {
        const t = normalizeTheme(theme);
        const html = document.documentElement;
        html.setAttribute("data-theme", t);
        html.setAttribute("data-bs-theme", THEME_MODES[t]);
        try { localStorage.setItem(THEME_KEY, t); } catch (e) { /* ignore */ }
        updateThemeColorMeta();
        return t;
    }

    function getTheme() {
        try {
            return normalizeTheme(localStorage.getItem(THEME_KEY));
        } catch (e) {
            return DEFAULT_THEME;
        }
    }

    // Ensure the chrome colour is set for the theme applied by the inline boot script.
    updateThemeColorMeta();

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

    // Position an open dropdown so it never overflows the viewport, regardless of
    // where the toggle sits or how wide the browser is (an auto-flip like Popper):
    //   - Prefer aligning the menu's LEFT edge to the toggle (menu grows to the right).
    //   - If that would overflow the right edge, align the menu's RIGHT edge to the
    //     toggle (menu grows to the left).
    //   - If neither fits, pin to the viewport edge and cap the width.
    // Also caps the height to the space below the toggle. Inline styles set here win
    // over the stylesheet, so any CSS default alignment is just the pre-open fallback.
    function positionDropdown(toggleEl, menuEl) {
        if (!toggleEl || !menuEl) {
            return;
        }
        const margin = 8;
        const vw = document.documentElement.clientWidth;
        const vh = document.documentElement.clientHeight;

        // Reset before measuring.
        menuEl.style.left = "auto";
        menuEl.style.right = "auto";
        menuEl.style.maxWidth = "";

        const t = toggleEl.getBoundingClientRect();
        const menuW = menuEl.offsetWidth;

        if (t.left + menuW <= vw - margin) {
            // Fits when left-aligned to the toggle.
            menuEl.style.left = "0";
        } else if (t.right - menuW >= margin) {
            // Fits when right-aligned to the toggle.
            menuEl.style.right = "0";
        } else {
            // Too wide for either side: pin to the right and clamp the width.
            menuEl.style.right = "0";
            menuEl.style.maxWidth = (vw - 2 * margin) + "px";
        }

        // Keep the menu inside the viewport vertically.
        menuEl.style.maxHeight = Math.max(120, vh - t.bottom - margin) + "px";
        menuEl.style.overflowY = "auto";
    }

    return {
        applyTheme: applyTheme,
        getTheme: getTheme,
        initResizer: initResizer,
        positionDropdown: positionDropdown
    };
})();
