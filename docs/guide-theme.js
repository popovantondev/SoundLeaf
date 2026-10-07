/* SoundLeaf guide appearance. No analytics, external scripts or network requests. */
(function () {
    "use strict";
    var key = "soundleaf-guide-theme", mode = "system";
    try { var saved = localStorage.getItem(key); if (saved === "light" || saved === "dark") mode = saved; } catch (_) {}
    var media = window.matchMedia("(prefers-color-scheme: dark)");
    function apply() {
        if (mode === "system") document.documentElement.removeAttribute("data-theme");
        else document.documentElement.setAttribute("data-theme", mode);
        var dark = mode === "dark" || (mode === "system" && media.matches);
        var lang = document.documentElement.lang;
        var words = { en: ["Theme", "system", "light", "dark"], de: ["Design", "System", "hell", "dunkel"], ru: ["Тема", "система", "светлая", "тёмная"] }[lang] || ["Theme", "system", "light", "dark"];
        var button = document.getElementById("theme-toggle");
        if (button) {
            button.textContent = words[0] + ": " + words[{system: 1, light: 2, dark: 3}[mode]];
            button.setAttribute("aria-label", button.textContent);
        }
        var shot = document.querySelector(".shot");
        if (shot) shot.src = shot.getAttribute("src").replace(/-control-(light|dark)\.png$/, "-control-" + (dark ? "dark" : "light") + ".png");
    }
    apply();
    document.addEventListener("DOMContentLoaded", function () {
        apply();
        document.getElementById("theme-toggle").addEventListener("click", function () {
            mode = { system: "light", light: "dark", dark: "system" }[mode];
            try { localStorage.setItem(key, mode); } catch (_) {}
            apply();
        });
    });
    if (media.addEventListener) media.addEventListener("change", apply);
    else media.addListener(apply);
}());
