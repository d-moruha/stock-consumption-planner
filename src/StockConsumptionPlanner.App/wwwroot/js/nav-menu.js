(function () {
    if (window.scpNavMenuReady) {
        return;
    }

    window.scpNavMenuReady = true;

    function menuOverlay() {
        return document.getElementById("nav-overlay");
    }

    function menuButton() {
        return document.querySelector(".nav-toggle");
    }

    function setMenuOpen(open) {
        var menu = menuOverlay();
        var button = menuButton();
        if (menu === null) {
            return;
        }

        menu.classList.toggle("is-open", open);
        if (button !== null) {
            button.setAttribute("aria-expanded", open ? "true" : "false");
        }
    }

    function isMenuOpen() {
        var menu = menuOverlay();
        return menu !== null && menu.classList.contains("is-open");
    }

    document.addEventListener("click", function (event) {
        var target = event.target;
        if (target instanceof Text) {
            target = target.parentElement;
        }

        if (!(target instanceof Element)) {
            return;
        }

        if (target.closest(".nav-toggle") !== null) {
            setMenuOpen(!isMenuOpen());
            return;
        }

        if (!isMenuOpen()) {
            return;
        }

        if (target.closest(".nav-backdrop") !== null
            || target.closest(".nav-panel a") !== null) {
            setMenuOpen(false);
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key !== "Escape" || !isMenuOpen()) {
            return;
        }

        setMenuOpen(false);
        event.preventDefault();
        event.stopPropagation();
    }, true);
})();
