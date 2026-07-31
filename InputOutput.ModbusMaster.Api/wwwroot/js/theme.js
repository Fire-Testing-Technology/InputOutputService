// FTT theme preference (matches Aegis / FCAControls.Wpf ThemeType: Dark | Light)
window.theme = {
    storageKey: 'inputoutput-theme',

    get() {
        try {
            const stored = localStorage.getItem(this.storageKey);
            if (stored === 'light' || stored === 'dark') {
                return stored;
            }
        } catch {
            // localStorage unavailable
        }
        return 'dark';
    },

    apply(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        try {
            localStorage.setItem(this.storageKey, theme);
        } catch {
            // localStorage unavailable
        }
        document.querySelectorAll('.theme-toggle-btn').forEach((btn) => {
            btn.classList.toggle('active', btn.dataset.theme === theme);
        });
    },

    isDark() {
        return this.get() === 'dark';
    },

    set(theme) {
        if (theme !== 'light' && theme !== 'dark') {
            return this.isDark();
        }
        this.apply(theme);
        return theme === 'dark';
    },

    toggle() {
        return this.set(this.isDark() ? 'light' : 'dark');
    },

    init() {
        this.apply(this.get());
        document.querySelectorAll('.theme-toggle-btn').forEach((btn) => {
            btn.addEventListener('click', () => this.set(btn.dataset.theme));
        });
    }
};

window.theme.init();
